using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using Xunit;

namespace UpdateHub.Application.Tests;

/// <summary>
/// Pure-logic tests for the stepping-stone upgrade-path picker. We exercise
/// <see cref="UpdateResolverService.PickBestForVersion"/> directly so we don't
/// need a repository fake — it's a static function over a release list.
/// </summary>
public class UpgradePathTests
{
    [Fact]
    public void MeetsMinimum_TreatsNullAsAlways_Eligible()
    {
        Assert.True(UpdateResolverService.MeetsMinimum("0.0.1", null));
        Assert.True(UpdateResolverService.MeetsMinimum("0.0.1", ""));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]   // equal
    [InlineData("2.0.0", "1.0.0", true)]   // newer
    [InlineData("1.0.0", "2.0.0", false)]  // older
    [InlineData("v1.4.0", "1.0.0", true)]  // v-prefix tolerated
    [InlineData("1.4.0-rc.1", "1.4.0", false)] // pre-release < release
    public void MeetsMinimum_RespectsSemverPrecedence(string current, string min, bool expected)
    {
        Assert.Equal(expected, UpdateResolverService.MeetsMinimum(current, min));
    }

    [Fact]
    public void PickBest_Returns_LatestWhenNoConstraints()
    {
        var releases = new[]
        {
            Pub("1.0.0"),
            Pub("2.0.0"),
            Pub("3.0.0"),
        };

        var pick = UpdateResolverService.PickBestForVersion(releases, "1.0.0");

        Assert.NotNull(pick);
        Assert.Equal("3.0.0", pick!.Version);
    }

    [Fact]
    public void PickBest_Returns_HighestEligibleSteppingStone()
    {
        // User on 1.0. Latest is 6.0 but requires 5.0+. 5.0 requires 4.0+.
        // 4.0 has no constraint, so that's the highest the user can install.
        var releases = new[]
        {
            Pub("6.0.0", minFrom: "5.0.0"),
            Pub("5.0.0", minFrom: "4.0.0"),
            Pub("4.0.0"),
            Pub("3.0.0"),
            Pub("2.0.0"),
        };

        var pick = UpdateResolverService.PickBestForVersion(releases, "1.0.0");

        Assert.Equal("4.0.0", pick!.Version);
    }

    [Fact]
    public void PickBest_Progresses_OnSubsequentChecks()
    {
        var releases = new[]
        {
            Pub("6.0.0", minFrom: "5.0.0"),
            Pub("5.0.0", minFrom: "4.0.0"),
            Pub("4.0.0"),
        };

        // Step 1: 1.0 → 4.0 (highest unconstrained)
        Assert.Equal("4.0.0", UpdateResolverService.PickBestForVersion(releases, "1.0.0")!.Version);
        // Step 2: 4.0 → 5.0 (now meets 4.0 minimum)
        Assert.Equal("5.0.0", UpdateResolverService.PickBestForVersion(releases, "4.0.0")!.Version);
        // Step 3: 5.0 → 6.0 (now meets 5.0 minimum)
        Assert.Equal("6.0.0", UpdateResolverService.PickBestForVersion(releases, "5.0.0")!.Version);
    }

    [Fact]
    public void PickBest_Returns_Null_WhenClientIsAlreadyOnLatest()
    {
        var releases = new[] { Pub("3.0.0"), Pub("2.0.0") };

        var pick = UpdateResolverService.PickBestForVersion(releases, "3.0.0");

        Assert.Null(pick);
    }

    [Fact]
    public void PickBest_Returns_Null_WhenClientIsAhead()
    {
        var releases = new[] { Pub("2.0.0"), Pub("1.0.0") };

        var pick = UpdateResolverService.PickBestForVersion(releases, "5.0.0");

        Assert.Null(pick);
    }

    [Fact]
    public void PickBest_Skips_Constrained_PicksLowerUnconstrained()
    {
        // 3.0 requires 2.5+; user on 2.0 can't get 3.0 — gets 2.5 if any, else 2.1.
        var releases = new[]
        {
            Pub("3.0.0", minFrom: "2.5.0"),
            Pub("2.1.0"),
        };

        var pick = UpdateResolverService.PickBestForVersion(releases, "2.0.0");

        Assert.Equal("2.1.0", pick!.Version);
    }

    [Fact]
    public void PickBest_OrdersBySemver_NotByListOrder()
    {
        // Repository might hand them back in publish-date order (1.9 hotfix
        // published after 2.0). The picker must still treat 2.0 as the latest.
        var releases = new[]
        {
            Pub("1.9.1"), // published latest (hotfix)
            Pub("2.0.0"),
            Pub("1.9.0"),
        };

        var pick = UpdateResolverService.PickBestForVersion(releases, "1.0.0");

        Assert.Equal("2.0.0", pick!.Version);
    }

    private static Release Pub(string version, string? minFrom = null) =>
        new()
        {
            Version        = version,
            Status         = ReleaseStatus.Published,
            Channel        = ReleaseChannel.Stable,
            MinFromVersion = minFrom,
            App            = new App { Slug = "test-app", Name = "Test" },
        };
}
