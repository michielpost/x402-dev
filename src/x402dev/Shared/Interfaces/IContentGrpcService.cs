using ProtoBuf.Grpc;
using System.ServiceModel;
using x402dev.Shared.Models;

namespace x402dev.Shared.Interfaces
{
    [ServiceContract]

    public interface IContentGrpcService
    {
        [OperationContract]
        Task<ContentResponse> GetContent(CallContext context = default);
    }
}
