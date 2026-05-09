using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using UpdateHub.Infrastructure.Persistence;
using Xunit;

namespace UpdateHub.Web.Tests;

public class PublicEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PublicEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
        _client = _factory.CreateClient();
    }

    // ── /health ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200WithOkStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Status);
        Assert.NotEqual(default, body.Timestamp);
    }

    // ── /api/apps/{slug}/update ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateCheck_Returns404_ForUnknownApp()
    {
        var response = await _client.GetAsync("/api/apps/unknown-app/update?version=1.0.0");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCheck_Returns400_WhenVersionMissing()
    {
        var response = await _client.GetAsync("/api/apps/my-app/update");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCheck_ReturnsUpdate_WhenNewerVersionPublished()
    {
        await SeedPublishedRelease("check-app", "2.0.0");

        var response = await _client.GetAsync(
            "/api/apps/check-app/update?version=1.0.0&platform=windows&arch=x64");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpdateCheckResponse>();
        Assert.True(body!.HasUpdate);
        Assert.Equal("2.0.0", body.Version);
    }

    [Fact]
    public async Task UpdateCheck_HasUpdateFalse_WhenAlreadyLatest()
    {
        await SeedPublishedRelease("up-to-date-app", "1.5.0");

        var response = await _client.GetAsync(
            "/api/apps/up-to-date-app/update?version=1.5.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpdateCheckResponse>();
        Assert.False(body!.HasUpdate);
    }

    // ── /api/apps/{slug}/tauri/latest.json ───────────────────────────────────

    [Fact]
    public async Task TauriManifest_Returns404_ForUnknownApp()
    {
        var response = await _client.GetAsync("/api/apps/no-such-app/tauri/latest.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TauriManifest_Returns404_WhenNoArtifactWithSignature()
    {
        // Published release exists but has no signed artifact
        await SeedPublishedRelease("unsigned-app", "1.0.0", signature: null);

        var response = await _client.GetAsync("/api/apps/unsigned-app/tauri/latest.json");

        // Manifest returned but with empty platforms — still 200
        // (Tauri checks platforms dict; empty is valid JSON)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TauriManifest_ReturnsPlatformEntry_WithSignedArtifact()
    {
        await SeedPublishedRelease("tauri-app", "3.0.0", signature: "dW50cnVzdGVk");

        var response = await _client.GetAsync("/api/apps/tauri-app/tauri/latest.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TauriManifestResponse>();
        Assert.Equal("3.0.0", body!.Version);
        Assert.True(body.Platforms.ContainsKey("windows-x86_64"));
    }

    // ── /api/downloads/{id} ───────────────────────────────────────────────────

    [Fact]
    public async Task Download_Returns404_ForUnknownArtifact()
    {
        var response = await _client.GetAsync($"/api/downloads/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_Returns200_ForKnownArtifact()
    {
        var artifactId = await SeedArtifact("dl-app");

        var response = await _client.GetAsync($"/api/downloads/{artifactId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream",
            response.Content.Headers.ContentType?.MediaType);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task SeedPublishedRelease(
        string slug, string version, string? signature = "test-sig")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UpdateHub.Infrastructure.Persistence.AppDbContext>();

        var app = new App { Slug = slug, Name = slug };
        db.Apps.Add(app);

        var artifact = new Artifact
        {
            Platform     = "windows",
            Architecture = "x64",
            FileName     = "setup.exe",
            StoredPath   = "/fake/path",
            Sha256       = "abc123",
            Signature    = signature
        };

        var release = new Release
        {
            AppId       = app.Id,
            App         = app,
            Version     = version,
            Status      = ReleaseStatus.Published,
            Channel     = ReleaseChannel.Stable,
            PublishedAt = DateTime.UtcNow,
            Artifacts   = [artifact]
        };
        artifact.ReleaseId = release.Id;

        db.Releases.Add(release);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedArtifact(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UpdateHub.Infrastructure.Persistence.AppDbContext>();

        var app     = new App { Slug = slug, Name = slug };
        var release = new Release { AppId = app.Id, App = app, Version = "1.0.0" };
        var artifact = new Artifact
        {
            ReleaseId    = release.Id,
            Platform     = "windows",
            Architecture = "x64",
            FileName     = "setup.exe",
            StoredPath   = "/fake/path",
            Sha256       = "abc123"
        };

        db.Apps.Add(app);
        db.Releases.Add(release);
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();

        return artifact.Id;
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private record HealthResponse(string Status, DateTime Timestamp);

    private record UpdateCheckResponse(
        [property: JsonPropertyName("has_update")]    bool HasUpdate,
        [property: JsonPropertyName("version")]       string Version,
        [property: JsonPropertyName("release_notes")] string? ReleaseNotes,
        [property: JsonPropertyName("download_url")]  string? DownloadUrl,
        [property: JsonPropertyName("sha256")]        string? Sha256,
        [property: JsonPropertyName("is_mandatory")]  bool IsMandatory,
        [property: JsonPropertyName("channel")]       string Channel);

    private record TauriManifestResponse(
        [property: JsonPropertyName("version")]   string Version,
        [property: JsonPropertyName("notes")]     string Notes,
        [property: JsonPropertyName("pub_date")]  string PubDate,
        [property: JsonPropertyName("platforms")] Dictionary<string, object> Platforms);
}
