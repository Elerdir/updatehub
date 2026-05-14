using NSubstitute;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using Xunit;

namespace UpdateHub.Application.Tests;

public class UpdateResolverServiceTests
{
    private readonly IReleaseRepository _releases = Substitute.For<IReleaseRepository>();
    private const string BaseUrl = "https://updates.example.com";
    private readonly UpdateResolverService _sut;

    public UpdateResolverServiceTests()
    {
        _sut = new UpdateResolverService(_releases, BaseUrl);
    }

    // ── CheckUpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CheckUpdateAsync_ReturnsNull_WhenNoRelease()
    {
        _releases.GetLatestPublishedAsync(Arg.Any<string>(), Arg.Any<ReleaseChannel>())
                 .Returns((Release?)null);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", "windows", "x64", null);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckUpdateAsync_HasUpdate_WhenServerVersionIsNewer()
    {
        var release = PublishedRelease("2.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", null, null, null);

        Assert.NotNull(result);
        Assert.True(result!.HasUpdate);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public async Task CheckUpdateAsync_NoUpdate_WhenVersionsEqual()
    {
        var release = PublishedRelease("1.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", null, null, null);

        Assert.NotNull(result);
        Assert.False(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_NoUpdate_WhenCurrentIsNewer()
    {
        var release = PublishedRelease("1.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "2.0.0", null, null, null);

        Assert.False(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_ReturnsDownloadUrl_WhenPlatformMatches()
    {
        var artifactId = Guid.NewGuid();
        var release = PublishedRelease("2.0.0", new Artifact
        {
            Id           = artifactId,
            Platform     = "windows",
            Architecture = "x64"
        });
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", "windows", "x64", null);

        Assert.Equal($"{BaseUrl}/api/downloads/{artifactId}", result!.DownloadUrl);
    }

    [Fact]
    public async Task CheckUpdateAsync_DownloadUrlNull_WhenNoPlatformMatch()
    {
        var release = PublishedRelease("2.0.0", new Artifact
        {
            Platform     = "linux",
            Architecture = "x64"
        });
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", "windows", "x64", null);

        Assert.True(result!.HasUpdate);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task CheckUpdateAsync_ParsesBetaChannel()
    {
        var release = PublishedRelease("2.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Beta).Returns(release);
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns((Release?)null);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", null, null, "beta");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CheckUpdateAsync_HandlesVPrefixedVersions()
    {
        var release = PublishedRelease("2.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "v1.0.0", null, null, null);

        Assert.True(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_StableIsNewerThanItsPreRelease()
    {
        // 1.0.0 > 1.0.0-beta.1 per semver precedence
        var release = PublishedRelease("1.0.0");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0-beta.1", null, null, null);

        Assert.True(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_NoUpdate_WhenCurrentIsNewerPreRelease()
    {
        // running 2.0.0 stable, server only has 2.0.0-rc.1 → no update
        var release = PublishedRelease("2.0.0-rc.1");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "2.0.0", null, null, null);

        Assert.False(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_OrdersPreReleaseTagsCorrectly()
    {
        // 1.2.0-beta.2 > 1.2.0-beta.10 would be true under naive string compare — verify it isn't
        var release = PublishedRelease("1.2.0-beta.10");
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.2.0-beta.2", null, null, null);

        Assert.True(result!.HasUpdate);
    }

    [Fact]
    public async Task CheckUpdateAsync_ReturnsMandatoryFlag()
    {
        var release = PublishedRelease("2.0.0");
        release.IsMandatory = true;
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var result = await _sut.CheckUpdateAsync("my-app", "1.0.0", null, null, null);

        Assert.True(result!.IsMandatory);
    }

    // ── GetTauriManifestAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTauriManifestAsync_ReturnsNull_WhenNoRelease()
    {
        _releases.GetLatestPublishedAsync(Arg.Any<string>(), Arg.Any<ReleaseChannel>())
                 .Returns((Release?)null);

        var result = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTauriManifestAsync_BuildsPlatformEntry_ForWindowsX64()
    {
        var artifactId = Guid.NewGuid();
        var release = PublishedRelease("1.1.0", new Artifact
        {
            Id           = artifactId,
            Platform     = "windows",
            Architecture = "x64",
            Signature    = "dW50cnVzdGVk"
        });
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.NotNull(manifest);
        Assert.Equal("1.1.0", manifest!.Version);
        Assert.True(manifest.Platforms.ContainsKey("windows-x86_64"));
        Assert.Equal("dW50cnVzdGVk", manifest.Platforms["windows-x86_64"].Signature);
        Assert.Equal($"{BaseUrl}/api/downloads/{artifactId}", manifest.Platforms["windows-x86_64"].Url);
    }

    [Fact]
    public async Task GetTauriManifestAsync_ExcludesArtifacts_WithoutSignature()
    {
        var release = PublishedRelease("1.0.0", new Artifact
        {
            Platform     = "windows",
            Architecture = "x64",
            Signature    = null
        });
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.NotNull(manifest);
        Assert.Empty(manifest!.Platforms);
    }

    [Theory]
    [InlineData("windows", "x64",   "windows-x86_64")]
    [InlineData("windows", "x86",   "windows-i686")]
    [InlineData("macos",   "x64",   "darwin-x86_64")]
    [InlineData("macos",   "arm64", "darwin-aarch64")]
    [InlineData("linux",   "x64",   "linux-x86_64")]
    [InlineData("linux",   "arm64", "linux-aarch64")]
    public async Task GetTauriManifestAsync_MapsAllPlatformKeys(
        string platform, string arch, string expectedKey)
    {
        var release = PublishedRelease("1.0.0", new Artifact
        {
            Platform     = platform,
            Architecture = arch,
            Signature    = "sig"
        });
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(release);

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.True(manifest!.Platforms.ContainsKey(expectedKey));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Release PublishedRelease(string version, params Artifact[] artifacts)
    {
        var app = new App { Slug = "my-app", Name = "My App" };
        var release = new Release
        {
            AppId       = app.Id,
            App         = app,
            Version     = version,
            Status      = ReleaseStatus.Published,
            Channel     = ReleaseChannel.Stable,
            PublishedAt = DateTime.UtcNow,
            Artifacts   = [.. artifacts]
        };
        foreach (var a in release.Artifacts)
            a.ReleaseId = release.Id;
        return release;
    }
}
