using ProtoBuf.Grpc;
using x402dev.Services;
using x402dev.Shared.Interfaces;
using x402dev.Shared.Models;

namespace x402dev.Server.Services
{
    public class PublicMessageGrpcService(PublicMessagesService publicMessagesService) : IPublicMessageGrpcService
    {
        public async Task<List<PublicMessage>> GetMessages(CallContext context = default)
        {
            var result = await publicMessagesService.GetPublicMessagesAsync(10);

            return result.Select(pm => new PublicMessage
            {
                Asset = pm.Asset,
                CreatedDateTime = pm.CreatedDateTime.DateTime,
                Link = pm.Link,
                Message = pm.Message,
                Name = pm.Name,
                Network = pm.Network,
                Payer = pm.Payer,
                Transaction = pm.Transaction,
                Value = pm.Value,
            }).ToList();
        }
    }
}
