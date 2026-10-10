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

        // The resolver now fetches the full published list to compute the best
        // stepping-stone. Existing tests only mock GetLatestPublishedAsync, so
        // we mirror that single value into the all-published call.
        _releases.GetAllPublishedAsync(Arg.Any<string>(), Arg.Any<ReleaseChannel>())
            .Returns(ci =>
            {
                var slug = ci.Arg<string>();
                var ch   = ci.Arg<ReleaseChannel>();
                var r    = _releases.GetLatestPublishedAsync(slug, ch).GetAwaiter().GetResult();
                return r is null ? [] : new List<Release> { r };
            });
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

    [Fact]
    public async Task GetTauriManifestAsync_PrefersNsisOverMsi_RegardlessOfOrder()
    {
        var nsis = new Artifact { Platform = "windows", Architecture = "x64", FileName = "App_1.1.0_x64-setup.exe", Signature = "nsis-sig", CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var msi  = new Artifact { Platform = "windows", Architecture = "x64", FileName = "App_1.1.0_x64_en-US.msi", Signature = "msi-sig", CreatedAt = DateTime.UtcNow };
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(PublishedRelease("1.1.0", nsis, msi));

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        var entry = manifest!.Platforms["windows-x86_64"];
        Assert.Equal("nsis-sig", entry.Signature);
        Assert.Equal($"{BaseUrl}/api/downloads/{nsis.Id}", entry.Url);
    }

    [Fact]
    public async Task GetTauriManifestAsync_FallsBackToMsi_WhenOnlyMsiIsSigned()
    {
        var nsis = new Artifact { Platform = "windows", Architecture = "x64", FileName = "App-setup.exe", Signature = null };
        var msi  = new Artifact { Platform = "windows", Architecture = "x64", FileName = "App.msi", Signature = "msi-sig" };
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(PublishedRelease("1.1.0", nsis, msi));

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.Equal("msi-sig", manifest!.Platforms["windows-x86_64"].Signature);
    }

    [Fact]
    public async Task GetTauriManifestAsync_SameFormat_PicksNewestUpload()
    {
        var older = new Artifact { Platform = "macos", Architecture = "arm64", FileName = "App.app.tar.gz", Signature = "old", CreatedAt = DateTime.UtcNow.AddHours(-1) };
        var newer = new Artifact { Platform = "macos", Architecture = "arm64", FileName = "App.app.tar.gz", Signature = "new", CreatedAt = DateTime.UtcNow };
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Stable).Returns(PublishedRelease("1.1.0", newer, older));

        var manifest = await _sut.GetTauriManifestAsync("my-app", null);

        Assert.Equal("new", manifest!.Platforms["darwin-aarch64"].Signature);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // ── GetLatestReleaseAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestReleaseAsync_ReturnsNull_WhenNothingPublished()
    {
        _releases.GetLatestPublishedAsync(Arg.Any<string>(), Arg.Any<ReleaseChannel>())
                 .Returns((Release?)null);

        Assert.Null(await _sut.GetLatestReleaseAsync("my-app", null));
    }

    [Fact]
    public async Task GetLatestReleaseAsync_ListsAllArtifactsSortedWithDownloadUrls()
    {
        var macArm = new Artifact { Platform = "macos",   Architecture = "arm64", FileName = "App_aarch64.dmg", FileSizeBytes = 20, Sha256 = "b" };
        var winExe = new Artifact { Platform = "windows", Architecture = "x64",   FileName = "App_x64-setup.exe", FileSizeBytes = 10, Sha256 = "a" };
        var macX64 = new Artifact { Platform = "macos",   Architecture = "x64",   FileName = "App_x64.dmg", FileSizeBytes = 30, Sha256 = "c" };
        var release = PublishedRelease("1.2.0", winExe, macX64, macArm);
        release.ReleaseNotes = "Notes";
        _releases.GetLatestPublishedAsync("my-app", ReleaseChannel.Beta).Returns(release);

        var latest = await _sut.GetLatestReleaseAsync("my-app", "beta");

        Assert.NotNull(latest);
        Assert.Equal("My App", latest!.Name);
        Assert.Equal("1.2.0", latest.Version);
        Assert.Equal("Notes", latest.ReleaseNotes);
        Assert.Equal(["App_aarch64.dmg", "App_x64.dmg", "App_x64-setup.exe"], latest.Artifacts.Select(a => a.FileName));
        Assert.Equal($"{BaseUrl}/api/downloads/{winExe.Id}", latest.Artifacts[2].Url);
        Assert.Equal(10, latest.Artifacts[2].SizeBytes);
    }

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
