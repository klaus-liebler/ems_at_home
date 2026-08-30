namespace EmsAtHome.Abstractions;

/// <summary>A battery charger/inverter or BMS that can also be commanded, not just read (Victron Multiplus, JKBMS, ...).</summary>
public interface IBattery : IMeter
{
    Task SetChargePowerLimitAsync(double watts, CancellationToken cancellationToken);
}
