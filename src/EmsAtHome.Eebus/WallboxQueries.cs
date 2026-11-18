using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;

namespace EmsAtHome.Eebus;

/// <summary>
/// Thin translation layer between EEBUS.Net's <see cref="EEBUSManager"/> API and our own
/// app-facing <see cref="WallboxSnapshot"/> shape. LimitationOfPowerConsumption (LPC) is the only
/// use case with full read+write support in the unforked EEBUS.Net 1.0.30 - see
/// Design/decisions/0002-opev-usecase-integration.md for why the mA-precise OPEV use case needs a
/// fork - so LPC doubles here as the "small intervention" demo for the walking-skeleton dashboard.
/// </summary>
public static class WallboxQueries
{
    /// <summary>
    /// Actively reads the current LPC limit and Measurement data from the remote device - returns
    /// null if there is no active connection for <paramref name="ski"/> yet.
    /// </summary>
    /// <remarks>
    /// LPC and Measurement are read in separate round-trips, each tolerating its own failure: the
    /// wallbox has been observed to not answer LoadControl reads at all while no EV is connected
    /// (see ADR-0001), which would otherwise throw before a Measurement read - which the wallbox
    /// does answer even when idle - is even attempted.
    /// </remarks>
    public static async Task<WallboxSnapshot?> GetSnapshotAsync(EEBUSManager manager, string ski)
    {
        var status = manager.GetConnectionStatus(ski);
        if (status == DeviceConnectionStatus.Unknown)
            return null;

        var name = manager.GetDiscoveredDevices().FirstOrDefault(d => d.Ski == ski)?.Name;

        MeasurementsData? measurements = null;
        try
        {
            var data = new DeviceData { Measurements = new MeasurementsData() };
            await manager.ReadDataAsync(data, ski);
            measurements = data.Measurements;
        }
        catch { /* e.g. wallbox not answering while idle - degrade gracefully, see remarks above */ }

        LpcLppData? lpc = null;
        try
        {
            var data = new DeviceData { Lpc = new LpcLppData() };
            await manager.ReadDataAsync(data, ski);
            lpc = data.Lpc;
        }
        catch { /* e.g. wallbox not answering while idle - degrade gracefully, see remarks above */ }

        return new WallboxSnapshot(
            Ski: ski,
            Name: name,
            ConnectionStatus: status.ToString(),
            LimitActive: lpc?.LimitActive,
            LimitWatts: lpc?.Limit,
            LimitDurationSeconds: lpc?.LimitDuration,
            AcPowerTotalWatts: measurements?.AcPowerTotal,
            GridConsumptionWatts: measurements?.GridConsumption,
            AcEnergyConsumedWattHours: measurements?.AcEnergyConsumed,
            VoltagePhaseA: measurements?.AcVoltage?.PhaseA,
            VoltagePhaseB: measurements?.AcVoltage?.PhaseB,
            VoltagePhaseC: measurements?.AcVoltage?.PhaseC);
    }

    /// <summary>Writes our locally declared LPC consumption limit and pushes it to the remote device.</summary>
    public static Task SetConsumptionLimitAsync(EEBUSManager manager, string ski, bool active, long watts)
    {
        var data = new DeviceData
        {
            Lpc = new LpcLppData
            {
                LimitActive = active,
                Limit = watts,
            },
        };
        return manager.WriteDataAsync(data, ski);
    }
}
