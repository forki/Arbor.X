﻿using Arbor.X.Core;
using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Maybe
{
    public class when_comparing_two_default_maybes
    {
        Because of = () => equal = Equals(default(Maybe<string>), default(Maybe<string>));

        It should_return_false = () => equal.ShouldBeFalse();

        static bool equal;
    }
}