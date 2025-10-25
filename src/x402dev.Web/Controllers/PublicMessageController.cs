using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;
using x402;
using x402.Core.Enums;
using x402.Core.Models;
using x402dev.Web.Data;
using x402dev.Web.Models;

namespace x402dev.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PublicMessageController : ControllerBase
    {
        private readonly ApplicationDbContext dbContext;
        private readonly X402Handler x402Handler;

        public PublicMessageController(ApplicationDbContext dbContext, X402Handler x402Handler)
        {
            this.dbContext = dbContext;
            this.x402Handler = x402Handler;
        }


        /// <summary>
        /// Post a message on x402dev.com.
        /// A payment of 0.1 USDC is required using the x402 protocol.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("send-msg")]
        [SwaggerRequestExample(typeof(PublicMessageRequest), typeof(PublicMessageRequestExample))]
        public async Task<PublicMessageResponse?> SendMsg([FromBody] PublicMessageRequest req)
        {
            var payReq = new PaymentRequirementsBasic
            {
                //Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e", //Testnet
                Asset = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913", //Mainnet
                Description = "Publish a public message on x402dev.com",
                MaxAmountRequired = "100000",
                PayTo = "0x7D95514aEd9f13Aa89C8e5Ed9c29D08E8E9BfA37",
            };

            var x402Result = await x402Handler.HandleX402Async(
                payReq,
                discoverable: true,
                SettlementMode.Pessimistic,
                onSetOutputSchema: (context, reqs, schema) =>
                {
                    schema.Input ??= new();

                    schema.Input.Method = "POST";
                    schema.Input.BodyType = "json";

                    //Manually set the input schema
                    schema.Input.BodyFields = new Dictionary<string, object>
                    {
                        {
                            nameof(req.Name),
                            new FieldDefenition
                            {
                                Required = false,
                                Description = "Sender name (max length: 32)",
                                Type = "string"
                            }
                        },
                        {
                            nameof(req.Message),
                            new FieldDefenition
                            {
                                Required = true,
                                Description = "Message to publish (max length: 255)",
                                Type = "string"
                            }
                        },
                        {
                            nameof(req.Link),
                            new FieldDefenition
                            {
                                Required = false,
                                Description = "Optional URL to show (max length: 255)",
                                Type = "string"
                            }
                        }
                    };

                    return schema;
                });

            if (!x402Result.CanContinueRequest)
            {
                return null;
            }

            try
            {
                //Save message to db
                var publicMessage = new PublicMessage
                {
                    Name = req.Name,
                    Link = req.Link,
                    Message = req.Message,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    Payer = x402Result.VerificationResponse?.Payer,
                    Asset = payReq.Asset,
                    Network = x402Result.SettlementResponse?.Network,
                    Transaction = x402Result.SettlementResponse?.Transaction,
                    Value = payReq.MaxAmountRequired
                };

                dbContext.PublicMessages.Add(publicMessage);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return new PublicMessageResponse { Success = false, Error = ex.Message };
            }

            return new PublicMessageResponse { Success = true };
        }

    }
}
