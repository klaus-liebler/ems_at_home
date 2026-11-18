using EEBUS.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmsAtHome.Eebus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEebus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EebusOptions>()
            .Bind(configuration.GetSection(EebusOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => !string.IsNullOrWhiteSpace(o.BasePath),
                "Eebus:BasePath ist nicht gesetzt - siehe appsettings.Local.json (gitignored) bzw. " +
                "Design/decisions/0001-eebus-net-library-choice.md.")
            .ValidateOnStart();

        services.AddSingleton<ITrustedSkiStore>(sp =>
            new JsonTrustedSkiStore(sp.GetRequiredService<IOptions<EebusOptions>>().Value));

        // EEBUSManager is registered as its own singleton (rather than being created inside
        // EebusHostedService) so API endpoints and other components can inject it directly to
        // query/write live device data - see EebusHostedService's doc comment.
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EebusOptions>>().Value;
            CertificateProvisioning.EnsureCertificateExists(options);

            var manager = new EEBUSManager(options.ToEebusSettings(), logger: sp.GetRequiredService<ILogger<EEBUSManager>>());

            foreach (var ski in sp.GetRequiredService<ITrustedSkiStore>().GetTrustedSkis())
            {
                manager.AddTrustedSki(ski);
            }

            return manager;
        });

        services.AddHostedService<EebusHostedService>();

        return services;
    }
}
