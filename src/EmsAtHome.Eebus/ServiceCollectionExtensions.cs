using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EmsAtHome.Eebus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEebus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EebusOptions>()
            .Bind(configuration.GetSection(EebusOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITrustedSkiStore>(sp =>
            new JsonTrustedSkiStore(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EebusOptions>>().Value));

        services.AddHostedService<EebusHostedService>();

        return services;
    }
}
