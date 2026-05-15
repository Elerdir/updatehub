using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Web.Authorization;
using UpdateHub.Web.Localization;
using UpdateHub.Web.Startup;
using Serilog;
using UpdateHub.Application.Services;
using UpdateHub.Infrastructure;
using UpdateHub.Infrastructure.Persistence;
using UpdateHub.Web.Components;
using UpdateHub.Web.Endpoints;
using UpdateHub.Web.HealthChecks;

// ── Serilog ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Allow large file uploads (installers can be 500 MB+) via Blazor SignalR
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 512 * 1024 * 1024);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/login";
        options.AccessDeniedPath  = "/login";
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddCookie(AuthEndpoints.TwoFactorPendingScheme, options =>
    {
        options.Cookie.Name     = "updatehub-2fa-pending";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan  = TimeSpan.FromMinutes(5);
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddInfrastructure(builder.Configuration);

// HttpContext-backed current-user accessor so Application services can do role guards
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Rate limiting
builder.Services.AddRateLimiter(o =>
{
    // Public update-check endpoints: 60 req/min per IP
    o.AddSlidingWindowLimiter("public-api", opts =>
    {
        opts.PermitLimit          = 60;
        opts.Window               = TimeSpan.FromMinutes(1);
        opts.SegmentsPerWindow    = 6;
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit           = 0;
    });
    // Login endpoint: 10 attempts per 5 minutes per IP
    o.AddSlidingWindowLimiter("login", opts =>
    {
        opts.PermitLimit          = 10;
        opts.Window               = TimeSpan.FromMinutes(5);
        opts.SegmentsPerWindow    = 5;
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit           = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Localization — cs/en/de via the standard .AspNetCore.Culture cookie
var supportedCultures = UiStrings.Languages
    .Select(l => new CultureInfo(l.Code))
    .ToArray();
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture("en");
    o.SupportedCultures     = supportedCultures;
    o.SupportedUICultures   = supportedCultures;
});
builder.Services.AddScoped<Translator>();

// OpenAPI
builder.Services.AddOpenApi();

// Health checks
var storagePath = builder.Configuration["UpdateHub:StoragePath"] ?? "artifacts";
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck("disk_space", new DiskSpaceHealthCheck(storagePath, minimumFreeBytes: 1L * 1024 * 1024 * 1024));

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.MigrateDatabase();
    await BootstrapSeeder.SeedAdminAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseForwardedHeaders();

app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRateLimiter();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    // CSP: allow Blazor SignalR websockets + same-origin scripts/styles
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' ws: wss:; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none';";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseForceChangePassword();
app.UseAntiforgery();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            status    = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks    = report.Entries.Select(e => new
            {
                name    = e.Key,
                status  = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
}).AllowAnonymous();

// OpenAPI document + Scalar UI (always available on personal server)
app.MapOpenApi();
app.MapScalarApiReference("/api/docs");

app.MapPublicEndpoints();
app.MapCiEndpoints();
app.MapAuthEndpoints();
app.MapAdminEndpoints();

// Set the UI language cookie and bounce back to where the user was.
// GET is fine — switching language is not a security-sensitive action and
// keeping it GET means the chooser can be a plain <a> link in the sidebar.
app.MapGet("/account/culture", (HttpContext ctx, string lang, string? returnUrl) =>
{
    var code = UiStrings.Languages.Any(l => l.Code == lang) ? lang : "en";
    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(code)),
        new CookieOptions
        {
            Expires     = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite    = SameSiteMode.Lax,
            HttpOnly    = false,
        });
    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
}).AllowAnonymous();

app.MapRazorComponents<AppShell>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
