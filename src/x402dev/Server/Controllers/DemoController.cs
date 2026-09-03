using Microsoft.AspNetCore.Mvc;
using x402;
using x402.Core.Enums;
using x402.Core.Models;
using x402.Core.Models.v2;
using x402dev.Server.Models;

namespace x402dev.Server.Controllers
{
    /// <summary>
    /// Live demo endpoints for the x402 payment schemes, using testnet USDC on Base Sepolia.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class DemoController : ControllerBase
    {
        private const string TestnetUsdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e"; //Base Sepolia
        private const string PayToAddress = "0x7D95514aEd9f13Aa89C8e5Ed9c29D08E8E9BfA37";
        private const string IconUrl = "https://www.x402dev.com/x402-button-small.png";

        private readonly X402HandlerV2 x402Handler;

        public DemoController(X402HandlerV2 x402Handler)
        {
            this.x402Handler = x402Handler;
        }

        /// <summary>
        /// Demo of the x402 "upto" scheme (usage-based billing).
        /// You authorize a maximum of 0.01 testnet USDC; the server settles only the actual usage,
        /// controlled by the optional "usage" query parameter (percentage of the authorized maximum).
        /// </summary>
        /// <param name="usage">Percentage of the authorized maximum to charge (0-100, default 50)</param>
        /// <returns></returns>
        [HttpGet]
        [Route("upto")]
        public async Task<DemoResponse?> Upto(int usage = 50)
        {
            var x402Result = await x402Handler.HandleX402Async(
                new PaymentRequiredInfo
                {
                    Accepts = new List<PaymentRequirementsBasic>
                    {
                        new PaymentRequirementsBasic
                        {
                            Scheme = PaymentScheme.Upto,
                            Asset = TestnetUsdc,
                            Amount = "10000", //Authorized maximum: 0.01 USDC
                            PayTo = PayToAddress,
                        }
                    },
                    Resource = new ResourceInfoBasic
                    {
                        Description = "Demo of the x402 'upto' scheme: authorize a maximum, pay only actual usage.",
                        ServiceName = "x402dev Upto Demo",
                        Tags = new List<string> { "demo", "upto", "usage-based" },
                        IconUrl = IconUrl,
                    },
                    Discoverable = true
                },
                SettlementMode.Pessimistic,
                onSetOutputSchema: (context, reqs, schema) =>
                {
                    schema.Input ??= new();
                    schema.Input.Method = "GET";
                    schema.Input.QueryParams = new Dictionary<string, object>
                    {
                        {
                            nameof(usage),
                            new FieldDefenition
                            {
                                Required = false,
                                Description = "Percentage of the authorized maximum to charge (0-100, default 50)",
                                Type = "number"
                            }
                        }
                    };

                    return schema;
                });

            if (!x402Result.CanContinueRequest)
            {
                return null;
            }

            usage = Math.Clamp(usage, 0, 100);

            //Upto settles after the response, for the actual usage only
            HttpContext.SetSettlementOverrides($"{usage}%");

            return new DemoResponse
            {
                Scheme = "upto",
                AuthorizedMax = "10000 (0.01 USDC)",
                Charged = $"{usage}% of the authorized maximum",
                Message = $"Success! You authorized 0.01 USDC and are charged {usage}% of it. With the 'upto' scheme settlement happens after the response, for the actual usage only."
            };
        }

        /// <summary>
        /// Demo of the x402 "batch-settlement" scheme (payment channels).
        /// You authorize a channel deposit of 0.01 testnet USDC; each request records a signed
        /// off-chain voucher of 0.001 USDC. A ChannelManager periodically batches vouchers into a
        /// single on-chain settlement and refunds unused balances of idle channels.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("batch-settlement")]
        public async Task<DemoResponse?> BatchSettlement()
        {
            var x402Result = await x402Handler.HandleX402Async(
                new PaymentRequiredInfo
                {
                    Accepts = new List<PaymentRequirementsBasic>
                    {
                        new PaymentRequirementsBasic
                        {
                            Scheme = PaymentScheme.BatchSettlement,
                            Asset = TestnetUsdc,
                            Amount = "10000", //Channel deposit: 0.01 USDC
                            PayTo = PayToAddress,
                        }
                    },
                    Resource = new ResourceInfoBasic
                    {
                        Description = "Demo of the x402 'batch-settlement' scheme: off-chain vouchers on a payment channel, batched into periodic on-chain settlements.",
                        ServiceName = "x402dev Batch Demo",
                        Tags = new List<string> { "demo", "batch-settlement", "payment-channel" },
                        IconUrl = IconUrl,
                    },
                    Discoverable = true
                },
                SettlementMode.Pessimistic);

            if (!x402Result.CanContinueRequest)
            {
                return null;
            }

            //Each request records an off-chain voucher of 0.001 USDC on the payment channel
            HttpContext.SetSettlementOverrides("1000");

            return new DemoResponse
            {
                Scheme = "batch-settlement",
                AuthorizedMax = "10000 (0.01 USDC channel deposit)",
                Charged = "1000 (0.001 USDC) per request, recorded as an off-chain voucher",
                Message = "Success! This request was recorded as an off-chain voucher on your payment channel. The ChannelManager batches vouchers into a single on-chain settlement and refunds unused balances of idle channels."
            };
        }
    }
}
