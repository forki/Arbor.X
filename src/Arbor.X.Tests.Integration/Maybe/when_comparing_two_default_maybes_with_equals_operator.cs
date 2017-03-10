﻿using Arbor.X.Core;
using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Maybe
{
    public class when_comparing_two_default_maybes_with_equals_operator
    {
        Because of = () => equal = default(Defensive.Maybe<string>) == default(Defensive.Maybe<string>);

        It should_return_false = () => equal.ShouldBeFalse();

        static bool equal;
    }
}