using System;

namespace Cutting.Data
{
    public struct Edge : IEquatable<Edge>
    {
        public int a, b;

        public bool Equals(Edge other)
        {
            var firstCase = a == other.a && b == other.b;
            var secondCase = a == other.b && b == other.a;

            return firstCase | secondCase;
        }

        public override int GetHashCode() => a.GetHashCode() + b.GetHashCode();

        public bool Empty()
        {
            return a == -1 || b == -1;
        }
    }
}
