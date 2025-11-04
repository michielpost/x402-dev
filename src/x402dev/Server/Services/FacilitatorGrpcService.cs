using ProtoBuf.Grpc;
using x402dev.Services;
using x402dev.Shared.Interfaces;
using x402dev.Shared.Models;

namespace x402dev.Server.Services
{
    public class FacilitatorGrpcService(ContentService contentService) : IFacilitatorGrpcService
    {
        public async Task<List<Facilitator>> GetFacilitators(CallContext context = default)
        {
            var facilitators = (await contentService.GetFacilitators()).Where(x => x.Checked.HasValue);

            return facilitators.Select(f => new Facilitator
            {
                Name = f.Name,
                Url = f.Url,
                NeedsApiKey = f.NeedsApiKey,
                Checked = f.Checked.GetValueOrDefault().DateTime,
                Comments = f.Comments,
                ErrorCount = f.ErrorCount,
                ErrorMessage = f.ErrorMessage,
                HasError = f.HasError,
                Kinds = f.Kinds.Select(x => x.Network).ToList(),
                NextCheck = f.NextCheck.GetValueOrDefault().DateTime
            }).ToList();
        }
    }
}
