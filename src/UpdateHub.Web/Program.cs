using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UpdateHub.Web.Components;
using UpdateHub.Web.Data;
using UpdateHub.Web.Endpoints;
using UpdateHub.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        options.MaximumReceiveMessageSize = 512 * 1024 * 1024); // 512 MB for file uploads

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var dbPath = builder.Configuration["UpdateHub:DatabasePath"] ?? "updatehub.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<UpdateResolverService>();
builder.Services.AddSingleton<ArtifactStorageService>();

var app = builder.Build();

// Create DB schema on first run
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPublicEndpoints();
app.MapCiEndpoints();

// Login form POST
app.MapPost("/account/login", async (HttpContext ctx, IConfiguration config) =>
{
    var form     = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var adminUser = config["UpdateHub:Admin:Username"] ?? "admin";
    var adminHash = config["UpdateHub:Admin:PasswordHash"] ?? "";

    var valid = string.Equals(username, adminUser, StringComparison.OrdinalIgnoreCase)
             && BCrypt.Net.BCrypt.Verify(password, adminHash);

    if (!valid)
    {
        ctx.Response.Redirect("/login?error=1");
        return;
    }

    var claims    = new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") };
    var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    ctx.Response.Redirect("/");
}).AllowAnonymous().DisableAntiforgery();

// Logout
app.MapPost("/account/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
