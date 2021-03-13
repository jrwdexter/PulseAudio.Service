using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace PulseAudio.ServiceWrapper
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ServiceConfigInfo _serviceConfigInfo;
        private Process _processInfo;

        public Worker(IConfiguration config, ILogger<Worker> logger)
        {
            _serviceConfigInfo = config.GetSection("ServiceInformation").Get<ServiceConfigInfo>();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_processInfo == null)
            {
                var servicePath = Path.GetFullPath(_serviceConfigInfo.Service);
                var executableFile = new FileInfo(servicePath);

                if (executableFile.Directory?.Parent == null)
                    throw new FileNotFoundException("Could not find file to launch.");
                Debug.Assert(executableFile.DirectoryName != null, "executableFile.DirectoryName != null");

                var rootPath = executableFile.Directory.Parent.FullName;
                _logger.LogInformation($"Starting application {servicePath}");
                _logger.LogInformation($"Root path: {rootPath}");
                var arguments = $"-p \"{rootPath}\\lib\\pulse-1.1\\modules\" -nF \"{rootPath}\\etc\\pulse\\default.pa\"";
                _logger.LogInformation($"Launching pulse audio with args: {arguments}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = servicePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = executableFile.DirectoryName,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                _processInfo = Process.Start(startInfo);
                if (_processInfo == null)
                    throw new ApplicationException("Could not start Pulse Audio");
                _logger.LogInformation("Process started with ID: {Id}", _processInfo.Id);
                Thread.Sleep(30000);
            }
            while (!stoppingToken.IsCancellationRequested && !_processInfo.HasExited)
            {
                if (!_processInfo.Responding)
                    throw new ApplicationException(_processInfo.StandardError.ReadToEnd());
                var stdLine = await _processInfo.StandardOutput.ReadLineAsync();
                var errorLine = await _processInfo.StandardError.ReadLineAsync();
                if (!string.IsNullOrEmpty(stdLine) || !string.IsNullOrEmpty(errorLine))
                {
                    var line = errorLine ?? stdLine;
                    var (levelChar, message) = (line.Substring(0, 1), line.Substring(3));
                    LogLevel level;
                    switch (levelChar)
                    {
                        case "E":
                            level = LogLevel.Error;
                            break;
                        case "W":
                            level = LogLevel.Warning;
                            break;
                        case "I":
                            level = LogLevel.Information;
                            break;
                        case "D":
                            level = LogLevel.Debug;
                            break;
                        case "T":
                            level = LogLevel.Trace;
                            break;
                        default:
                            level = LogLevel.Information;
                            break;
                    }

                    _logger.Log(level, message);
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            TryCloseSubprocess();
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            TryCloseSubprocess();
            Log.CloseAndFlush();
            base.Dispose();
        }

        private void TryCloseSubprocess()
        {
            if (_processInfo != null && !_processInfo.HasExited)
                _processInfo.Close();
        }
    }
}
