using System;
using System.Collections.Generic;
using System.Linq;

namespace Arbor.X.Core.Tools.Kudu
{
    public class KuduWebJobType : IEquatable<KuduWebJobType>
    {
        readonly string _invariantName;

        KuduWebJobType(string invariantName)
        {
            _invariantName = invariantName;
        }

        public static KuduWebJobType Continuous => new KuduWebJobType("Continuous");

        public static KuduWebJobType Triggered => new KuduWebJobType("Triggered");

        public static IEnumerable<KuduWebJobType> All
        {
            get
            {
                yield return Continuous;
                yield return Triggered;
            }
        }

        public bool Equals(KuduWebJobType other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_invariantName, other._invariantName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((KuduWebJobType) obj);
        }

        public override int GetHashCode()
        {
            return _invariantName?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public string DisplayName => _invariantName;

        public static bool operator ==(KuduWebJobType left, KuduWebJobType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KuduWebJobType left, KuduWebJobType right)
        {
            return !Equals(left, right);
        }

        public static KuduWebJobType Parse(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentNullException(nameof(type));
            }

            var message = $"Could not parse {typeof(KuduWebJobType).Name} from value '{type}'";

            const StringComparison ComparisonType = StringComparison.InvariantCultureIgnoreCase;

            string valueToParse;

            var exception = new FormatException(message);

            valueToParse = type.Trim().StartsWith("<") ? type.ExtractFromTag(typeof (KuduWebJobType).Name) : type;

            KuduWebJobType foundItem =
                All.SingleOrDefault(
                    item => item._invariantName.Equals(valueToParse, ComparisonType));

            if (foundItem == null)
            {
                throw exception;
            }

            return foundItem;
        }
    }
}
