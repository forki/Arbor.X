using System;
using System.IO;
using Arbor.X.Core.IO;
using Machine.Specifications;

namespace Arbor.X.Tests.Integration.PathExtensions
{
    [Subject(typeof(Core.IO.PathExtensions))]
    public class when_checking_file_is_blacklisted_in_artifacts_directory
    {
        static bool isBlackListed;

        static PathLookupSpecification specification;

        static string root;

        Cleanup after = () => { new DirectoryInfo(root).DeleteIfExists(true); };

        Establish context = () =>
        {
            root = $@"C:\Temp\root-{Guid.NewGuid()}";

            new DirectoryInfo(Path.Combine(root, "artifacts")).EnsureExists();
            using (File.Create(Path.Combine(root, "artifacts", "afile.txt")))
            {
            }

            specification = DefaultPaths.DefaultPathLookupSpecification;
        };

        Because of =
            () => { isBlackListed = specification.IsFileBlackListed($@"{root}\artifacts\afile.txt", root).Item1; };

        It should_return_false = () => isBlackListed.ShouldBeTrue();
    }
}
