﻿// ReSharper disable InconsistentNaming

namespace Arbor.X.Core.BuildVariables
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1310:Field names must not contain underscore",
        Justification = "Variables")]
    public partial class WellKnownVariables
    {
        [VariableDescription("Jenkins HOME path")]
        public static readonly string ExternalTools_Jenkins_JenkinsHome =
            "JENKINS_HOME";

        [VariableDescription("Flag to indiciate if running in Jenkins (calculated)")]
        public static readonly string ExternalTools_Jenkins_IsRunningInJenkins =
            "Arbor.X.Tools.External.Jenkins.IsRunningInJenkins";
    }
}
