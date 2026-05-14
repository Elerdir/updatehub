namespace UpdateHub.Application.Interfaces;

/// <summary>
/// Fire-and-forget queue for slow side-effects (webhooks, SMTP) so they never
/// block the request that triggered them.
/// </summary>
public interface INotificationQueue
{
    void Enqueue(Func<CancellationToken, Task> work);
}
