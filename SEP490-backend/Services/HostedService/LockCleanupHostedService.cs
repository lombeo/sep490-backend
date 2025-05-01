using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sep490_Backend.Services.ConstructionPlanService;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sep490_Backend.Services.HostedService
{
    public class LockCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LockCleanupHostedService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

        public LockCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<LockCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Lock Cleanup Hosted Service running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredLocks();
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is shutting down
                    _logger.LogInformation("Lock Cleanup Hosted Service shutting down.");
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue running
                    _logger.LogError(ex, "Error occurred in Lock Cleanup Hosted Service.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task CleanupExpiredLocks()
        {
            _logger.LogInformation("Cleaning up expired locks at {time}", DateTimeOffset.Now);

            using (var scope = _serviceProvider.CreateScope())
            {
                var lockService = scope.ServiceProvider.GetRequiredService<IPlanEditLockService>();
                await lockService.CleanupExpiredLocks();
            }

            _logger.LogInformation("Finished cleaning up expired locks at {time}", DateTimeOffset.Now);
        }
    }
} 