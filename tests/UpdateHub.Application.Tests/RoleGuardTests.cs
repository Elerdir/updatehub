using NSubstitute;
using UpdateHub.Application.Authorization;
using UpdateHub.Application.Interfaces;
using Xunit;

namespace UpdateHub.Application.Tests;

public class RoleGuardTests
{
    [Fact]
    public void Require_Throws_WhenUnauthenticated()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(false);

        Assert.Throws<UnauthorizedAccessException>(
            () => RoleGuard.Require(user, "Admin"));
    }

    [Fact]
    public void Require_Throws_WhenAuthenticatedButMissingRole()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.IsInRole(Arg.Any<string>()).Returns(false);

        Assert.Throws<UnauthorizedAccessException>(
            () => RoleGuard.Require(user, "Admin"));
    }

    [Fact]
    public void Require_Passes_WhenUserHasOneOfTheRequestedRoles()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.IsInRole("Admin").Returns(false);
        user.IsInRole("Manager").Returns(true);

        // Should not throw
        RoleGuard.Require(user, "Admin", "Manager");
    }

    [Fact]
    public void Require_Throws_WhenAllowList_IsEmptyAndUserHasNoMatchingRole()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);

        Assert.Throws<UnauthorizedAccessException>(
            () => RoleGuard.Require(user, "Admin"));
    }
}
