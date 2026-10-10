using NSubstitute;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using Xunit;

namespace UpdateHub.Application.Tests;

public class UserServiceTests
{
    private readonly IUserRepository                _users        = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository  _resetTokens  = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPersonalAccessTokenRepository _pats         = Substitute.For<IPersonalAccessTokenRepository>();
    private readonly ISecretProtector               _protector    = Substitute.For<ISecretProtector>();
    private readonly IAuditRepository               _auditRepo    = Substitute.For<IAuditRepository>();
    private readonly INotificationQueue             _queue        = Substitute.For<INotificationQueue>();
    private readonly ICurrentUser                   _current      = Substitute.For<ICurrentUser>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _current.IsAuthenticated.Returns(true);
        _current.IsInRole(Arg.Any<string>()).Returns(true);
        // ISecretProtector — round-trip plaintext for tests
        _protector.Protect(Arg.Any<string>()).Returns(ci => "enc:" + ci.Arg<string>());
        _protector.Unprotect(Arg.Any<string>()).Returns(ci =>
        {
            var s = ci.Arg<string>();
            return s.StartsWith("enc:") ? s[4..] : s;
        });

        var audit = new AuditService(_auditRepo);
        _sut = new UserService(_users, _resetTokens, _pats, _protector, audit, _queue, _current);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Persists_NewUser_WithMustChangePassword()
    {
        User? captured = null;
        _users.CreateAsync(Arg.Do<User>(u => captured = u)).Returns(ci => ci.Arg<User>());

        await _sut.CreateAsync("alice", null, "tempPass1", UserRole.Manager, Guid.NewGuid(), "admin");

        Assert.NotNull(captured);
        Assert.Equal("alice", captured!.Username);
        Assert.True(captured.MustChangePassword);
        Assert.True(captured.IsActive);
        Assert.Equal(UserRole.Manager, captured.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify("tempPass1", captured.PasswordHash));
    }

    [Fact]
    public async Task CreateAsync_Throws_OnDuplicateUsername()
    {
        _users.GetByUsernameAsync("dupe").Returns(new User { Username = "dupe" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateAsync("dupe", null, "longenough", UserRole.Viewer, Guid.NewGuid(), "admin"));
    }

    [Fact]
    public async Task CreateAsync_Throws_OnTooShortPassword()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateAsync("alice", null, "short", UserRole.Viewer, Guid.NewGuid(), "admin"));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNotAdmin()
    {
        _current.IsInRole(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.CreateAsync("alice", null, "longenough", UserRole.Viewer, Guid.NewGuid(), "admin"));
    }

    // ── SetActiveAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetActiveAsync_Refuses_DeactivatingSelf()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        _users.GetByIdAsync(me).Returns(new User { Id = me, Username = "admin", IsActive = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.SetActiveAsync(me, false, "admin"));
    }

    [Fact]
    public async Task SetRoleAsync_Refuses_ChangingOwnRole()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        _users.GetByIdAsync(me).Returns(new User { Id = me, Role = UserRole.Admin });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.SetRoleAsync(me, UserRole.Viewer, "admin"));
    }

    // ── ChangeOwnPasswordAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ChangeOwnPasswordAsync_RotatesSecurityStamp_AndClearsMustChange()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        var original = new User
        {
            Id                 = me,
            Username           = "alice",
            PasswordHash       = BCrypt.Net.BCrypt.HashPassword("oldPassw0rd"),
            MustChangePassword = true,
            SecurityStamp      = "stamp-A",
        };
        _users.GetByIdAsync(me).Returns(original);

        await _sut.ChangeOwnPasswordAsync(me, "oldPassw0rd", "newPassw0rd");

        Assert.False(original.MustChangePassword);
        Assert.NotEqual("stamp-A", original.SecurityStamp);
        Assert.True(BCrypt.Net.BCrypt.Verify("newPassw0rd", original.PasswordHash));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_Refuses_WrongCurrent()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        _users.GetByIdAsync(me).Returns(new User
        {
            Id = me, PasswordHash = BCrypt.Net.BCrypt.HashPassword("realPassw0rd"),
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ChangeOwnPasswordAsync(me, "wrongPassw0rd", "newPassw0rd"));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_Refuses_DifferentUser()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        _current.Id.Returns(me);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ChangeOwnPasswordAsync(other, "pw1", "newPassw0rd"));
    }

    // ── Forgot-password flow ──────────────────────────────────────────────────

    [Fact]
    public async Task InitiatePasswordReset_Returns_TokenAndPersists()
    {
        var user = new User
        {
            Id       = Guid.NewGuid(),
            Username = "alice",
            Email    = "alice@example.com",
            IsActive = true,
        };
        _users.GetByUsernameAsync("alice").Returns(user);

        PasswordResetToken? captured = null;
        _resetTokens.CreateAsync(Arg.Do<PasswordResetToken>(t => captured = t)).Returns(Task.CompletedTask);

        var raw = await _sut.InitiatePasswordResetAsync("alice");

        Assert.NotNull(raw);
        Assert.NotNull(captured);
        Assert.Equal(user.Id, captured!.UserId);
        Assert.True(captured.ExpiresAt > DateTime.UtcNow);
        // Hash stored must NOT equal the raw token
        Assert.NotEqual(raw, captured.TokenHash);
        Assert.Equal(64, captured.TokenHash.Length); // SHA-256 hex
    }

    [Fact]
    public async Task InitiatePasswordReset_Returns_Null_ForUnknownUser()
    {
        _users.GetByUsernameAsync(Arg.Any<string>()).Returns((User?)null);
        _users.GetAllAsync().Returns([]);

        var raw = await _sut.InitiatePasswordResetAsync("ghost");

        Assert.Null(raw);
    }

    [Fact]
    public async Task InitiatePasswordReset_Returns_Null_WhenUserHasNoEmail()
    {
        // We can't send the link, so we don't issue a token.
        _users.GetByUsernameAsync("alice").Returns(new User
        {
            Id = Guid.NewGuid(), Username = "alice", IsActive = true, Email = null,
        });

        var raw = await _sut.InitiatePasswordResetAsync("alice");

        Assert.Null(raw);
        await _resetTokens.DidNotReceive().CreateAsync(Arg.Any<PasswordResetToken>());
    }

    [Fact]
    public async Task ConsumePasswordReset_Throws_OnExpiredToken()
    {
        var user = new User { Id = Guid.NewGuid(), IsActive = true };
        var hash = HashToken("raw");
        _resetTokens.GetByHashAsync(hash).Returns(new PasswordResetToken
        {
            UserId    = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        _users.GetByIdAsync(user.Id).Returns(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConsumePasswordResetAsync("raw", "newPassw0rd"));
    }

    [Fact]
    public async Task ConsumePasswordReset_Throws_OnAlreadyUsedToken()
    {
        var user = new User { Id = Guid.NewGuid(), IsActive = true };
        var hash = HashToken("raw");
        _resetTokens.GetByHashAsync(hash).Returns(new PasswordResetToken
        {
            UserId    = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt    = DateTime.UtcNow.AddSeconds(-30),
        });
        _users.GetByIdAsync(user.Id).Returns(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConsumePasswordResetAsync("raw", "newPassw0rd"));
    }

    [Fact]
    public async Task ConsumePasswordReset_MarksTokenUsed_AndRotatesStamp()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            IsActive     = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldPassw0rd"),
            SecurityStamp = "stamp-A",
        };
        var hash  = HashToken("raw");
        var token = new PasswordResetToken
        {
            UserId    = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };
        _resetTokens.GetByHashAsync(hash).Returns(token);
        _users.GetByIdAsync(user.Id).Returns(user);

        await _sut.ConsumePasswordResetAsync("raw", "newPassw0rd");

        Assert.NotNull(token.UsedAt);
        Assert.NotEqual("stamp-A", user.SecurityStamp);
        Assert.True(BCrypt.Net.BCrypt.Verify("newPassw0rd", user.PasswordHash));
    }

    // ── Personal access tokens ────────────────────────────────────────────────

    [Fact]
    public async Task CreatePersonalAccessToken_Returns_RawValue_Once()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        _users.GetByIdAsync(me).Returns(new User { Id = me, Username = "alice" });

        PersonalAccessToken? stored = null;
        _pats.CreateAsync(Arg.Do<PersonalAccessToken>(t => stored = t)).Returns(Task.CompletedTask);

        var raw = await _sut.CreatePersonalAccessTokenAsync(me, "my-laptop", expiresInDays: 30);

        Assert.NotEmpty(raw);
        Assert.NotNull(stored);
        Assert.Equal(me, stored!.UserId);
        Assert.Equal("my-laptop", stored.Name);
        Assert.Equal(raw[..8], stored.Prefix);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task VerifyPersonalAccessToken_Rejects_RevokedToken()
    {
        var user  = new User { Id = Guid.NewGuid(), IsActive = true };
        var token = new PersonalAccessToken
        {
            User      = user, UserId = user.Id,
            TokenHash = HashToken("the-token"),
            RevokedAt = DateTime.UtcNow,
        };
        _pats.GetByHashAsync(token.TokenHash).Returns(token);

        var result = await _sut.VerifyPersonalAccessTokenAsync("the-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyPersonalAccessToken_Rejects_ExpiredToken()
    {
        var user  = new User { Id = Guid.NewGuid(), IsActive = true };
        var token = new PersonalAccessToken
        {
            User      = user, UserId = user.Id,
            TokenHash = HashToken("the-token"),
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1),
        };
        _pats.GetByHashAsync(token.TokenHash).Returns(token);

        var result = await _sut.VerifyPersonalAccessTokenAsync("the-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyPersonalAccessToken_Rejects_DisabledOwner()
    {
        var user  = new User { Id = Guid.NewGuid(), IsActive = false };
        var token = new PersonalAccessToken
        {
            User = user, UserId = user.Id, TokenHash = HashToken("the-token"),
        };
        _pats.GetByHashAsync(token.TokenHash).Returns(token);

        var result = await _sut.VerifyPersonalAccessTokenAsync("the-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyPersonalAccessToken_Returns_User_AndUpdatesLastUsed()
    {
        var user  = new User { Id = Guid.NewGuid(), Username = "alice", IsActive = true };
        var token = new PersonalAccessToken
        {
            User = user, UserId = user.Id, TokenHash = HashToken("the-token"),
        };
        _pats.GetByHashAsync(token.TokenHash).Returns(token);

        var result = await _sut.VerifyPersonalAccessTokenAsync("the-token");

        Assert.Equal(user.Id, result?.Id);
        Assert.NotNull(token.LastUsedAt);
    }

    // ── RevokeAllSessionsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAllSessions_RotatesStamp_ForOwnAccount()
    {
        var me = Guid.NewGuid();
        _current.Id.Returns(me);
        var user = new User { Id = me, SecurityStamp = "stamp-A" };
        _users.GetByIdAsync(me).Returns(user);

        await _sut.RevokeAllSessionsAsync(me);

        Assert.NotEqual("stamp-A", user.SecurityStamp);
    }

    [Fact]
    public async Task RevokeAllSessions_Throws_ForDifferentUser()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        _current.Id.Returns(me);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.RevokeAllSessionsAsync(other));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashToken(string raw) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256
            .HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
