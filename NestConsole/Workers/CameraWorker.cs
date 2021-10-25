using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NestConsole.GoogleServices;
using NestConsole.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NestConsole
{
    public class CameraWorker : BackgroundService
    {
        private readonly ILogger<CameraWorker> _logger;
        private readonly IOAuthService _oAuth;

        public CameraWorker(ILogger<CameraWorker> logger, IOptions<GoogleOAuthClientSettings> _oAuthSettings , IOAuthService oAuth)
        {
            _logger = logger;
            _oAuth = oAuth;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
