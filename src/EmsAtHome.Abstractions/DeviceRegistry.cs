namespace EmsAtHome.Abstractions;

/// <summary>
/// Plain in-process lookup of registered <see cref="IEnergyDevice"/> instances by Id.
/// No persistence here on purpose - device configuration/persistence is deferred until
/// a real device exists (see Design/decisions), this only serves DI-time wiring.
/// </summary>
public sealed class DeviceRegistry
{
    private readonly Dictionary<string, IEnergyDevice> _devices = [];

    public void Register(IEnergyDevice device) => _devices.Add(device.Id, device);

    public IEnergyDevice Get(string id) => _devices[id];

    public IReadOnlyCollection<IEnergyDevice> All => _devices.Values;
}
