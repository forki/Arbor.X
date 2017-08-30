﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Arbor.Processing.Core;
using Arbor.X.Core.BuildVariables;
using Arbor.X.Core.IO;
using Arbor.X.Core.Logging;
using Arbor.X.Core.Tools.Testing;
using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Tests.MSpec
{
    [Subject(typeof(MSpecTestRunner))]
    [Tags(MSpecInternalConstants.RecursiveArborXTest)]
    public class when_running_mspec_on_self
    {
        private static MSpecTestRunner testRunner;
        private static readonly List<IVariable> variables = new List<IVariable>();
        private static ExitCode ExitCode;
        private static string mspecReports;

        private static ExitCode exitCode;
        private static DirectoryInfo tempDirectory;

        private Cleanup after = () =>
        {
            Thread.Sleep(1000);
            try
            {
                tempDirectory.DeleteIfExists(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not cleanup test directory, " + ex);
            }
        };

        private Establish context = () =>
        {
            string root = Path.Combine(VcsTestPathHelper.FindVcsRootPath(), "src");

            string combine = Path.Combine(root, "Arbor.X.Tests.Integration", "bin", "debug");

            string tempPath = Path.Combine(Path.GetTempPath(),
                $"{DefaultPaths.TempPathPrefix}_mspec_self_{DateTime.Now.ToString("yyyyMMddHHmmssfff_")}{Guid.NewGuid().ToString().Substring(0, 8)}");

            tempDirectory = new DirectoryInfo(tempPath).EnsureExists();

            DirectoryInfo binDirectory = tempDirectory.CreateSubdirectory("bin");

            exitCode = DirectoryCopy.CopyAsync(combine,
                    binDirectory.FullName,
                    pathLookupSpecificationOption: new PathLookupSpecification())
                .Result;

            using (File.Create(Path.Combine(tempDirectory.FullName, ".gitattributes.")))
            {
            }

            testRunner = new MSpecTestRunner();
            variables.Add(new EnvironmentVariable(WellKnownVariables.ExternalTools,
                Path.Combine(VcsTestPathHelper.FindVcsRootPath(), "tools", "external")));

            variables.Add(new EnvironmentVariable(WellKnownVariables.SourceRootOverride, tempDirectory.FullName));
            variables.Add(new EnvironmentVariable(WellKnownVariables.SourceRoot, tempDirectory.FullName));

            mspecReports = Path.Combine(tempDirectory.FullName, "MSpecReports");

            new DirectoryInfo(mspecReports).EnsureExists();

            variables.Add(new EnvironmentVariable(WellKnownVariables.ExternalTools_MSpec_ReportPath, mspecReports));
            variables.Add(new EnvironmentVariable(WellKnownVariables.RunTestsInReleaseConfigurationEnabled, "false"));
        };

        private Because of =
            () =>
                ExitCode =
                    testRunner.ExecuteAsync(new ConsoleLogger { LogLevel = LogLevel.Verbose },
                            variables,
                            new CancellationToken())
                        .Result;

        private It shoud_have_created_html_report = () =>
        {
            var reports = new DirectoryInfo(mspecReports);
            DirectoryInfo htmlDirectory = reports.GetDirectories()
                .SingleOrDefault(dir => dir.Name.Equals("html", StringComparison.InvariantCultureIgnoreCase));

            FileInfo[] files = reports.GetFiles("*.html", SearchOption.AllDirectories);

            foreach (FileInfo fileInfo in files)
            {
                Console.WriteLine(fileInfo.FullName);
            }

            htmlDirectory.ShouldNotBeNull();
        };

        private It shoud_have_created_xml_report = () =>
        {
            var reports = new DirectoryInfo(mspecReports);

            FileInfo[] files = reports.GetFiles("*.xml", SearchOption.AllDirectories);

            foreach (FileInfo fileInfo in files)
            {
                Console.WriteLine(fileInfo.FullName);
            }

            files.Length.ShouldNotEqual(0);
        };

        private It should_return_a_successful_exit_code = () => ExitCode.IsSuccess.ShouldBeTrue();
    }
}
