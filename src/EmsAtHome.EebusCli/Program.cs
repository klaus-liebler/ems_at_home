using EEBUS;
using EmsAtHome.EebusCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// .NET buffers Console.Out when stdout is redirected (not an interactive terminal) instead of
// flushing per line - harmless when run directly in a terminal, but means a piped/redirected
// consumer (log capture, this project's own test scripts) can see output arrive in delayed,
// arbitrarily-sized chunks instead of as it's printed. Force line-flushing unconditionally.
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddCommandLine(args)
    .Build();

string logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", $"eebus-cli-{DateTime.Now:yyyyMMdd-HHmmss}.log");

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    // Per-provider levels (appsettings.json: Logging:Console:*, Logging:File:*) so the interactive
    // console can stay quiet enough to type commands in, while the file keeps the full SHIP/SPINE
    // trace - typing a command used to be a fight against constantly scrolling Trace-level logs.
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
    });
    builder.AddProvider(new FileLoggerProvider(logFilePath));
});

Console.WriteLine($"Full SHIP/SPINE trace log: {logFilePath}");

var eebus = configuration.GetSection("Eebus");
string basePath = eebus["BasePath"]
    ?? throw new InvalidOperationException(
        "Eebus:BasePath is not set. Copy appsettings.Local.json.example to appsettings.Local.json " +
        "(gitignored) next to this project and point it at a certificate directory outside the repo.");

var settings = new Settings
{
    Certificate = eebus["Certificate"] ?? "ems-eebus-cli",
    BasePath = basePath,
    Device = new DeviceSettings
    {
        Name = eebus["Name"] ?? "ems-at-home-cli",
        Id = eebus["Id"] ?? "ems-at-home-cli-1",
        Model = "EmsAtHomeCli",
        Brand = "ems_at_home",
        Type = "EnergyManagementSystem",
        Serial = "0001",
        Port = ushort.Parse(eebus["Port"] ?? "4713"),
        // Fixed hex secret, not left empty: EEBUS.Net's LocalDevice.GetSecret() generates a random
        // one per process start when Settings.Secret is empty, which changes the SHIP identity
        // string on every restart and gets the paired identity rejected as "SHIP id mismatch" on
        // the very next connection - a full day was lost to this exact bug earlier in this project,
        // see Design/decisions/0001-eebus-net-library-choice.md. Must stay stable across restarts
        // for a given paired identity.
        Secret = eebus["Secret"] ?? "00112233445566778899AABBCCDDEEFF",
        Entities =
        [
            // Every EEBUS.Net reference config leads with a "DeviceInformation" entity at index 0.
            new EntitySettings { Type = "DeviceInformation" },
            new EntitySettings
            {
                Type = "CEM",
                UseCases =
                [
                    // EnergyGuard = the client role that WRITES a consumption limit onto a remote
                    // ControllableSystem (e.g. a wallbox) - what the "write" demo command uses.
                    new UseCaseSettings
                    {
                        Type = "limitationOfPowerConsumption",
                        Actor = "EnergyGuard",
                        InitLimits = new LimitSettings
                        {
                            Active = false,
                            Limit = 11000,
                            Duration = TimeSpan.FromHours(1),
                            FailsafeLimit = 4200,
                            FailsafeDurationMinimum = TimeSpan.FromMinutes(2),
                            NominalMax = 11000,
                        },
                    },
                    new UseCaseSettings
                    {
                        Type = "monitoringOfPowerConsumption",
                        Actor = "MonitoringAppliance",
                    },
                    // CEM = the actor string for evChargingSummary when Energy Guard and Energy
                    // Broker are the same entity (which they are here) - see the Use Case's "Use
                    // Case Discovery rules". Only enables reading a remote's Bill feature.
                    new UseCaseSettings
                    {
                        Type = "evChargingSummary",
                        Actor = "CEM",
                    },
                ],
            },
        ],
    },
};

string[] trustedSkis = eebus.GetSection("TrustedSkis").Get<string[]>() ?? [];

await new EebusDemo(loggerFactory).RunAsync(settings, trustedSkis);
