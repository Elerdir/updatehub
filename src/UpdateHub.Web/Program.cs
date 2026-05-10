using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OtpNet;
using Scalar.AspNetCore;
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
    .AddCookie("TwoFactorPending", options =>
    {
        options.Cookie.Name     = "updatehub-2fa-pending";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan  = TimeSpan.FromMinutes(5);
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddInfrastructure(builder.Configuration);

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

// OpenAPI
builder.Services.AddOpenApi();

// Health checks
var storagePath = builder.Configuration["UpdateHub:StoragePath"] ?? "artifacts";
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck("disk_space", new DiskSpaceHealthCheck(storagePath, minimumFreeBytes: 1L * 1024 * 1024 * 1024));

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
    app.Services.MigrateDatabase();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseForwardedHeaders();

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

app.MapPost("/account/login", async (HttpContext ctx, IConfiguration config,
    BruteForceProtectionService bruteForce, AuditService audit, SettingsService settings) =>
{
    var ip        = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = ctx.Request.Headers.UserAgent.ToString();

    if (await bruteForce.IsBlockedAsync(ip))
    {
        await audit.LogAsync("LoginBlocked", actor: ip, ip: ip);
        ctx.Response.Redirect("/login?error=blocked");
        return;
    }

    var form     = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var adminUser = config["UpdateHub:Admin:Username"] ?? "admin";
    // Check DB-stored hash first, fall back to config
    var dbHash    = await settings.GetAdminPasswordHashAsync();
    var adminHash = string.IsNullOrWhiteSpace(dbHash)
        ? (config["UpdateHub:Admin:PasswordHash"] ?? "")
        : dbHash;

    var valid = string.Equals(username, adminUser, StringComparison.OrdinalIgnoreCase)
             && !string.IsNullOrEmpty(adminHash)
             && BCrypt.Net.BCrypt.Verify(password, adminHash);

    if (!valid)
    {
        await bruteForce.RecordFailureAsync(ip, userAgent);
        await audit.LogAsync("LoginFailed", actor: username, ip: ip,
            details: $"Failed attempt from {ip}");
        ctx.Response.Redirect("/login?error=1");
        return;
    }

    await bruteForce.RecordSuccessAsync(ip);

    // If 2FA is enabled, issue a pending cookie and redirect to TOTP page
    if (await settings.IsTotpEnabledAsync())
    {
        var pendingClaims   = new[] { new Claim(ClaimTypes.Name, username) };
        var pendingIdentity = new ClaimsIdentity(pendingClaims, "TwoFactorPending");
        await ctx.SignInAsync("TwoFactorPending", new ClaimsPrincipal(pendingIdentity));
        ctx.Response.Redirect("/login/totp");
        return;
    }

    await audit.LogAsync("LoginSuccess", actor: username, ip: ip);

    var claims   = new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    ctx.Response.Redirect("/");
}).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("login");

app.MapPost("/account/totp", async (HttpContext ctx, SettingsService settings, AuditService audit) =>
{
    var result = await ctx.AuthenticateAsync("TwoFactorPending");
    if (!result.Succeeded) { ctx.Response.Redirect("/login"); return; }

    var form   = await ctx.Request.ReadFormAsync();
    var code   = form["code"].ToString().Replace(" ", "");
    var ip     = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var secret = await settings.GetTotpSecretAsync();
    if (string.IsNullOrEmpty(secret)) { ctx.Response.Redirect("/login"); return; }

    var totp  = new Totp(Base32Encoding.ToBytes(secret));
    var valid = totp.VerifyTotp(DateTime.UtcNow, code, out _, new VerificationWindow(1, 1));

    if (!valid)
    {
        await audit.LogAsync("LoginFailed2FA", actor: result.Principal!.Identity!.Name ?? "?", ip: ip);
        ctx.Response.Redirect("/login/totp?error=1");
        return;
    }

    await ctx.SignOutAsync("TwoFactorPending");

    var username = result.Principal!.Identity!.Name ?? "admin";
    await audit.LogAsync("LoginSuccess", actor: username, ip: ip);

    var claims   = new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    ctx.Response.Redirect("/");
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/account/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<AppShell>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
