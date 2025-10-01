using Alt_Support.Configuration;
using Alt_Support.Services;
using Microsoft.Extensions.Options;

namespace Alt_Support.Services
{
    public class HistoricalDataSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ApplicationConfiguration _config;
        private readonly ILogger<HistoricalDataSyncService> _logger;

        public HistoricalDataSyncService(
            IServiceScopeFactory serviceScopeFactory,
            IOptions<ApplicationConfiguration> config,
            ILogger<HistoricalDataSyncService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _config = config.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.EnableHistoricalDataSync)
            {
                _logger.LogInformation("Historical data sync is disabled");
                return;
            }

            _logger.LogInformation("Historical data sync service started");

            // Initial sync on startup
            await PerformSyncAsync();

            // Periodic sync
            var interval = TimeSpan.FromHours(_config.HistoricalDataSyncIntervalHours);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);
                    
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await PerformSyncAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in historical data sync loop");
                    // Continue the loop even if there's an error
                }
            }

            _logger.LogInformation("Historical data sync service stopped");
        }

        private async Task PerformSyncAsync()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<ITicketAnalysisService>();

                _logger.LogInformation("Starting scheduled historical data sync");
                await analysisService.SyncHistoricalDataAsync();
                _logger.LogInformation("Completed scheduled historical data sync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled historical data sync");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Historical data sync service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}