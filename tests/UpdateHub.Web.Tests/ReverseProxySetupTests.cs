using System.Net;
using UpdateHub.Web.Startup;
using Xunit;

namespace UpdateHub.Web.Tests;

public class ReverseProxySetupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseNetworks_Empty_FallsBackToDefaults(string? value)
    {
        var networks = ReverseProxySetup.ParseNetworks(value);

        Assert.Equal(ReverseProxySetup.DefaultTrustedNetworks.Count, networks.Count);
    }

    [Fact]
    public void ParseNetworks_Defaults_TrustDockerBridgeButNotHomeLan()
    {
        var networks = ReverseProxySetup.ParseNetworks(null);

        Assert.Contains(networks, n => n.Contains(IPAddress.Parse("172.17.0.1")));
        Assert.Contains(networks, n => n.Contains(IPAddress.Loopback));
        Assert.DoesNotContain(networks, n => n.Contains(IPAddress.Parse("192.168.1.50")));
    }

    [Fact]
    public void ParseNetworks_CustomList_SupportsCommaAndSemicolon()
    {
        var networks = ReverseProxySetup.ParseNetworks(" 10.0.0.0/8 ; 192.168.1.10/32, fd00::/8 ");

        Assert.Equal(3, networks.Count);
        Assert.Contains(networks, n => n.Contains(IPAddress.Parse("192.168.1.10")));
        Assert.DoesNotContain(networks, n => n.Contains(IPAddress.Parse("172.17.0.1")));
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("10.0.0.0/8, 300.1.1.1/32")]
    public void ParseNetworks_InvalidEntry_Throws(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ReverseProxySetup.ParseNetworks(value));
        Assert.Contains(ReverseProxySetup.ConfigKey, ex.Message);
    }
}
