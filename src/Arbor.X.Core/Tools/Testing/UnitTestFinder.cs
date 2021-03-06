﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Arbor.X.Core.Assemblies;
using Arbor.X.Core.Logging;
using Mono.Cecil;

namespace Arbor.X.Core.Tools.Testing
{
    public class UnitTestFinder
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<Type> _typesToFind;

        public UnitTestFinder(IEnumerable<Type> typesesToFind, bool debugLogEnabled = false, ILogger logger = null)
        {
            _logger = logger ?? new NullLogger();
            _typesToFind = typesesToFind;
            DebugLogEnabled = debugLogEnabled;
        }

        private bool DebugLogEnabled { get; }

        public HashSet<string> GetUnitTestFixtureDlls(
            DirectoryInfo currentDirectory,
            bool? releaseBuild = null,
            string assemblyFilePrefix = null,
            string targetFrameworkPrefix = null)
        {
            if (currentDirectory == null)
            {
                throw new ArgumentNullException(nameof(currentDirectory));
            }

            string fullName = currentDirectory.FullName;

            if (!currentDirectory.Exists)
            {
                return new HashSet<string>();
            }

            var blacklisted = new List<string>
            {
                ".git",
                ".hg",
                ".svn",
                "obj",
                "build",
                "packages",
                "_ReSharper",
                "external",
                "artifacts",
                "temp",
                ".HistoryData",
                "LocalHistory",
                "_",
                ".",
                "NCrunch",
                ".vs"
            };

            bool isBlacklisted =
                blacklisted.Any(
                    blackListedItem =>
                        currentDirectory.Name.StartsWith(blackListedItem, StringComparison.InvariantCultureIgnoreCase));

            if (isBlacklisted)
            {
                _logger.WriteDebug($"Directory '{fullName}' is blacklisted");
                return new HashSet<string>();
            }

            string searchPattern = "*.dll";

            IEnumerable<FileInfo> filteredDllFiles = string.IsNullOrWhiteSpace(assemblyFilePrefix)
                ? currentDirectory.EnumerateFiles(searchPattern)
                : currentDirectory.EnumerateFiles(searchPattern)
                    .Where(file => file.Name.StartsWith(assemblyFilePrefix, StringComparison.OrdinalIgnoreCase));

            var ignoredNames = new List<string> { "ReSharper", "dotCover", "Microsoft" };

            List<(AssemblyDefinition, FileInfo)> assemblies = filteredDllFiles
                .Where(file => !file.Name.StartsWith("System", StringComparison.InvariantCultureIgnoreCase))
                .Where(file => !ignoredNames.Any(
                    name => file.Name.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) >= 0))
                .Select(dllFile => GetAssembly(dllFile, targetFrameworkPrefix))
                .Where(assembly => assembly.Item1 != null)
                .ToList();

            List<(AssemblyDefinition, FileInfo)> configurationFiltered;

            if (releaseBuild.HasValue && releaseBuild.Value)
            {
                configurationFiltered = assemblies.Where(assembly => !assembly.Item1.IsDebugAssembly()).ToList();
                _logger.WriteDebug("Filtered to only include release assemblies");
            }
            else if (releaseBuild.HasValue)
            {
                configurationFiltered = assemblies.Where(assembly => assembly.Item1.IsDebugAssembly()).ToList();
                _logger.WriteDebug("Filtered to only include release assemblies");
            }
            else
            {
                configurationFiltered = assemblies;
                _logger.WriteDebug("No debug/release filter is used");
            }

            IReadOnlyCollection<string> testFixtureAssemblies = UnitTestFixtureAssemblies(configurationFiltered);

            List<string> subDirAssemblies = currentDirectory
                .EnumerateDirectories()
                .SelectMany(dir => GetUnitTestFixtureDlls(dir, releaseBuild, assemblyFilePrefix, targetFrameworkPrefix))
                .ToList();

            List<string> allUnitFixtureAssemblies = testFixtureAssemblies
                .Concat(subDirAssemblies)
                .Distinct()
                .ToList();

            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string allUnitFixtureAssembly in allUnitFixtureAssemblies)
            {
                hashSet.Add(allUnitFixtureAssembly);
            }

            return hashSet;
        }

        public bool TryIsTypeTestFixture(TypeDefinition typeToInvestigate)
        {
            if (typeToInvestigate == null)
            {
                throw new ArgumentNullException(nameof(typeToInvestigate));
            }

            try
            {
                string toInvestigate = typeToInvestigate.FullName;
                bool any = IsTypeUnitTestFixture(typeToInvestigate);

                if (any)
                {
                    _logger.WriteDebug($"Testing type '{toInvestigate}': is unit test fixture");
                }

                return any;
            }
            catch (Exception ex)
            {
                _logger.WriteDebug(
                    $"Failed to determine if type {typeToInvestigate.Module.Assembly.FullName} is {string.Join(" | ", _typesToFind.Select(type => type.FullName))} {ex.Message}");
                return false;
            }
        }

// ReSharper disable ReturnTypeCanBeEnumerable.Local
        private IReadOnlyCollection<string> UnitTestFixtureAssemblies(IEnumerable<(AssemblyDefinition, FileInfo)> assemblies)

            // ReSharper restore ReturnTypeCanBeEnumerable.Local
        {
            List<string> unitTestFixtureAssemblies =
                assemblies.Where(TryFindAssembly)
                    .Select(a => a.Item2.FullName)
                    .Distinct()
                    .ToList();

            return unitTestFixtureAssemblies;
        }

        private bool TryFindAssembly((AssemblyDefinition, FileInfo) assembly)
        {
            bool result;
            try
            {
                _logger.WriteDebug($"Testing assembly '{assembly}'");
                TypeDefinition[] types = assembly.Item1.MainModule.Types.ToArray();
                bool anyType = types.Any(TryIsTypeTestFixture);

                result = anyType;
            }
            catch (Exception)
            {
                _logger.WriteDebug($"Could not get types from assembly '{assembly.Item1.FullName}'");
                result = false;
            }

            if (DebugLogEnabled || result)
            {
                _logger.WriteDebug(
                    $"Assembly {assembly.Item1.FullName}, found any class with {string.Join(" | ", _typesToFind.Select(type => type.FullName))}: {result}");
            }

            return result;
        }

        private bool IsTypeUnitTestFixture(TypeDefinition typeToInvestigate)
        {
            IEnumerable<CustomAttribute> customAttributeDatas = typeToInvestigate.CustomAttributes;

            bool isTypeUnitTestFixture = IsCustomAttributeOfExpectedType(customAttributeDatas);

            bool isTestType = isTypeUnitTestFixture || TypeHasTestMethods(typeToInvestigate);

            return isTestType;
        }

        private bool IsCustomAttributeOfExpectedType(IEnumerable<CustomAttribute> customAttributes)
        {
            bool isTypeUnitTestFixture = customAttributes.Any(
                attributeData =>
                {
                    if (attributeData.AttributeType.FullName.StartsWith(nameof(System), StringComparison.Ordinal) ||
                        attributeData.AttributeType.FullName.StartsWith("_", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return IsCustomAttributeTypeToFind(attributeData);
                });
            return isTypeUnitTestFixture;
        }

        private bool TypeHasTestMethods(TypeDefinition typeToInvestigate)
        {
            IEnumerable<MethodDefinition> publicInstanceMethods =
                typeToInvestigate.Methods.Where(method => method.IsPublic && !method.IsStatic);

            bool hasPublicInstanceTestMethod =
                publicInstanceMethods.Any(method => IsCustomAttributeOfExpectedType(method.CustomAttributes));

            FieldDefinition[] declaredFields = typeToInvestigate.Fields.ToArray();

            bool hasPrivateFieldMethod = declaredFields
                .Where(field => field.IsPrivate)
                .Any(
                    field =>
                    {
                        string fullName = field.FieldType.FullName;

                        bool any = _typesToFind.Any(
                            type =>
                                !string.IsNullOrWhiteSpace(fullName) && type.FullName == fullName);

                        if (field.FieldType.IsGenericInstance && !string.IsNullOrWhiteSpace(fullName))
                        {
                            const string GenericPartSeparator = "`";
                            int fieldIndex = fullName.IndexOf(
                                GenericPartSeparator,
                                StringComparison.InvariantCultureIgnoreCase);

                            string fieldName = fullName.Substring(0, fieldIndex);

                            return _typesToFind.Any(
                                type =>
                                {
                                    int typePosition = type.FullName.IndexOf(
                                        GenericPartSeparator,
                                        StringComparison.InvariantCultureIgnoreCase);

                                    if (typePosition < 0)
                                    {
                                        return false;
                                    }

                                    string typeName = type.FullName.Substring(0, typePosition);

                                    return typeName.Equals(fieldName);
                                });
                        }

                        return any;
                    });

            bool hasTestMethod = hasPublicInstanceTestMethod || hasPrivateFieldMethod;

            return hasTestMethod;
        }

        private bool IsCustomAttributeTypeToFind(CustomAttribute attr)
        {
            return
                _typesToFind.Any(
                    typeToFind =>
                        attr.AttributeType.FullName.Equals(
                            typeToFind.FullName,
                            StringComparison.InvariantCultureIgnoreCase));
        }

        private (AssemblyDefinition, FileInfo) GetAssembly(FileInfo dllFile, string targetFrameworkPrefix)
        {
            try
            {
                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(dllFile.FullName);

                if (!string.IsNullOrWhiteSpace(targetFrameworkPrefix))
                {
                    TargetFrameworkAttribute targetFrameworkAttribute = assemblyDefinition.CustomAttributes.OfType<TargetFrameworkAttribute>().FirstOrDefault();

                    if (targetFrameworkAttribute != null)
                    {
                        if (!targetFrameworkAttribute.FrameworkName.StartsWith(targetFrameworkPrefix,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.WriteDebug($"The current assembly '{dllFile.FullName}' target framework attribute with value '{targetFrameworkAttribute.FrameworkName}' does not match the specified target framework '{targetFrameworkPrefix}'");
                            return (null, null);
                        }
                    }
                }

                TypeDefinition[] types = assemblyDefinition.Modules.SelectMany(m => m.GetTypes()).ToArray();

                int count = types.Length;

                if (DebugLogEnabled)
                {
                    _logger.WriteVerbose($"Found {count} types in assembly '{dllFile.FullName}'");
                }

                return (assemblyDefinition, dllFile);
            }
            catch (ReflectionTypeLoadException ex)
            {
                string message = $"Could not load assembly '{dllFile.FullName}', type load exception. Ignoring.";

                _logger.WriteDebug(message);
#if DEBUG
                Debug.WriteLine("{0}, {1}", message, ex);
#endif
                return (null, null);
            }
            catch (BadImageFormatException ex)
            {
                string message = $"Could not load assembly '{dllFile.FullName}', bad image format exception. Ignoring.";

                _logger.WriteDebug(message);
#if DEBUG
                Debug.WriteLine("{0}, {1}", message, ex);
#endif
                return (null,null);
            }
        }
    }
}
