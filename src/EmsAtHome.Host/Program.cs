using EmsAtHome.Eebus;
using EmsAtHome.Host;

var builder = WebApplication.CreateBuilder(args);

// EEBus certificates and the trusted-SKI store live outside the repo (never committed) - the
// directory itself (Eebus:BasePath) is machine-specific and configured in the gitignored
// appsettings.Local.json, not the committed appsettings.json. See
// Design/decisions/0001-eebus-net-library-choice.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddEebus(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", () => Results.Ok(new { status = "ems-at-home laeuft" }));
app.MapEebusEndpoints();

app.Run();
