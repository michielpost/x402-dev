using x402dev.Web.Services;

namespace x402dev.Web.HostedServices
{
    public class ContentSyncBackgroundService : IHostedService, IDisposable
    {
        private int isBusy = 0;
        private readonly ILogger<ContentSyncBackgroundService> _logger;
        private readonly IServiceProvider services;
        private Timer? _timer;

        public ContentSyncBackgroundService(ILogger<ContentSyncBackgroundService> logger, IServiceProvider services)
        {
            _logger = logger;
            this.services = services;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(ContentSyncBackgroundService)} running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(10));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            //Only run one at a time
            if (System.Threading.Interlocked.CompareExchange(ref this.isBusy, 1, 0) == 1)
            {
                return;
            }

            try
            {
                using (var scope = services.CreateScope())
                {
                    var scopedProcessingService =
                        scope.ServiceProvider
                            .GetRequiredService<ContentService>();

                    await scopedProcessingService.UpdateFromGithub();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ContentSyncBackgroundService)} failed.", ex);
            }
            finally
            {
                this.isBusy = 0;
            }

        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(ContentSyncBackgroundService)} stopping.");

            _timer?.Change(Timeout.Infinite, 0);
            this.isBusy = 0;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
