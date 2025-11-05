using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Nethereum.Blazor;
using Nethereum.Metamask;
using Nethereum.Metamask.Blazor;
using Nethereum.UI;
using x402.Client.v1;
using x402dev.Client.Extensions;
using x402dev.Client.Models;
using x402dev.Shared.Interfaces;

namespace x402dev.Client;

public class Program
{
    public static string? Version { get; set; }
    public static string PageTitlePostFix => "x402dev";

    public static string? GetVersionHash()
    {
        if (Version != null)
        {
            string shortVersion = Version;
            int sep = shortVersion.LastIndexOf('-');
            if (sep >= 0 && sep < shortVersion.Length)
                shortVersion = shortVersion.Substring(sep + 1);

            int sep1 = shortVersion.LastIndexOf('+');
            if (sep1 >= 0 && sep1 < shortVersion.Length)
                shortVersion = shortVersion.Substring(sep1 + 1);

            if (shortVersion.Length > 7)
                shortVersion = shortVersion.Substring(0, 7);

            return shortVersion;
        }
        return null;
    }

    public static string? GetVersionWithoutHash()
    {
        if (Version != null)
        {
            string shortVersion = Version;
            int sep = shortVersion.LastIndexOf('-');
            if (sep >= 0)
                return shortVersion.Substring(0, sep);

            int sep1 = shortVersion.IndexOf('+');
            if (sep1 >= 0 && sep1 < shortVersion.Length)
                shortVersion = shortVersion.Substring(0, sep1);

            return shortVersion;

        }
        return Version;
    }

    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var baseAddress = builder.HostEnvironment.BaseAddress;

#if RELEASE
        baseAddress = "https://api.x402dev.com";
#endif
        ConfigureServices(builder.Services, baseAddress);

        WebAssemblyHost host = builder.Build();

        await host.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection services, string baseAddress)
    {

        services.AddFluentUIComponents();

        services.AddScoped<SignatureBuilderState>();

        services.AddSingleton<IWalletProvider, WalletProvider>();
        services.AddTransient<PaymentRequiredV1Handler>();

        services.AddAuthorizationCore();
        services.AddSingleton<IMetamaskInterop, MetamaskBlazorInterop>();
        services.AddSingleton<MetamaskHostProvider>();

        //Add metamask as the selected ethereum host provider
        services.AddSingleton(services =>
        {
            var metamaskHostProvider = services.GetService<MetamaskHostProvider>();
            var selectedHostProvider = new SelectedEthereumHostProviderService();
            selectedHostProvider.SetSelectedEthereumHostProvider(metamaskHostProvider);
            return selectedHostProvider;
        });
        services.AddSingleton<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();


        services.AddHttpClient("x402", client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        })
        .AddHttpMessageHandler<PaymentRequiredV1Handler>();

        services.AddHttpClient("ServerAPI", client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        })
        .AddHttpMessageHandler(() => new GrpcWebHandler(GrpcWebMode.GrpcWeb));

        services.AddGrpcService<IFacilitatorGrpcService>(baseAddress);
        services.AddGrpcService<IContentGrpcService>(baseAddress);
        services.AddGrpcService<IPublicMessageGrpcService>(baseAddress);


    }
}

