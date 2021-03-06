using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Logging.LogLevel
{
    [Subject(typeof(Core.Logging.LogLevel))]
    public class when_comparing_log_level_information_and_default
    {
        static Core.Logging.LogLevel logLevel;
        Because of = () => { logLevel = Core.Logging.LogLevel.Information; };

        It should_equal_default = () => logLevel.ShouldEqual(Core.Logging.LogLevel.Default);
    }
}
