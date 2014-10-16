using Arbor.X.Core;
using Machine.Specifications;

namespace Arbor.X.Tests.Integration.BoolExtensions
{
    [Subject(typeof(Core.BoolExtensions))]
    public class when_parsing_true_value_with_default_false
    {
        static bool result;
        Because of = () => { result = "true".TryParseBool(defaultValue: false); };

        It should_be_true = () => result.ShouldBeTrue();
    }
}