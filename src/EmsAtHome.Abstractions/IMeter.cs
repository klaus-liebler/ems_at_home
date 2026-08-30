namespace EmsAtHome.Abstractions;

/// <summary>A device that only reports measurements (energy meter, inverter telemetry, BMS cell data, ...).</summary>
public interface IMeter : IEnergyDevice
{
    Task<IReadOnlyList<MeasurementSample>> ReadAsync(CancellationToken cancellationToken);
}
