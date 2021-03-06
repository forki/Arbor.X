using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.X.Core.BuildVariables;
using Arbor.X.Core.GenericExtensions;
using Arbor.X.Core.Logging;
using Arbor.X.Core.Parsing;
using Arbor.X.Core.Tools.Cleanup;
using JetBrains.Annotations;

namespace Arbor.X.Core.Tools.Environments
{
    [UsedImplicitly]
    public class BuildAgentVariableProvider : IVariableProvider
    {
        public int Order => VariableProviderOrder.Default;

        public Task<IEnumerable<IVariable>> GetEnvironmentVariablesAsync(
            ILogger logger,
            IReadOnlyCollection<IVariable> buildVariables,
            CancellationToken cancellationToken)
        {
            bool isBuildAgentValue;

            var buildAgentEnvironmentVariables = new List<string>
            {
                WellKnownVariables.ExternalTools_Hudson_HudsonHome,
                WellKnownVariables.ExternalTools_Jenkins_JenkinsHome,
                WellKnownVariables.TeamCity.ExternalTools_TeamCity_TeamCityVersion
            };

            ParseResult<bool> isBuildAgent =
                Environment.GetEnvironmentVariable(WellKnownVariables.IsRunningOnBuildAgent)
                    .TryParseBool(false);

            if (isBuildAgent.Parsed)
            {
                logger.WriteVerbose(
                    $"Successfully parsed environment variable '{WellKnownVariables.IsRunningOnBuildAgent}' with value '{isBuildAgent.OriginalValue}' as boolean with value: {isBuildAgent.Value}");
                isBuildAgentValue = isBuildAgent.Value;
            }
            else
            {
                isBuildAgentValue =
                    buildAgentEnvironmentVariables.Any(
                        buildAgent => Environment.GetEnvironmentVariable(buildAgent).TryParseString().Parsed);
            }

            var variables = new List<IVariable>
            {
                new EnvironmentVariable(
                    WellKnownVariables.IsRunningOnBuildAgent,
                    isBuildAgentValue.ToString().ToLowerInvariant())
            };

            return Task.FromResult<IEnumerable<IVariable>>(variables);
        }
    }
}
