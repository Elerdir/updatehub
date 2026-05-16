using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UpdateHub.Application.Interfaces;

namespace UpdateHub.Infrastructure;

/// <summary>
/// In-process background queue that drains queued work items one at a time.
/// Each work item runs inside a freshly created DI scope so it can resolve
/// scoped services (DbContext, repositories) safely after the originating
/// HTTP request has ended.
/// </summary>
public class BackgroundNotificationQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundNotificationQueue> logger)
    : BackgroundService, INotificationQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> work) =>
        _channel.Writer.TryWrite(work);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await work(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background notification failed");
            }
        }
    }
}
