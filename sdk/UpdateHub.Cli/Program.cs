// updatehub — a minimal CLI for uploading release artifacts to an UpdateHub server.
//
// Usage:
//   updatehub upload <slug> <version> <file> [--platform windows] [--arch x64]
//                                            [--channel stable] [--notes "…"]
//                                            [--signature <sig>]
//
// Auth via env vars:
//   UPDATEHUB_URL    — e.g. https://updates.example.com
//   UPDATEHUB_TOKEN  — CI token (global or per-app) or a personal access token
//                      (passed as either X-UpdateHub-Token or Authorization: Bearer)
//
// Exit codes: 0 ok, 1 usage error, 2 server / network error.

using System.Net.Http.Headers;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 0;
}

return args[0] switch
{
    "upload"  => await Upload(args[1..]),
    "version" => PrintVersion(),
    _         => Usage($"Unknown command: {args[0]}")
};

static int Usage(string msg)
{
    Console.Error.WriteLine(msg);
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        updatehub — upload artifacts to an UpdateHub server.

        Usage:
          updatehub upload <slug> <version> <file> [options]

        Required env:
          UPDATEHUB_URL    base URL of the server (e.g. https://updates.example.com)
          UPDATEHUB_TOKEN  CI token or personal access token

        Options:
          --platform <p>     windows | macos | linux  (default: windows)
          --arch <a>         x64 | arm64 | x86        (default: x64)
          --channel <c>      stable | beta | alpha    (default: stable)
          --notes <text>     release notes (markdown)
          --signature <sig>  Tauri Ed25519 signature
          --mandatory        mark release as mandatory
        """);
}

static int PrintVersion()
{
    Console.WriteLine("updatehub 1.0.0");
    return 0;
}

static async Task<int> Upload(string[] args)
{
    if (args.Length < 3)
        return Usage("upload requires <slug> <version> <file>");

    var slug    = args[0];
    var version = args[1];
    var path    = args[2];

    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    var server = Env("UPDATEHUB_URL")  ?? "";
    var token  = Env("UPDATEHUB_TOKEN") ?? "";
    if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(token))
    {
        Console.Error.WriteLine("UPDATEHUB_URL and UPDATEHUB_TOKEN must be set.");
        return 1;
    }

    var (platform, arch, channel, notes, signature, mandatory) = ParseOptions(args[3..]);

    using var client = new HttpClient { BaseAddress = new Uri(server.TrimEnd('/') + "/") };
    client.DefaultRequestHeaders.Add("X-UpdateHub-Token", token);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    using var form = new MultipartFormDataContent
    {
        { new StringContent(version),  "version" },
        { new StringContent(platform), "platform" },
        { new StringContent(arch),     "arch" },
        { new StringContent(channel),  "channel" },
        { new StringContent(notes ?? ""),     "release_notes" },
        { new StringContent(signature ?? ""), "signature" },
        { new StringContent(mandatory ? "true" : "false"), "is_mandatory" },
    };

    await using var file = File.OpenRead(path);
    form.Add(new StreamContent(file), "file", Path.GetFileName(path));

    try
    {
        Console.WriteLine($"Uploading {Path.GetFileName(path)} → {server}/api/ci/apps/{slug}/releases …");
        var resp = await client.PostAsync($"api/ci/apps/{Uri.EscapeDataString(slug)}/releases", form);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"HTTP {(int)resp.StatusCode}: {body}");
            return 2;
        }
        Console.WriteLine(body);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Network error: {ex.Message}");
        return 2;
    }
}

static (string platform, string arch, string channel, string? notes, string? signature, bool mandatory)
    ParseOptions(string[] args)
{
    var platform  = "windows";
    var arch      = "x64";
    var channel   = "stable";
    string? notes = null;
    string? sig   = null;
    var mandatory = false;

    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        var v = i + 1 < args.Length ? args[i + 1] : "";
        switch (a)
        {
            case "--platform":  platform  = v; i++; break;
            case "--arch":      arch      = v; i++; break;
            case "--channel":   channel   = v; i++; break;
            case "--notes":     notes     = v; i++; break;
            case "--signature": sig       = v; i++; break;
            case "--mandatory": mandatory = true; break;
        }
    }
    return (platform, arch, channel, notes, sig, mandatory);
}

static string? Env(string name) => Environment.GetEnvironmentVariable(name);
