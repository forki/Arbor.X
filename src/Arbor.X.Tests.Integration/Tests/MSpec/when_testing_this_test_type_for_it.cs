using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.X.Core.Logging;
using Arbor.X.Core.Tools.Testing;
using Machine.Specifications;
using Mono.Cecil;

namespace Arbor.X.Tests.Integration.Tests.MSpec
{
    [Subject(typeof(UnitTestFinder))]
    [Tags(MSpecInternalConstants.RecursiveArborXTest)]
    public class when_testing_this_test_type_for_it
    {
        static UnitTestFinder finder;
        static bool isTestType;

        Establish context = () =>
        {
            var logger = new ConsoleLogger { LogLevel = LogLevel.Verbose };
            finder = new UnitTestFinder(new List<Type>
                {
                    typeof(It)
                },
                logger: logger);
        };

        Because of =
            () =>
            {
                Type typeToInvestigate = typeof(when_testing_this_test_type_for_it);

                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(typeToInvestigate.Assembly.Location);

                TypeDefinition typeDefinition = assemblyDefinition.MainModule.Types.Single(t => t.FullName.Equals(typeToInvestigate.FullName));

                isTestType = finder.TryIsTypeTestFixture(typeDefinition);
            };

        It should_Behaviour = () => isTestType.ShouldBeTrue();
    }
}
