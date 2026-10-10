using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using UpdateHub.Application.Interfaces;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Web.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    // Keep connection alive for the lifetime of the factory — SQLite in-memory DB
    // disappears when the last connection closes, so we hold one open.
    private readonly SqliteConnection _connection =
        new($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");

    public TestWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace SQLite (file) with SQLite in-memory — same provider, no conflict
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            // Replace file storage with a no-op stub
            services.RemoveAll<IArtifactStorage>();
            var storage = Substitute.For<IArtifactStorage>();
            storage.OpenRead(Arg.Any<string>()).Returns(new MemoryStream([1, 2, 3]));
            services.AddSingleton(storage);

            // Replace health checks with no-ops — the real ones hit disk/DB paths
            // that behave differently in in-memory test environments
            services.Configure<HealthCheckServiceOptions>(opts => opts.Registrations.Clear());
        });
    }

    // Initialise schema once the host is built (called before first test in the fixture)
    public void EnsureSchemaCreated()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
