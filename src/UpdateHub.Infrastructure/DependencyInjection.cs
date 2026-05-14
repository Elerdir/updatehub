using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Infrastructure.Email;
using UpdateHub.Infrastructure.Persistence;
using UpdateHub.Infrastructure.Persistence.Repositories;
using UpdateHub.Infrastructure.Security;
using UpdateHub.Infrastructure.Storage;

namespace UpdateHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var dbPath      = config["UpdateHub:DatabasePath"] ?? "updatehub.db";
        var storagePath = config["UpdateHub:StoragePath"]  ?? "artifacts";
        var baseUrl     = config["UpdateHub:BaseUrl"]      ?? "";

        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        // Data Protection — keys persisted next to the database so they survive
        // container restarts (an ephemeral key ring would lock out 2FA on restart).
        var dbDir    = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        var keysPath = config["UpdateHub:DataProtectionKeysPath"]
            ?? Path.Combine(string.IsNullOrEmpty(dbDir) ? "." : dbDir, "dp-keys");
        Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("UpdateHub");
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddScoped<IAppRepository,          AppRepository>();
        services.AddScoped<IReleaseRepository,      ReleaseRepository>();
        services.AddScoped<IArtifactRepository,     ArtifactRepository>();
        services.AddScoped<ISettingsRepository,     SettingsRepository>();
        services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.AddScoped<IAuditRepository,        AuditRepository>();

        services.AddSingleton<IArtifactStorage>(_ => new LocalArtifactStorage(storagePath));

        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<EmailNotificationService>();

        // Background notification queue — webhooks/email run off the request path
        services.AddSingleton<BackgroundNotificationQueue>();
        services.AddSingleton<INotificationQueue>(sp => sp.GetRequiredService<BackgroundNotificationQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundNotificationQueue>());

        services.AddHttpClient("webhook");
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<AuditService>();
        services.AddScoped<AdminService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<BruteForceProtectionService>();
        services.AddScoped<UpdateResolverService>(sp => new UpdateResolverService(
            sp.GetRequiredService<IReleaseRepository>(),
            baseUrl));

        return services;
    }

    public static void MigrateDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
}
