using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NestConsole.GoogleServices;
using NestConsole.Settings;

namespace NestConsole
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static void ConfigureAppConfiguration(IConfigurationBuilder configuration)
        {
            configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureDiServices(services);
            services.AddHostedService<CameraWorker>();
        }

        public void ConfigureDiServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole());
            services.AddOptions();
            services
                .Configure<GoogleOAuthClientSettings>(_configuration.GetSection("GoogleOAuthClient"))
                .Configure<NestDeviceAccessSettings>(_configuration.GetSection("NestDeviceAccess"));

            services.AddScoped<IOAuthService, OAuthService>();
        }
    }
}