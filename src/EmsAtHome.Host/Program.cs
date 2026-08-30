using EmsAtHome.Eebus;

var builder = WebApplication.CreateBuilder(args);

// EEBus certificates and the trusted-SKI store live outside the repo (never committed) -
// see Design/decisions/0001-eebus-net-library-choice.md.
var certDir = Environment.GetEnvironmentVariable("EMS_EEBUS_CERT_DIR")
    ?? throw new InvalidOperationException(
        "Umgebungsvariable EMS_EEBUS_CERT_DIR ist nicht gesetzt (Verzeichnis mit dem EEBus-PFX-Zertifikat).");

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Eebus:BasePath"] = certDir,
});

builder.Services.AddEebus(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ems-at-home laeuft" }));

app.Run();
