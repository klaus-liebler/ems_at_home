namespace EmsAtHome.Abstractions;

/// <summary>
/// Base contract every physical device integration (inverter, meter, charger, battery, ...) implements.
/// Kept intentionally minimal until the first real device (Milestone 2+) forces a concrete shape.
/// </summary>
public interface IEnergyDevice
{
    /// <summary>Stable identifier used for registration, logging, and persistence (once it exists).</summary>
    string Id { get; }

    /// <summary>Human-readable name for logs/UI, e.g. "Huawei SUN2000" or "Elli Wallbox".</summary>
    string DisplayName { get; }
}
