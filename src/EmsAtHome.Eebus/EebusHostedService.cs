using System.Text.Json;
using EEBUS.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmsAtHome.Eebus;

/// <summary>
/// Wraps EEBUSManager's lifecycle as an IHostedService. EEBUSManager is a plain object with its
/// own embedded Kestrel for the SHIP endpoint (separate port from our own ASP.NET Core pipeline) -
/// there is nothing else to wire into our request pipeline. See
/// Design/decisions/0001-eebus-net-library-choice.md.
/// </summary>
public sealed class EebusHostedService : IHostedService
{
    private readonly EebusOptions _options;
    private readonly ITrustedSkiStore _trustedSkiStore;
    private readonly ILogger<EebusHostedService> _logger;
    private EEBUSManager? _manager;

    public EebusHostedService(IOptions<EebusOptions> options, ITrustedSkiStore trustedSkiStore, ILogger<EebusHostedService> logger)
    {
        _options = options.Value;
        _trustedSkiStore = trustedSkiStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CertificateProvisioning.EnsureCertificateExists(_options);

        _manager = new EEBUSManager(_options.ToEebusSettings(), logger: _logger);

        foreach (var ski in _trustedSkiStore.GetTrustedSkis())
        {
            _manager.AddTrustedSki(ski);
        }

        _manager.Start();

        var local = _manager.GetLocalData();
        _logger.LogInformation("EEBus-CEM gestartet: {Local}", JsonSerializer.Serialize(local));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_manager is null)
            return;

        await _manager.StopAsync();
        _manager.Dispose();
    }
}
