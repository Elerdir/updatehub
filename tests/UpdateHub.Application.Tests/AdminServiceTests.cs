using NSubstitute;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using Xunit;

namespace UpdateHub.Application.Tests;

public class AdminServiceTests
{
    private readonly IAppRepository      _apps      = Substitute.For<IAppRepository>();
    private readonly IReleaseRepository  _releases  = Substitute.For<IReleaseRepository>();
    private readonly IArtifactRepository _artifacts = Substitute.For<IArtifactRepository>();
    private readonly IArtifactStorage    _storage   = Substitute.For<IArtifactStorage>();
    private readonly IWebhookService     _webhook   = Substitute.For<IWebhookService>();
    private readonly IAuditRepository    _auditRepo = Substitute.For<IAuditRepository>();
    private readonly IEmailService       _emailSvc  = Substitute.For<IEmailService>();
    private readonly INotificationQueue  _queue     = Substitute.For<INotificationQueue>();
    private readonly ICurrentUser        _currentUser = Substitute.For<ICurrentUser>();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        // Tests cover business logic, not authorization — give the substitute
        // enough rope to pass every RoleGuard check.
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.IsInRole(Arg.Any<string>()).Returns(true);

        var audit = new AuditService(_auditRepo);
        var email = new EmailNotificationService(_emailSvc);
        _sut = new AdminService(_apps, _releases, _artifacts, _storage, _webhook,
                                audit, email, _queue, _currentUser);
    }

    // ── CreateAppAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAppAsync_NormalisesSlugAndName()
    {
        App? captured = null;
        _apps.CreateAsync(Arg.Do<App>(a => captured = a))
             .Returns(ci => ci.Arg<App>());

        await _sut.CreateAppAsync("  My-App  ", "  My App  ", null);

        Assert.NotNull(captured);
        Assert.Equal("my-app", captured!.Slug);
        Assert.Equal("My App", captured.Name);
    }

    [Fact]
    public async Task CreateAppAsync_TrimsDescription()
    {
        App? captured = null;
        _apps.CreateAsync(Arg.Do<App>(a => captured = a))
             .Returns(ci => ci.Arg<App>());

        await _sut.CreateAppAsync("slug", "Name", "  desc  ");

        Assert.Equal("desc", captured!.Description);
    }

    [Fact]
    public async Task CreateAppAsync_NullDescriptionStaysNull()
    {
        App? captured = null;
        _apps.CreateAsync(Arg.Do<App>(a => captured = a))
             .Returns(ci => ci.Arg<App>());

        await _sut.CreateAppAsync("slug", "Name", null);

        Assert.Null(captured!.Description);
    }

    // ── CreateReleaseAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReleaseAsync_SetsAllFields()
    {
        var appId = Guid.NewGuid();
        Release? captured = null;
        _releases.CreateAsync(Arg.Do<Release>(r => captured = r))
                 .Returns(ci => ci.Arg<Release>());

        await _sut.CreateReleaseAsync(appId, " 1.2.3 ", ReleaseChannel.Beta, "notes", true);

        Assert.NotNull(captured);
        Assert.Equal(appId, captured!.AppId);
        Assert.Equal("1.2.3", captured.Version);
        Assert.Equal(ReleaseChannel.Beta, captured.Channel);
        Assert.Equal("notes", captured.ReleaseNotes);
        Assert.True(captured.IsMandatory);
    }

    [Fact]
    public async Task CreateReleaseAsync_WhitespaceNotesBecomesNull()
    {
        Release? captured = null;
        _releases.CreateAsync(Arg.Do<Release>(r => captured = r))
                 .Returns(ci => ci.Arg<Release>());

        await _sut.CreateReleaseAsync(Guid.NewGuid(), "1.0.0", ReleaseChannel.Stable, "   ", false);

        Assert.Null(captured!.ReleaseNotes);
    }

    // ── PublishReleaseAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task PublishReleaseAsync_SetsStatusAndPublishedAt()
    {
        var release = new Release { Version = "1.0.0", App = new App { Slug = "my-app" } };
        _releases.GetByIdAsync(release.Id).Returns(release);
        _releases.GetPublishedByAppChannelAsync(Arg.Any<Guid>(), Arg.Any<ReleaseChannel>(), Arg.Any<Guid>())
                 .Returns([]);

        await _sut.PublishReleaseAsync(release.Id);

        Assert.Equal(ReleaseStatus.Published, release.Status);
        Assert.NotNull(release.PublishedAt);
        await _releases.Received(1).UpdateAsync(release);
    }

    [Fact]
    public async Task PublishReleaseAsync_ArchivesOlderRelease()
    {
        var old     = new Release { Status = ReleaseStatus.Published, Channel = ReleaseChannel.Stable };
        var current = new Release { Channel = ReleaseChannel.Stable, App = new App { Slug = "my-app" } };
        _releases.GetByIdAsync(current.Id).Returns(current);
        _releases.GetPublishedByAppChannelAsync(Arg.Any<Guid>(), Arg.Any<ReleaseChannel>(), current.Id)
                 .Returns([old]);

        await _sut.PublishReleaseAsync(current.Id);

        Assert.Equal(ReleaseStatus.Archived, old.Status);
        Assert.Equal(ReleaseStatus.Published, current.Status);
    }

    [Fact]
    public async Task PublishReleaseAsync_ThrowsWhenReleaseNotFound()
    {
        _releases.GetByIdAsync(Arg.Any<Guid>()).Returns((Release?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.PublishReleaseAsync(Guid.NewGuid()));
    }

    // ── ArchiveReleaseAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveReleaseAsync_SetsStatusArchived()
    {
        var release = new Release { Status = ReleaseStatus.Published };
        _releases.GetByIdAsync(release.Id).Returns(release);

        await _sut.ArchiveReleaseAsync(release.Id);

        Assert.Equal(ReleaseStatus.Archived, release.Status);
        await _releases.Received(1).UpdateAsync(release);
    }

    // ── DeleteReleaseAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteReleaseAsync_CallsDeleteOnRepo()
    {
        var release = new Release();
        _releases.GetByIdAsync(release.Id).Returns(release);

        await _sut.DeleteReleaseAsync(release.Id);

        await _releases.Received(1).DeleteAsync(release);
    }

    [Fact]
    public async Task DeleteReleaseAsync_DoesNothingWhenNotFound()
    {
        _releases.GetByIdAsync(Arg.Any<Guid>()).Returns((Release?)null);

        await _sut.DeleteReleaseAsync(Guid.NewGuid()); // no exception

        await _releases.DidNotReceive().DeleteAsync(Arg.Any<Release>());
    }

    // ── GetStatsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_CountsAppsAndPublishedReleases()
    {
        var apps = new List<App>
        {
            new()
            {
                Releases =
                [
                    new() { Status = ReleaseStatus.Published },
                    new() { Status = ReleaseStatus.Draft }
                ]
            },
            new()
            {
                Releases =
                [
                    new() { Status = ReleaseStatus.Published }
                ]
            }
        };
        _apps.GetAllWithReleasesAsync().Returns(apps);

        var (appCount, published, downloads) = await _sut.GetStatsAsync();

        Assert.Equal(2, appCount);
        Assert.Equal(2, published);
        Assert.Equal(0, downloads);
    }

    // ── AddArtifactAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddArtifactAsync_StoresFileAndCreatesArtifact()
    {
        var release = new Release
        {
            Version = "1.0.0",
            App = new App { Slug = "my-app" }
        };
        _releases.GetByIdAsync(release.Id).Returns(release);
        _storage.StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(("/stored/path", "abc123", 1024L));

        Artifact? captured = null;
        _artifacts.CreateAsync(Arg.Do<Artifact>(a => captured = a))
                  .Returns(ci => ci.Arg<Artifact>());

        using var stream = new MemoryStream([1, 2, 3]);
        await _sut.AddArtifactAsync(release.Id, "Windows", "X64", stream, "setup.exe", "sig==");

        Assert.NotNull(captured);
        Assert.Equal("windows", captured!.Platform);
        Assert.Equal("x64", captured.Architecture);
        Assert.Equal("setup.exe", captured.FileName);
        Assert.Equal("/stored/path", captured.StoredPath);
        Assert.Equal("abc123", captured.Sha256);
        Assert.Equal("sig==", captured.Signature);
    }

    [Fact]
    public async Task AddArtifactAsync_ThrowsWhenReleaseNotFound()
    {
        _releases.GetByIdAsync(Arg.Any<Guid>()).Returns((Release?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddArtifactAsync(Guid.NewGuid(), "windows", "x64",
                                        Stream.Null, "file.exe", null));
    }

    // ── DeleteArtifactAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArtifactAsync_DeletesFileAndRecord()
    {
        var artifact = new Artifact { StoredPath = "/path/to/file" };
        _artifacts.GetByIdAsync(artifact.Id).Returns(artifact);

        await _sut.DeleteArtifactAsync(artifact.Id);

        _storage.Received(1).Delete("/path/to/file");
        await _artifacts.Received(1).DeleteAsync(artifact);
    }

    [Fact]
    public async Task DeleteArtifactAsync_DoesNothingWhenNotFound()
    {
        _artifacts.GetByIdAsync(Arg.Any<Guid>()).Returns((Artifact?)null);

        await _sut.DeleteArtifactAsync(Guid.NewGuid());

        _storage.DidNotReceive().Delete(Arg.Any<string>());
    }
}
