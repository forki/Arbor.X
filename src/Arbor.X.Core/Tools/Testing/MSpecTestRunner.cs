﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Arbor.Defensive.Collections;
using Arbor.Processing;
using Arbor.Processing.Core;
using Arbor.X.Core.BuildVariables;
using Arbor.X.Core.IO;
using Arbor.X.Core.Logging;
using Arbor.X.Core.Properties;
using Machine.Specifications;

namespace Arbor.X.Core.Tools.Testing
{
    [Priority(450)]
    public class MSpecTestRunner : ITool
    {
        public async Task<ExitCode> ExecuteAsync(
            ILogger logger,
            IReadOnlyCollection<IVariable> buildVariables,
            CancellationToken cancellationToken)
        {
            bool enabled = buildVariables.GetBooleanByKey(WellKnownVariables.MSpecEnabled, true);

            if (!enabled)
            {
                logger.WriteWarning($"{MachineSpecificationsConstants.MachineSpecificationsName} not enabled");
                return ExitCode.Success;
            }

            string externalToolsPath =
                buildVariables.Require(WellKnownVariables.ExternalTools).ThrowIfEmptyValue().Value;

            string sourceRoot =
                buildVariables.Require(WellKnownVariables.SourceRoot).ThrowIfEmptyValue().Value;

            string testReportDirectoryPath =
                buildVariables.Require(WellKnownVariables.ExternalTools_MSpec_ReportPath).ThrowIfEmptyValue().Value;

            string sourceRootOverride =
                buildVariables.GetVariableValueOrDefault(WellKnownVariables.SourceRootOverride, string.Empty);

            string sourceDirectoryPath;

            if (string.IsNullOrWhiteSpace(sourceRootOverride) || !Directory.Exists(sourceRootOverride))
            {
                if (sourceRoot == null)
                {
                    throw new InvalidOperationException("Source root cannot be null");
                }

                sourceDirectoryPath = sourceRoot;
            }
            else
            {
                sourceDirectoryPath = sourceRootOverride;
            }

            var directory = new DirectoryInfo(sourceDirectoryPath);
            string mspecExePath = Path.Combine(
                externalToolsPath,
                MachineSpecificationsConstants.MachineSpecificationsName,
                "mspec-clr4.exe");

            bool runTestsInReleaseConfiguration =
                buildVariables.GetBooleanByKey(
                    WellKnownVariables.RunTestsInReleaseConfigurationEnabled,
                    true);

            IEnumerable<Type> typesToFind = new List<Type>
            {
                typeof(It),
                typeof(BehaviorsAttribute),
                typeof(SubjectAttribute),
                typeof(Behaves_like<>)
            };

            logger.WriteVerbose(
                $"Scanning directory '{directory.FullName}' for assemblies containing Machine.Specifications tests");

            string assemblyFilePrefix = buildVariables.GetVariableValueOrDefault(WellKnownVariables.TestsAssemblyStartsWith, string.Empty);

            List<string> testDlls =
                new UnitTestFinder(typesToFind, logger: logger)
                    .GetUnitTestFixtureDlls(directory, runTestsInReleaseConfiguration, assemblyFilePrefix, FrameworkConstants.NetFramework)
                    .ToList();

            if (!testDlls.Any())
            {
                logger.WriteWarning(
                    $"No DLL files with {MachineSpecificationsConstants.MachineSpecificationsName} specifications was found");
                return ExitCode.Success;
            }

            var arguments = new List<string>();

            arguments.AddRange(testDlls);

            arguments.Add("--xml");
            string timestamp = DateTime.UtcNow.ToString("O").Replace(":", ".");
            string fileName = "MSpec_" + timestamp + ".xml";
            string xmlReportPath = Path.Combine(testReportDirectoryPath, "Xml", fileName);

            new FileInfo(xmlReportPath).Directory.EnsureExists();

            arguments.Add(xmlReportPath);
            string htmlPath = Path.Combine(testReportDirectoryPath, "Html", "MSpec_" + timestamp);

            new DirectoryInfo(htmlPath).EnsureExists();

            IReadOnlyCollection<string> excludedTags = buildVariables
                .GetVariableValueOrDefault(
                    WellKnownVariables.IgnoredTestCategories,
                    string.Empty)
                .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToReadOnlyCollection();

            arguments.Add("--html");
            arguments.Add(htmlPath);

            bool hasArborTestDll =
                testDlls.Any(dll => dll.IndexOf("arbor", StringComparison.InvariantCultureIgnoreCase) >= 0);

            if (hasArborTestDll || excludedTags.Any())
            {
                var allExcludedTags = new List<string>();

                arguments.Add("--exclude");

                if (hasArborTestDll)
                {
                    allExcludedTags.Add(MSpecInternalConstants.RecursiveArborXTest);
                }

                if (excludedTags.Any())
                {
                    allExcludedTags.AddRange(excludedTags);
                }

                string excludedTagsParameter = string.Join(",", allExcludedTags);

                logger.Write($"Running MSpec with excluded tags: {excludedTagsParameter}");

                arguments.Add(excludedTagsParameter);
            }

            // ReSharper disable once CollectionNeverUpdated.Local
            var environmentVariables = new Dictionary<string, string>();

            ExitCode exitCode = await
                ProcessRunner.ExecuteAsync(
                    mspecExePath,
                    arguments: arguments,
                    cancellationToken: cancellationToken,
                    standardOutLog: logger.Write,
                    standardErrorAction: logger.WriteError,
                    toolAction: logger.Write,
                    verboseAction: logger.WriteVerbose,
                    environmentVariables: environmentVariables,
                    debugAction: logger.WriteDebug);

            if (buildVariables.GetBooleanByKey(
                WellKnownVariables.MSpecJUnitXslTransformationEnabled,
                false))
            {
                logger.WriteVerbose(
                    $"Transforming {MachineSpecificationsConstants.MachineSpecificationsName} test reports to JUnit format");

                const string junitSuffix = "_junit.xml";

                DirectoryInfo xmlReportDirectory = new FileInfo(xmlReportPath).Directory;

// ReSharper disable once PossibleNullReferenceException
                IReadOnlyCollection<FileInfo> xmlReports = xmlReportDirectory
                    .GetFiles("*.xml")
                    .Where(report => !report.Name.EndsWith(junitSuffix, StringComparison.Ordinal))
                    .ToReadOnlyCollection();

                if (xmlReports.Any())
                {
                    Encoding encoding = Encoding.UTF8;
                    using (Stream stream = new MemoryStream(encoding.GetBytes(MSpecJUnitXsl.Xml)))
                    {
                        using (XmlReader xmlReader = new XmlTextReader(stream))
                        {
                            var myXslTransform = new XslCompiledTransform();
                            myXslTransform.Load(xmlReader);

                            foreach (FileInfo xmlReport in xmlReports)
                            {
                                logger.WriteDebug($"Transforming '{xmlReport.FullName}' to JUnit XML format");
                                try
                                {
                                    TransformReport(xmlReport, junitSuffix, encoding, myXslTransform, logger);
                                }
                                catch (Exception ex)
                                {
                                    logger.WriteError($"Could not transform '{xmlReport.FullName}', {ex}");
                                    return ExitCode.Failure;
                                }

                                logger.WriteDebug(
                                    $"Successfully transformed '{xmlReport.FullName}' to JUnit XML format");
                            }
                        }
                    }
                }
            }

            return exitCode;
        }

        private static void TransformReport(
            FileInfo xmlReport,
            string junitSuffix,
            Encoding encoding,
            XslCompiledTransform myXslTransform,
            ILogger logger)
        {
            // ReSharper disable once PossibleNullReferenceException
            string resultFile = Path.Combine(
                xmlReport.Directory.FullName,
                $"{Path.GetFileNameWithoutExtension(xmlReport.Name)}{junitSuffix}");

            if (File.Exists(resultFile))
            {
                logger.Write(
                    $"Skipping XML transformation for '{xmlReport.FullName}', the transformation result file '{resultFile}' already exists");
                return;
            }

            using (var fileStream = new FileStream(xmlReport.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var streamReader = new StreamReader(fileStream, encoding))
                {
                    using (XmlReader reportReader = XmlReader.Create(streamReader))
                    {
                        using (var outStream = new FileStream(resultFile, FileMode.Create, FileAccess.Write))
                        {
                            using (XmlWriter reportWriter = new XmlTextWriter(outStream, encoding))
                            {
                                myXslTransform.Transform(reportReader, reportWriter);
                            }
                        }
                    }
                }
            }

            File.Delete(xmlReport.FullName);
        }
    }
}
