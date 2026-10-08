using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Infrastructure.Persistence;
using UpdateHub.Web.Endpoints;
using Xunit;

namespace UpdateHub.Web.Tests;

/// <summary>
/// The CI endpoint carries its own body/form size limits. Proves the
/// configured limit is what gets enforced (a tiny limit must reject a small
/// upload), so the raised production default actually reaches Kestrel and
/// the form reader instead of the built-in 30 MB / 128 MB caps.
/// </summary>
public class CiUploadLimitTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Token = "ci-limit-test-token";
    private readonly TestWebApplicationFactory _factory;

    public CiUploadLimitTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task Upload_LargerThanConfiguredLimit_IsRejectedBeforeStorage()
    {
        var host = HostWithLimit(1024);
        var storage = await SeedAppAsync(host, "limit-small");

        var response = await PostAsync(host.CreateClient(), "limit-small", new byte[8 * 1024]);

        Assert.False(response.IsSuccessStatusCode, $"Expected rejection, got {(int)response.StatusCode}");
        await storage.DidNotReceiveWithAnyArgs().StoreAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Upload_WithinConfiguredLimit_IsStored()
    {
        var host = HostWithLimit(CiEndpoints.DefaultMaxUploadBytes);
        var storage = await SeedAppAsync(host, "limit-ok");

        var response = await PostAsync(host.CreateClient(), "limit-ok", new byte[8 * 1024]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await storage.ReceivedWithAnyArgs(1).StoreAsync(default!, default!, default!, default!);
    }

    private WebApplicationFactory<Program> HostWithLimit(long bytes) =>
        _factory.WithWebHostBuilder(b => b.UseSetting(CiEndpoints.MaxUploadBytesKey, bytes.ToString()));

    private static async Task<IArtifactStorage> SeedAppAsync(WebApplicationFactory<Program> host, string slug)
    {
        var storage = host.Services.GetRequiredService<IArtifactStorage>();
        storage.StoreAsync(default!, default!, default!, default!)
               .ReturnsForAnyArgs(("stored/path", "sha256", 8 * 1024L));

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Apps.Any(a => a.Slug == slug))
        {
            db.Apps.Add(new App { Slug = slug, Name = slug, CiToken = Token });
            await db.SaveChangesAsync();
        }
        return storage;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string slug, byte[] payload)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(payload);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "app-setup.exe");
        form.Add(new StringContent("1.0.0"), "version");
        form.Add(new StringContent("windows"), "platform");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/ci/apps/{slug}/releases") { Content = form };
        request.Headers.Add("X-UpdateHub-Token", Token);
        return await client.SendAsync(request);
    }
}
