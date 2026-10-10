namespace UpdateHub.Application.Services;

/// <summary>
/// Holds the public BaseUrl so services (notably <see cref="EmailNotificationService"/>)
/// can build absolute links without taking a direct config dependency.
/// </summary>
public class BaseUrlAccessor(string baseUrl)
{
    public string BaseUrl { get; } = baseUrl.TrimEnd('/');
}
