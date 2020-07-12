using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            while (!stoppingToken.IsCancellationRequested)
            {
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

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var servicePath = Path.GetFullPath(_serviceConfigInfo.Service);
            _logger.LogInformation($"Starting application {servicePath} .");

            var startInfo = new ProcessStartInfo(servicePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            _processInfo = Process.Start(startInfo);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_processInfo.HasExited)
            {
                _processInfo.Close();
            }
            return base.StopAsync(cancellationToken);
        }
    }

    public class PulseOutputLogger : StreamWriter
    {
        public PulseOutputLogger(Stream stream) : base(stream)
        {
        }

        public PulseOutputLogger(Stream stream, Encoding encoding) : base(stream, encoding)
        {
        }

        public PulseOutputLogger(Stream stream, Encoding encoding, int bufferSize) : base(stream, encoding, bufferSize)
        {
        }

        public PulseOutputLogger(Stream stream, Encoding? encoding = null, int bufferSize = -1, bool leaveOpen = false) : base(stream, encoding, bufferSize, leaveOpen)
        {
        }

        public PulseOutputLogger(string path) : base(path)
        {
        }

        public PulseOutputLogger(string path, bool append) : base(path, append)
        {
        }

        public PulseOutputLogger(string path, bool append, Encoding encoding) : base(path, append, encoding)
        {
        }

        public PulseOutputLogger(string path, bool append, Encoding encoding, int bufferSize) : base(path, append, encoding, bufferSize)
        {
        }
    }
}
