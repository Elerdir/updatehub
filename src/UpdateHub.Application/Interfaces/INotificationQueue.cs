namespace UpdateHub.Application.Interfaces;

/// <summary>
/// Fire-and-forget queue for slow side-effects (webhooks, SMTP) so they never
/// block the request that triggered them.
/// </summary>
public interface INotificationQueue
{
    /// <summary>
    /// The work delegate gets a fresh DI scope each invocation — resolve any
    /// scoped service (DbContext, repositories, settings) from it instead of
    /// capturing the request's scope, which is gone by the time the queue runs.
    /// </summary>
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> work);
}
