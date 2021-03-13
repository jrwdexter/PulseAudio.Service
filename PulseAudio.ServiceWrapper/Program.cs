using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

namespace PulseAudio.ServiceWrapper
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("PulseAudio.Service.log")
                .WriteTo.Console()
                .CreateLogger();
            Log.Information("Starting up");
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(options =>
                {
                    options.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information);
                    options.AddProvider(new EventLogLoggerProvider());
                    options.AddSerilog();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>().Configure<EventLogSettings>(
                        config =>
                        {
                            config.LogName = "Pulse Audio";
                            config.SourceName = "Pulse Audio Source";
                        }
                    );
                });
    }
}
