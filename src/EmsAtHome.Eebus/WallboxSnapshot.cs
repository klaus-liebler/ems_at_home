namespace EmsAtHome.Eebus;

/// <summary>
/// App-facing projection of EEBUS.Net's DeviceData/DeviceConnectionStatus for one paired remote
/// device - keeps API/dashboard consumers decoupled from EEBUS.Net's own DTO shapes.
/// </summary>
public sealed record WallboxSnapshot(
    string Ski,
    string? Name,
    string ConnectionStatus,
    bool? LimitActive,
    long? LimitWatts,
    int? LimitDurationSeconds,
    float? AcPowerTotalWatts,
    float? GridConsumptionWatts,
    float? AcEnergyConsumedWattHours,
    float? VoltagePhaseA,
    float? VoltagePhaseB,
    float? VoltagePhaseC);
