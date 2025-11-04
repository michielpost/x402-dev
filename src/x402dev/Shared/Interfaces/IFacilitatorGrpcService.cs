using ProtoBuf.Grpc;
using System.ServiceModel;
using x402dev.Shared.Models;

namespace x402dev.Shared.Interfaces
{
    [ServiceContract]

    public interface IFacilitatorGrpcService
    {
        [OperationContract]
        Task<List<Facilitator>> GetFacilitators(CallContext context = default);
    }
}
