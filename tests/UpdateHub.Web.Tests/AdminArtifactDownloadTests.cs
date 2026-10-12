using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using UpdateHub.Infrastructure.Persistence;
using Xunit;

namespace UpdateHub.Web.Tests;

public class AdminArtifactDownloadTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminArtifactDownloadTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Download_Anonymous_IsRejected()
    {
        var artifactId = await SeedArtifactAsync("anon-dl-app");

        var res = await _client.GetAsync($"/admin/artifacts/{artifactId}/download");

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Download_SignedIn_ReturnsFile_WithoutCountingDownload()
    {
        var artifactId = await SeedArtifactAsync("admin-dl-app");
        await SignInAsync("dl-admin", "Passw0rd!Long");

        var res = await _client.GetAsync($"/admin/artifacts/{artifactId}/download");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Hive-0.1.0-x64.msi", res.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal([1, 2, 3], await res.Content.ReadAsByteArrayAsync());

        using var scope = _factory.Services.CreateScope();
        var artifact = await scope.ServiceProvider.GetRequiredService<IArtifactRepository>().GetByIdAsync(artifactId);
        Assert.Equal(0, artifact!.DownloadCount);
    }

    [Fact]
    public async Task Download_UnknownArtifact_Returns404()
    {
        await SignInAsync("dl-admin-404", "Passw0rd!Long");

        var res = await _client.GetAsync($"/admin/artifacts/{Guid.NewGuid()}/download");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private async Task<Guid> SeedArtifactAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var app = new App { Slug = slug, Name = slug };
        var release = new Release { AppId = app.Id, App = app, Version = "0.1.0", Status = ReleaseStatus.Archived };
        var artifact = new Artifact
        {
            ReleaseId    = release.Id,
            Platform     = "windows",
            Architecture = "x64",
            FileName     = "Hive-0.1.0-x64.msi",
            StoredPath   = "/fake/path",
            Sha256       = "abc123"
        };

        db.Apps.Add(app);
        db.Releases.Add(release);
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();
        return artifact.Id;
    }

    private async Task SignInAsync(string username, string password)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(new User
            {
                Username     = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4),
                Role         = UserRole.Viewer,
                IsActive     = true,
            });
        }

        var res = await _client.PostAsync("/account/login", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
    }
}
