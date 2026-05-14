using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UpdateHub.Application.Interfaces;

namespace UpdateHub.Infrastructure;

/// <summary>
/// In-process background queue that drains queued work items one at a time.
/// Used for webhooks and SMTP so the publish/login request never blocks on them.
/// </summary>
public class BackgroundNotificationQueue(ILogger<BackgroundNotificationQueue> logger)
    : BackgroundService, INotificationQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>();

    public void Enqueue(Func<CancellationToken, Task> work) =>
        _channel.Writer.TryWrite(work);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await work(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background notification failed");
            }
        }
    }
}
