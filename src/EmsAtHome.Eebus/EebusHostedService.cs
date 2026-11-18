using System.Text.Json;
using EEBUS.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmsAtHome.Eebus;

/// <summary>
/// Starts/stops the <see cref="EEBUSManager"/> lifecycle. The manager itself is a singleton
/// registered in <see cref="ServiceCollectionExtensions"/> (already provisioned with its
/// certificate and trusted SKIs) so other components - API endpoints, a future CLI - can inject
/// it directly to query and write live device data. See
/// Design/decisions/0001-eebus-net-library-choice.md.
/// </summary>
public sealed class EebusHostedService(EEBUSManager manager, ILogger<EebusHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        manager.Start();
        logger.LogInformation("EEBus-CEM gestartet: {Local}", JsonSerializer.Serialize(manager.GetLocalData()));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => manager.StopAsync();
}
