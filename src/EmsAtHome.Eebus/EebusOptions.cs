namespace EmsAtHome.Eebus;

/// <summary>
/// Binds the "Eebus" configuration section. Deliberately declares no SPINE use case/entity for
/// Milestone 1b: SHIP pairing itself is entity/device-level and doesn't require a matching use
/// case up front (that's negotiated afterwards) - so the minimal, honest config for "does pairing
/// work at all" is an empty Entities list, not a fabricated LPC declaration we'd never act on.
/// See Design/decisions/0001-eebus-net-library-choice.md.
/// </summary>
public sealed class EebusOptions
{
    public const string SectionName = "Eebus";

    /// <summary>Subject name of the PFX EEBUS.Net expects at {BasePath}/{Certificate}.pfx (password "xcenter").</summary>
    public required string Certificate { get; set; }

    /// <summary>Directory holding the certificate PFX and the trusted-SKI store - never inside the git repo.</summary>
    public required string BasePath { get; set; }

    public string Name { get; set; } = "ems-at-home-cem";
    public string Id { get; set; } = "ems-at-home-cem-1";
    public string Model { get; set; } = "EmsAtHome";
    public string Brand { get; set; } = "Klaus Liebler";
    public string Type { get; set; } = "EnergyManagementSystem";
    public string Serial { get; set; } = "0001";
    public string Host { get; set; } = "0.0.0.0";
    public ushort Port { get; set; } = 4712;

    public global::EEBUS.Settings ToEebusSettings() => new()
    {
        Certificate = Certificate,
        BasePath = BasePath,
        Device = new global::EEBUS.DeviceSettings
        {
            Name = Name,
            Id = Id,
            Model = Model,
            Brand = Brand,
            Type = Type,
            Serial = Serial,
            Host = Host,
            Port = Port,
            Secret = string.Empty,
            NetworkFeatureSet = string.Empty,
            Entities = [],
        },
    };
}
