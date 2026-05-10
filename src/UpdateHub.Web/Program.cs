using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using UpdateHub.Application.Services;
using UpdateHub.Infrastructure;
using UpdateHub.Web.Components;
using UpdateHub.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddInfrastructure(builder.Configuration);

// Rate limiting — 60 req/min per IP on public update-check endpoints
builder.Services.AddRateLimiter(o =>
{
    o.AddSlidingWindowLimiter("public-api", opts =>
    {
        opts.PermitLimit         = 60;
        opts.Window              = TimeSpan.FromMinutes(1);
        opts.SegmentsPerWindow   = 6;
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit          = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
    app.Services.EnsureDatabaseCreated();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

// OpenAPI document + Scalar UI (always available on personal server)
app.MapOpenApi();
app.MapScalarApiReference("/api/docs");

app.MapPublicEndpoints();
app.MapCiEndpoints();

app.MapPost("/account/login", async (HttpContext ctx, IConfiguration config,
    BruteForceProtectionService bruteForce) =>
{
    var ip        = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = ctx.Request.Headers.UserAgent.ToString();

    if (await bruteForce.IsBlockedAsync(ip))
    {
        ctx.Response.Redirect("/login?error=blocked");
        return;
    }

    var form     = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var adminUser = config["UpdateHub:Admin:Username"] ?? "admin";
    var adminHash = config["UpdateHub:Admin:PasswordHash"] ?? "";

    var valid = string.Equals(username, adminUser, StringComparison.OrdinalIgnoreCase)
             && BCrypt.Net.BCrypt.Verify(password, adminHash);

    if (!valid)
    {
        await bruteForce.RecordFailureAsync(ip, userAgent);
        ctx.Response.Redirect("/login?error=1");
        return;
    }

    await bruteForce.RecordSuccessAsync(ip);

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
