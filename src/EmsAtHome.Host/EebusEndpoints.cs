using EEBUS.Net;
using EmsAtHome.Eebus;

namespace EmsAtHome.Host;

/// <summary>Minimal API surface for the walking-skeleton dashboard - see 00-overview.md, Milestone 2.</summary>
public static class EebusEndpoints
{
    public static void MapEebusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/eebus/devices", (EEBUSManager manager) =>
            manager.GetDiscoveredDevices().Select(d => new
            {
                d.Ski,
                d.Name,
                ConnectionStatus = manager.GetConnectionStatus(d.Ski).ToString(),
            }));

        app.MapGet("/api/eebus/wallbox", async (EEBUSManager manager, ITrustedSkiStore trustedSkis, string? ski) =>
        {
            var targetSki = ski ?? trustedSkis.GetTrustedSkis().FirstOrDefault();
            if (targetSki is null)
                return Results.NotFound(new { error = "Keine gepaarte Wallbox konfiguriert." });

            var snapshot = await WallboxQueries.GetSnapshotAsync(manager, targetSki);
            return snapshot is null
                ? Results.NotFound(new { error = $"Keine aktive Verbindung zur SKI {targetSki}." })
                : Results.Ok(snapshot);
        });

        app.MapPost("/api/eebus/wallbox/limit", async (EEBUSManager manager, ITrustedSkiStore trustedSkis, SetLimitRequest request) =>
        {
            var targetSki = request.Ski ?? trustedSkis.GetTrustedSkis().FirstOrDefault();
            if (targetSki is null)
                return Results.NotFound(new { error = "Keine gepaarte Wallbox konfiguriert." });

            await WallboxQueries.SetConsumptionLimitAsync(manager, targetSki, request.Active, request.Watts);
            return Results.Accepted();
        });
    }
}

public sealed record SetLimitRequest(bool Active, long Watts, string? Ski = null);
