using System;

namespace Cutting.Data
{
    [Flags]
    public enum Side : byte
    {
        Left = 1 << 0,
        Right = 1 << 1,
        Intersected = Left | Right
    }
}
