using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NestConsole.GoogleServices;
using NestConsole.Settings;
using NestConsole.Extensions;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace NestConsole
{
    public class Program
    {
        private static void Main(string[] args)
        {
            // Configuration
            var configurationBuilder = new ConfigurationBuilder();
            Startup.ConfigureAppConfiguration(configurationBuilder);
            IConfiguration configuration = configurationBuilder.Build();

            // Set up DI
            IServiceCollection services = new ServiceCollection();
            var startup = new Startup(configuration);
            startup.ConfigureDiServices(services);
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            // UI
            Console.WriteLine("+-------------+");
            Console.WriteLine("| NestKeeping |");
            Console.WriteLine("+-------------+\n");

            // Google authentication
            using (var scope = serviceProvider.CreateScope())
            {
                IOAuthService oAuthService = scope.ServiceProvider.GetService<IOAuthService>();
                var signedIn = oAuthService.SignIn();
                if (!signedIn)
                {
                    Console.WriteLine("Sign in unsuccessful");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }

            // Start Workers
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}