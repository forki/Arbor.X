﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Arbor.X.Core.BuildVariables;
using Arbor.X.Core.GenericExtensions;
using Arbor.X.Core.Logging;
using Arbor.X.Core.ProcessUtils;

namespace Arbor.X.Core.Tools.NuGet
{
    public class NuGetHelper
    {
        private readonly ILogger _logger;

        public NuGetHelper(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<string> EnsureNuGetExeExistsAsync(string exeUri, CancellationToken cancellationToken)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetFile = Path.Combine(baseDir, "nuget.exe");

            const int MaxRetries = 6;

            var currentExePath = new FileInfo(targetFile);

            if (!File.Exists(targetFile))
            {
                string parentExePath = Path.Combine(currentExePath.Directory.Parent.FullName, currentExePath.Name);
                if (File.Exists(parentExePath))
                {
                    _logger.Write($"Found NuGet in path '{parentExePath}', skipping download");
                    return parentExePath;
                }

                _logger.Write($"'{targetFile}' does not exist, will try to download from nuget.org");

                var uris = new List<string>();

                Uri userUri;
                if (!string.IsNullOrWhiteSpace(exeUri) && Uri.TryCreate(exeUri, UriKind.Absolute, out userUri))
                {
                    uris.Add(exeUri);
                }

                uris.Add("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe");
                uris.Add("https://nuget.org/nuget.exe");
                uris.Add("https://www.nuget.org/nuget.exe");

                for (int i = 0; i < MaxRetries; i++)
                {
                    try
                    {
                        string nugetExeUri = uris[i % uris.Count];

                        await DownloadNuGetExeAsync(baseDir, targetFile, nugetExeUri, cancellationToken);

                        return targetFile;
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteError(string.Format("Attempt {1}. Could not download nuget.exe. {0}", ex, i + 1));
                    }

                    const int WaitTimeInSeconds = 1;

                    _logger.Write($"Waiting {WaitTimeInSeconds} seconds to try again");

                    await Task.Delay(TimeSpan.FromSeconds(WaitTimeInSeconds), cancellationToken);
                }
            }

            bool update = Environment.GetEnvironmentVariable(WellKnownVariables.NuGetVersionUpdatedEnabled)
                .TryParseBool(false);

            if (update)
            {
                try
                {
                    var arguments = new List<string> { "update", "-self" };
                    await ProcessHelper.ExecuteAsync(
                        targetFile,
                        arguments,
                        _logger,
                        addProcessNameAsLogCategory: true,
                        addProcessRunnerCategory: true,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.WriteError(ex.ToString());
                }
            }

            return targetFile;
        }

        private async Task DownloadNuGetExeAsync(
            string baseDir,
            string targetFile,
            string nugetExeUri,
            CancellationToken cancellationToken)
        {
            string tempFile = Path.Combine(baseDir, $"nuget.exe.{Guid.NewGuid()}.tmp");

            _logger.WriteVerbose($"Downloading {nugetExeUri} to {tempFile}");
            try
            {
                using (var client = new HttpClient())
                {
                    using (Stream stream = await client.GetStreamAsync(nugetExeUri))
                    {
                        using (var fs = new FileStream(tempFile, FileMode.Create))
                        {
                            await stream.CopyToAsync(fs, 4096, cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
                {
                    File.Copy(tempFile, targetFile, true);
                    _logger.WriteVerbose($"Copied {tempFile} to {targetFile}");
                    File.Delete(tempFile);
                    _logger.WriteVerbose($"Deleted temp file {tempFile}");
                }
            }
        }
    }
}
