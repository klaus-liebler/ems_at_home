namespace EmsAtHome.Eebus;

/// <summary>
/// Binds the "Eebus" configuration section. Milestone 2 (walking skeleton): declares our local
/// "CEM" entity with SPINE use cases EEBUS.Net already implements end-to-end without a fork -
/// LimitationOfPowerConsumption (EnergyGuard actor, read+write) and MonitoringOfPowerConsumption
/// as MonitoringAppliance (client, reads the wallbox's data) - so we can prove real SPINE data
/// exchange with the wallbox before investing in the OPEV fork from
/// Design/decisions/0002-opev-usecase-integration.md.
///
/// Do NOT also register MonitoringOfPowerConsumption as MonitoredUnit (exposing dummy measurement
/// data of our own) - registering the same use case twice with two different actors made the real
/// wallbox stop answering anything past `NodeDiscoveryCompleted` (silently, no rejection, no error -
/// see ADR-0001's "Offene Punkte" for the full day-long investigation this caused). Confirmed via a
/// from-scratch identity + minimal-declaration test on 2026-08-31: removing the MonitoredUnit
/// registration alone was sufficient to reach `UseCaseDiscoveryCompleted` and live measurement
/// notifies. See also the <see cref="Secret"/> property below for a second, independent bug found
/// and fixed the same day.
/// </summary>
public sealed class EebusOptions
{
    public const string SectionName = "Eebus";

    /// <summary>Subject name of the PFX EEBUS.Net expects at {BasePath}/{Certificate}.pfx (password "xcenter").</summary>
    public required string Certificate { get; set; }

    /// <summary>
    /// Directory holding the certificate PFX and the trusted-SKI store - never inside the git repo.
    /// Machine-specific, so it's set via the gitignored appsettings.Local.json, not appsettings.json.
    /// </summary>
    public required string BasePath { get; set; }

    public string Name { get; set; } = "ems-at-home-cem";
    public string Id { get; set; } = "ems-at-home-cem-1";
    public string Model { get; set; } = "EmsAtHome";
    public string Brand { get; set; } = "Klaus Liebler";
    public string Type { get; set; } = "EnergyManagementSystem";
    public string Serial { get; set; } = "0001";
    public string Host { get; set; } = "0.0.0.0";
    public ushort Port { get; set; } = 4712;

    /// <summary>
    /// Fixed hex secret (16 bytes) fed into the SHIP identity string's SPSEC component
    /// (EEBUS.Net's LocalDevice.GetSecret()). Left as EEBUS.Net's default empty string, this
    /// generates a fresh random secret on every process start, changing the SHIP identity string
    /// every restart and making the wallbox reject every reconnect as "SHIP id mismatch" (4450) -
    /// this cost a full day of investigation on 2026-08-31 before being found, see ADR-0001. Must
    /// stay stable across restarts for a given paired identity - do not change without re-pairing.
    /// </summary>
    public string Secret { get; set; } = "F7D9AD5D363C0E4E863CF2CFF376914A";

    /// <summary>Whether the LPC consumption limit starts out actively limiting the wallbox at boot. Defaults to false so the walking skeleton never silently throttles charging.</summary>
    public bool LpcInitialLimitActive { get; set; } = false;

    /// <summary>Contractual/nominal maximum consumption in Watts - also the initial (inactive) limit value. Adjust to the real household connection rating.</summary>
    public long LpcNominalMaxWatts { get; set; } = 11000;

    /// <summary>Failsafe limit in Watts the wallbox must fall back to if it loses contact with us. 4200 W (6 A x 3 phases x 230 V) is the German §14a-mandated minimum.</summary>
    public long LpcFailsafeLimitWatts { get; set; } = 4200;

    /// <summary>How long the wallbox waits without hearing from us before entering the failsafe limit.</summary>
    public TimeSpan LpcFailsafeDurationMinimum { get; set; } = TimeSpan.FromMinutes(2);

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
            Secret = Secret,
            // Deliberately NOT set: left null, EEBUS.Net omits the networkFeatureSet field from our
            // own deviceInformation entirely (matches the one working reference test on 2026-08-31).
            // An earlier explicit `= string.Empty` here sent networkFeatureSet:"" instead - suspected
            // as a further cause of the wallbox not answering past NodeDiscoveryCompleted, see
            // ADR-0001.
            Entities =
            [
                // Every EEBUS.Net reference config (ConsoleDemo, the bundled test fixtures) leads
                // with a "DeviceInformation" entity at index 0 before the actual "CEM" entity.
                new global::EEBUS.EntitySettings
                {
                    Type = "DeviceInformation",
                },
                new global::EEBUS.EntitySettings
                {
                    Type = "CEM",
                    UseCases =
                    [
                        new global::EEBUS.UseCaseSettings
                        {
                            Type = "limitationOfPowerConsumption",
                            Actor = "EnergyGuard",
                            InitLimits = new global::EEBUS.LimitSettings
                            {
                                Active = LpcInitialLimitActive,
                                Limit = LpcNominalMaxWatts,
                                Duration = TimeSpan.FromHours(1),
                                FailsafeLimit = LpcFailsafeLimitWatts,
                                FailsafeDurationMinimum = LpcFailsafeDurationMinimum,
                                NominalMax = LpcNominalMaxWatts,
                            },
                        },
                        new global::EEBUS.UseCaseSettings
                        {
                            Type = "monitoringOfPowerConsumption",
                            Actor = "MonitoringAppliance",
                        },
                    ],
                },
            ],
        },
    };
}
