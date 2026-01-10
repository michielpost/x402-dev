using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.HostWallet;
using Nethereum.UI;
using System.Security.Claims;
using x402.Client.v2;

namespace x402dev.Client.Pages;

public class BaseMetaMaskPage : ComponentBase, IDisposable
{
    [Inject]
    protected SelectedEthereumHostProviderService selectedHostProviderService { get; set; } = default!;

    [Inject]
    protected IWalletProvider WalletProvider { get; set; } = default!;

    [CascadingParameter]
    public Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    protected string? SelectedAccount { get; set; }
    protected string? UserName { get; set; }
    protected long SelectedChainId { get; set; }
    protected IEthereumHostProvider? _ethereumHostProvider;

    protected override void OnInitialized()
    {
        if (OperatingSystem.IsBrowser())
        {
            //metamask is selected
            _ethereumHostProvider = selectedHostProviderService.SelectedHost;
            _ethereumHostProvider.SelectedAccountChanged += HostProvider_SelectedAccountChanged;
        }
    }

    public virtual void Dispose()
    {
        if (_ethereumHostProvider != null)
        {
            _ethereumHostProvider.SelectedAccountChanged -= HostProvider_SelectedAccountChanged;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (OperatingSystem.IsBrowser())
        {
            if (_ethereumHostProvider != null)
            {
                var ethereumAvailable = await _ethereumHostProvider.CheckProviderAvailabilityAsync();
                if (ethereumAvailable)
                {
                    SelectedAccount = await _ethereumHostProvider.GetProviderSelectedAccountAsync();
                }
            }

            var authState = await AuthenticationState;
            if (authState != null)
            {
                UserName = authState.User.FindFirst(c => c.Type.Contains(ClaimTypes.NameIdentifier))?.Value;
            }
        }
    }

    protected virtual async Task HostProvider_SelectedAccountChanged(string account)
    {
        SelectedAccount = account;
        this.StateHasChanged();
    }

    protected async Task<string> ChangeChainTo(ulong chainId)
    {
        if (_ethereumHostProvider == null)
            return "_ethereumHostProvider is null";

        try
        {
            var web3 = await _ethereumHostProvider.GetWeb3Async();
            var result = await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(new SwitchEthereumChainParameter() { ChainId = new HexBigInteger(chainId) });
            return result;
        }
        catch (Exception ex)
        {
            return $"Error changing MetaMask chain to {chainId}: {ex.Message}";
        }
    }
}
