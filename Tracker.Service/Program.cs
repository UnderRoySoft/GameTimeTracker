using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Tracker.Service.Bootstrap;

namespace Tracker.Service
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Configure Serilog early so we get logs from startup failures too.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: "Logs\\service-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                .CreateLogger();

            try
            {
                Log.Information("Starting Tracker.Service host");

                var host = Host.CreateDefaultBuilder(args)
                    // If you later run as a Windows Service, keep this:
                    //.UseWindowsService()
                    .UseSerilog() // plug Serilog into the Host logging pipeline
                    .ConfigureServices((context, services) =>
                    {
                        // Centralized DI registration
                        ServiceBootstrap.Register(services);
                    })
                    .Build();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Tracker.Service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
