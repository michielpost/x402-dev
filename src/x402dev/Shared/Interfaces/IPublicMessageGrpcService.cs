using ProtoBuf.Grpc;
using System.ServiceModel;
using x402dev.Shared.Models;

namespace x402dev.Shared.Interfaces
{
    [ServiceContract]

    public interface IPublicMessageGrpcService
    {
        [OperationContract]
        Task<List<PublicMessage>> GetMessages(CallContext context = default);
    }
}
