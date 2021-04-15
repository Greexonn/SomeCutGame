using System;

namespace Cutting.Data
{
    public struct Edge : IEquatable<Edge>
    {
        public int a, b;

        public bool Equals(Edge other)
        {
            if (a == other.a)
            {
                if (b == other.b)
                    return true;
            }
            else
            {
                if (a != other.b) 
                    return false;
                if (b == other.a)
                    return true;
            }

            return false;
        }

        public override int GetHashCode() => a.GetHashCode() + b.GetHashCode();

        public bool Empty()
        {
            return a == -1 || b == -1;
        }
    }
}
