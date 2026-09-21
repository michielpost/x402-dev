using x402dev.Services;

namespace x402dev.Server.HostedServices
{
    public class X402ApiCheckBackgroundService : IHostedService, IDisposable
    {
        private int isBusy = 0;
        private readonly ILogger<X402ApiCheckBackgroundService> _logger;
        private readonly IServiceProvider services;
        private Timer? _timer;

        public X402ApiCheckBackgroundService(ILogger<X402ApiCheckBackgroundService> logger, IServiceProvider services)
        {
            _logger = logger;
            this.services = services;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(X402ApiCheckBackgroundService)} running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(5));

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
                            .GetRequiredService<X402ApiService>();

                    await scopedProcessingService.CheckDueApisAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(X402ApiCheckBackgroundService)} failed.");
            }
            finally
            {
                this.isBusy = 0;
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(X402ApiCheckBackgroundService)} stopping.");

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
