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
using x402dev.Client;
using x402dev.Client.Extensions;
using x402dev.Client.Models;
using x402dev.Shared.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddFluentUIComponents();

builder.Services.AddScoped<SignatureBuilderState>();


builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<IWalletProvider, WalletProvider>();
builder.Services.AddTransient<PaymentRequiredV1Handler>();

builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<IMetamaskInterop, MetamaskBlazorInterop>();
builder.Services.AddSingleton<MetamaskHostProvider>();

//Add metamask as the selected ethereum host provider
builder.Services.AddSingleton(services =>
{
    var metamaskHostProvider = services.GetService<MetamaskHostProvider>();
    var selectedHostProvider = new SelectedEthereumHostProviderService();
    selectedHostProvider.SetSelectedEthereumHostProvider(metamaskHostProvider);
    return selectedHostProvider;
});
builder.Services.AddSingleton<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();


builder.Services.AddHttpClient("x402", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<PaymentRequiredV1Handler>();

builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler(() => new GrpcWebHandler(GrpcWebMode.GrpcWeb));


builder.Services.AddGrpcService<IFacilitatorGrpcService>();
builder.Services.AddGrpcService<IContentGrpcService>();
builder.Services.AddGrpcService<IPublicMessageGrpcService>();


await builder.Build().RunAsync();
