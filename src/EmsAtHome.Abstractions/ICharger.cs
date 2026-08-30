namespace EmsAtHome.Abstractions;

/// <summary>
/// An EV charger/wallbox. <see cref="SetMaxCurrentAsync"/> takes a fractional ampere value so a
/// milliampere-precise backend (e.g. EEBus/SPINE) isn't forced to round to whole amps just to
/// satisfy this interface - see Design/decisions/0001-eebus-net-library-choice.md for why this matters.
/// </summary>
public interface ICharger : IMeter
{
    Task SetMaxCurrentAsync(double amps, CancellationToken cancellationToken);
}
