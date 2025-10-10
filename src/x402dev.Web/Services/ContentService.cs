using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using x402.Facilitator;
using x402dev.Web.Models;

namespace x402dev.Web.Services
{
    public class ContentService(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory, ILogger<ContentService> logger)
    {
        private readonly string projectsCacheKey = "projects";
        private readonly string facilitatorsCacheKey = "facilitators";
        private readonly string facilitatorsModifiedCacheKey = "facilitators-modified";
        private readonly string githubBase = "https://raw.githubusercontent.com/michielpost/x402-dev/refs/heads/master/";

        public async Task Initialize()
        {
            var facilitatorJson = await GetContentAsync("facilitators.json");
            var facilitators = System.Text.Json.JsonSerializer.Deserialize<List<FacilitatorData>>(facilitatorJson);

            var projects = await GetContentAsync("Projects.md");
            

            memoryCache.Set(facilitatorsCacheKey, facilitators);
            memoryCache.Set(projectsCacheKey, projects);

            await UpdateFromGithub();
        }

        private async Task<string> GetContentAsync(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Content", fileName);
            string text = await File.ReadAllTextAsync(path);

            return text;
        }

        public async Task UpdateFromGithub()
        {
            try
            {
                //make HttpClient request to get the latest content from GitHub
                var httpClient = httpClientFactory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Get, githubBase + "facilitators.json");
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Get the content as string
                string facilitatorJson = await response.Content.ReadAsStringAsync();

                // Try to get the last modified date
                DateTimeOffset? lastModified = response.Content.Headers.LastModified;

                var cachedLastModified = GetFromCache<DateTimeOffset?>(facilitatorsModifiedCacheKey);

                if(cachedLastModified == null || (lastModified.HasValue && lastModified > cachedLastModified))
                {
                    logger.LogInformation("Facilitators content updated from GitHub.");

                    memoryCache.Set(facilitatorsModifiedCacheKey, lastModified);
                    var facilitators = System.Text.Json.JsonSerializer.Deserialize<List<FacilitatorData>>(facilitatorJson);
                    memoryCache.Set(facilitatorsCacheKey, facilitators);

                    await TestFacilitators();

                }

                var projects = await httpClient.GetStringAsync(githubBase + "Projects.md");
                memoryCache.Set(projectsCacheKey, projects);
            }
            catch (Exception ex)
            {
                //log error
                logger.LogError(ex, "Error updating content from GitHub");
            }

        }

        public async Task<string> GetProjects()
        {
            var cached = GetFromCache<string>(projectsCacheKey);
            if (cached is not null)
            {
                return cached;
            }

            await Initialize();
            return GetFromCache<string>(projectsCacheKey) ?? string.Empty;
        }

        public async Task<List<FacilitatorData>> GetFacilitators()
        {
            var cached = GetFromCache<List<FacilitatorData>>(facilitatorsCacheKey);
            if (cached is not null)
            {
                return cached;
            }

            await Initialize();
            return GetFromCache<List<FacilitatorData>>(facilitatorsCacheKey) ?? new();
        }

        private T? GetFromCache<T>(string key)
        {
            return memoryCache.Get<T>(key);
        }

        public async Task TestFacilitators()
        {
            var facilitators = await GetFacilitators();

            foreach(var facilitator in facilitators
                .Where(x => !x.NeedsApiKey)
                .Where(x => !x.NextCheck.HasValue || x.NextCheck.Value <= DateTime.UtcNow))
            {
                try
                {
                    facilitator.HasError = false;
                    facilitator.Checked = DateTimeOffset.UtcNow;
                    facilitator.NextCheck = DateTimeOffset.UtcNow.AddMinutes(new Random().Next(10, 21));

                    var httpClient = httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri(facilitator.Url);

                    var facilitatorClient = new HttpFacilitatorClient(httpClient, NullLogger<HttpFacilitatorClient>.Instance);
                    var kinds = await facilitatorClient.SupportedAsync();

                    facilitator.Kinds = kinds;

                }
                catch (Exception ex)
                {
                    facilitator.HasError = true;
                    facilitator.ErrorMessage = $"Error accessing facilitator {facilitator.Name} url {facilitator.Url}";

                    logger.LogError(ex, $"Error accessing facilitator {facilitator.Name} url {facilitator.Url}");
                }
            }

            memoryCache.Set(facilitatorsCacheKey, facilitators);

        }
    }
}
