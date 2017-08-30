using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Maybe
{
    public class when_comparing_non_empty_maybe_with_null_using_overrided_equals
    {
        private static bool equal;
        private Because of = () => equal = new Defensive.Maybe<string>("a string").Equals(null);

        private It should_return_false = () => equal.ShouldBeFalse();
    }
}
