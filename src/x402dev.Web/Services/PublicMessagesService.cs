using Microsoft.EntityFrameworkCore;
using x402dev.Web.Data;

namespace x402dev.Web.Services
{
    public class PublicMessagesService(ApplicationDbContext dbContext)
    {
        public Task<List<PublicMessage>> GetPublicMessagesAsync(int max = 50)
        {
            return dbContext.PublicMessages
                .OrderByDescending(pm => pm.CreatedDateTime)
                .Take(max)
                .ToListAsync();
        }
    }
}
