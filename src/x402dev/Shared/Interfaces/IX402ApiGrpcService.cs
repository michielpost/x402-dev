using ProtoBuf.Grpc;
using System.ServiceModel;
using x402dev.Shared.Models;

namespace x402dev.Shared.Interfaces
{
    [ServiceContract]

    public interface IX402ApiGrpcService
    {
        [OperationContract]
        Task<List<X402Api>> GetX402Apis(CallContext context = default);

        [OperationContract]
        Task<AddX402ApiResult> AddX402Api(AddX402ApiRequest request, CallContext context = default);

        [OperationContract]
        Task<GetX402ApiDetailResult> GetX402ApiDetail(GetX402ApiDetailRequest request, CallContext context = default);

        [OperationContract]
        Task<List<X402Api>> GetX402ApisByDomain(GetX402ApisByDomainRequest request, CallContext context = default);

        [OperationContract]
        Task<List<X402Api>> GetX402ApisWithProblems(CallContext context = default);

        [OperationContract]
        Task<X402ApiStats> GetX402ApiStats(CallContext context = default);
    }
}
