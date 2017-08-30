using Machine.Specifications;

namespace Arbor.X.Tests.Integration.Logging.LogLevel
{
    [Subject(typeof(Core.Logging.LogLevel))]
    public class when_parsing_level_int_max
    {
        private static Core.Logging.LogLevel logLevel;
        private Because of = () => { logLevel = Core.Logging.LogLevel.TryParse(int.MaxValue); };

        private It should_equal_default = () => logLevel.ShouldEqual(Core.Logging.LogLevel.Default);

        private It should_equal_default_level = () => logLevel.Level.ShouldEqual(Core.Logging.LogLevel.Default.Level);
    }
}
