using Markdig;
using ProtoBuf.Grpc;
using x402dev.Services;
using x402dev.Shared.Interfaces;
using x402dev.Shared.Models;

namespace x402dev.Server.Services
{
    public class ContentGrpcService(ContentService contentService) : IContentGrpcService
    {
        public async Task<ContentResponse> GetContent(CallContext context = default)
        {
            var content = await contentService.GetProjects();
            var markdown = Markdown.ToHtml(content);

            return new ContentResponse
            {
                Text = markdown
            };
        }
    }
}
