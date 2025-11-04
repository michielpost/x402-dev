using Microsoft.EntityFrameworkCore;
using x402dev.Database;
using x402dev.Database.Models;

namespace x402dev.Services
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
