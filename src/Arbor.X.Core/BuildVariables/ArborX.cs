namespace Arbor.X.Core.BuildVariables
{
    internal class ArborX
    {
        private const string Namespace = "Arbor.X";

        public string Build { get; } = Namespace + ".Build";

        public static implicit operator string(ArborX arborX)
        {
            return Namespace;
        }

        public override string ToString()
        {
            return Namespace;
        }
    }
}
