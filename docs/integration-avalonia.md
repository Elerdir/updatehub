# Integrating UpdateHub into Avalonia / .NET apps

Applies to: **AIStudio**, **CineDeckPlayer**, **StoryForge** (any .NET 8+ app).

## 1. Add the SDK

Reference the UpdateHub.Client project directly (until NuGet package is published):

```xml
<!-- In your .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/updatehub/sdk/UpdateHub.Client/UpdateHub.Client.csproj" />
</ItemGroup>
```

Or copy the four `.cs` files from `sdk/UpdateHub.Client/` into your project.

## 2. Check for updates on startup

```csharp
using UpdateHub.Client;

public class UpdateService
{
    private const string ServerUrl = "https://your-updatehub-server.com";
    private const string AppSlug   = "ai-studio"; // as registered in UpdateHub

    public async Task<UpdateCheckResult?> CheckAsync(string currentVersion)
    {
        using var client = new UpdateHubClient(ServerUrl, AppSlug);
        try
        {
            var result = await client.CheckForUpdateAsync(currentVersion);
            return result.HasUpdate ? result : null;
        }
        catch (UpdateHubException)
        {
            // Server unreachable — silently skip, never block app startup
            return null;
        }
    }
}
```

## 3. Show the update dialog (Avalonia)

```csharp
// In your main window / app startup
var updateService = new UpdateService();
var update = await updateService.CheckAsync(AppVersion.Current);

if (update is not null)
{
    var dialog = new UpdateDialog(update);  // your own dialog
    var accepted = await dialog.ShowAsync();

    if (accepted && update.DownloadUrl is not null)
    {
        var dest = Path.Combine(Path.GetTempPath(), $"update-{update.LatestVersion}.exe");
        var client = new UpdateHubClient(ServerUrl, AppSlug);

        await client.DownloadAsync(
            update.DownloadUrl,
            dest,
            progress: new Progress<double>(p => ProgressBar.Value = p * 100));

        // Optional: verify SHA-256 before running
        if (update.Sha256 is not null && !UpdateHubClient.VerifySha256(dest, update.Sha256))
        {
            // show error
            return;
        }

        // Launch installer and exit
        System.Diagnostics.Process.Start(dest);
        Environment.Exit(0);
    }
}
```

## 4. Where to put the version string

Use `Assembly` attributes or a static constant:

```csharp
// AssemblyInfo.cs or wherever you keep version
public static class AppVersion
{
    public const string Current = "1.2.0";
}
```

Or read from the assembly:
```csharp
var version = System.Reflection.Assembly
    .GetExecutingAssembly()
    .GetName().Version?.ToString(3) ?? "0.0.0";
```

## 5. Register the app in UpdateHub

Before the integration works, register the app in the admin UI:

1. Open UpdateHub admin (`https://your-server/`)
2. Applications → **+ New Application**
3. Slug: `ai-studio` (must match `AppSlug` in your code)
4. Name: `AI Studio`

Then create a release, upload the installer, and publish it.

## Notes

- Checking for updates on startup is fire-and-forget — catch all exceptions, never block the app
- The update dialog and download logic are your responsibility — UpdateHub only provides the metadata
- For **StoryForge**: Velopack already handles updates there — no need to add UpdateHub
