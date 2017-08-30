using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Maybe
{
    public class when_comparing_empty_maybe_with_empty_value_using_equals_operator
    {
        private static bool equal;
        private Because of = () => equal = new Defensive.Maybe<string>() == (string)null;

        private It should_return_false = () => equal.ShouldBeFalse();
    }
}
