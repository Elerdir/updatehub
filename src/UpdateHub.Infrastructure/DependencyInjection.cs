using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Infrastructure.Persistence;
using UpdateHub.Infrastructure.Persistence.Repositories;
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

        services.AddScoped<IAppRepository,      AppRepository>();
        services.AddScoped<IReleaseRepository,  ReleaseRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();

        services.AddSingleton<IArtifactStorage>(_ => new LocalArtifactStorage(storagePath));

        services.AddScoped<AdminService>();
        services.AddScoped<UpdateResolverService>(sp => new UpdateResolverService(
            sp.GetRequiredService<IReleaseRepository>(),
            sp.GetRequiredService<IArtifactRepository>(),
            baseUrl));

        return services;
    }

    public static void EnsureDatabaseCreated(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }
}
