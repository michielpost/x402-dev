using Microsoft.Extensions.DependencyInjection;

namespace x402dev.Services.Security;

public static class PublicOutboundHttpClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the named client used for every outbound request to a
    /// user-supplied URL. Registering it next to the policy (rather than only in
    /// the web host) keeps the registry checker from silently falling back to a
    /// default client that follows redirects and reaches private addresses.
    /// </summary>
    public static IServiceCollection AddPublicOutboundHttpClient(this IServiceCollection services) =>
        services.AddHttpClient(PublicProxyNetworkPolicy.ClientName)
            .ConfigurePrimaryHttpMessageHandler(PublicProxyNetworkPolicy.CreateHandler)
            .Services;
}
