using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RIN.Core.DB;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RIN.WebAPI.Utils
{
    public class CharacterCleanupService : BackgroundService
    {
        private readonly ILogger<CharacterCleanupService> _logger;
        private readonly DB _db;

        public CharacterCleanupService(ILogger<CharacterCleanupService> logger, DB db)
        {
            _logger = logger;
            _db = db;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Serilog.Log.Information("Character Cleanup Service is starting.");

            // Use a PeriodicTimer (available since .NET 6) to run every minute
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            try
            {
                // Process immediately on start if needed, or wait for first tick
                // await _db.ProcessCharacterDeletionQueue();

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    Serilog.Log.Information("Processing character deletion queue...");
                    await _db.ProcessCharacterDeletionQueue();
                }
            }
            catch (OperationCanceledException)
            {
                Serilog.Log.Information("Character Cleanup Service is stopping.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "An error occurred in Character Cleanup Service.");
            }
        }
    }
}
