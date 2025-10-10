using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using x402.Coinbase;
using x402.Coinbase.Models;
using x402.Facilitator;
using x402dev.Web.Models;

namespace x402dev.Web.Services
{
    public class ContentService(IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        IOptions<CoinbaseOptions> coinbaseOptions,
        ILogger<ContentService> logger,
        ILoggerFactory loggerFactory)
    {
        private readonly string projectsCacheKey = "projects";
        private readonly string facilitatorsCacheKey = "facilitators";
        private readonly string facilitatorsETagCacheKey = "facilitators-etag";
        private readonly string githubBase = "https://raw.githubusercontent.com/michielpost/x402-dev/refs/heads/master/";
        private readonly SemaphoreSlim _testLock = new(1, 1);

        public async Task Initialize()
        {
            var facilitatorJson = await GetContentAsync("facilitators.json");
            var facilitators = System.Text.Json.JsonSerializer.Deserialize<List<FacilitatorData>>(facilitatorJson);

            var projects = await GetContentAsync("Projects.md");


            memoryCache.Set(facilitatorsCacheKey, facilitators);
            memoryCache.Set(projectsCacheKey, projects);

            Task.Run(() => UpdateFromGithub());
        }

        private async Task<string> GetContentAsync(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Content", fileName);
            string text = await File.ReadAllTextAsync(path);

            return text;
        }

        public async Task UpdateFromGithub()
        {
            return;
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
                var etag = response.Headers.ETag?.Tag;

                var cachedETag = GetFromCache<string?>(facilitatorsETagCacheKey);

                if (cachedETag == null || (etag != cachedETag))
                {
                    logger.LogInformation("Facilitators content updated from GitHub.");

                    memoryCache.Set(facilitatorsETagCacheKey, etag);
                    var facilitators = System.Text.Json.JsonSerializer.Deserialize<List<FacilitatorData>>(facilitatorJson);
                    memoryCache.Set(facilitatorsCacheKey, facilitators);

                    await TestFacilitators();

                }

                var projects = await httpClient.GetStringAsync(githubBase + "Projects.md");
                if (!string.IsNullOrWhiteSpace(projects))
                {
                    memoryCache.Set(projectsCacheKey, projects);
                }
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
            if (!await _testLock.WaitAsync(0)) // immediately check if another run is active
            {
                logger.LogInformation("TestFacilitators is already running. Skipping concurrent execution.");
                return;
            }

            try
            {

                var cachedFacilitators = await GetFacilitators();
                var facilitators = cachedFacilitators.Select(f => f with { }).ToList();

                var toCheck = facilitators
                    .Where(x => !x.NeedsApiKey || x.Name == "Coinbase")
                    .Where(x => !x.NextCheck.HasValue
                    || x.NextCheck.Value <= DateTime.UtcNow).ToList();

                foreach (var facilitator in toCheck)
                {
                    try
                    {
                        facilitator.HasError = false;
                        facilitator.Checked = DateTimeOffset.UtcNow;
                        facilitator.NextCheck = DateTimeOffset.UtcNow.AddMinutes(new Random().Next(10, 21));

                        var httpClient = httpClientFactory.CreateClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        httpClient.BaseAddress = new Uri(facilitator.Url);

                        var facilitatorClient = new HttpFacilitatorClient(httpClient, loggerFactory.CreateLogger<HttpFacilitatorClient>());
                        
                        if(facilitator.Name == "Coinbase")
                        {
                            //Use Coinbase Facilitator client with API keys
                            facilitatorClient = new CoinbaseFacilitatorClient(httpClient, coinbaseOptions);
                        }
                        
                        var kinds = await facilitatorClient.SupportedAsync();

                        facilitator.Kinds = kinds;

                    }
                    catch (Exception ex)
                    {
                        facilitator.HasError = true;
                        facilitator.NextCheck = DateTimeOffset.UtcNow.AddMinutes(1);

                        facilitator.ErrorMessage = $"Error accessing facilitator {facilitator.Name} url {facilitator.Url}";

                        logger.LogError(ex, $"Error accessing facilitator {facilitator.Name} url {facilitator.Url}");
                    }
                }

                memoryCache.Set(facilitatorsCacheKey, facilitators);

            }
            finally
            {
                _testLock.Release();
            }

        }
    }
}
