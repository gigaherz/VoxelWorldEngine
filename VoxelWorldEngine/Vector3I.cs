using System;

namespace VoxelWorldEngine
{
    public struct Vector3I : IComparable<Vector3I>, IEquatable<Vector3I>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Vector3I(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int CompareTo(Vector3I other)
        {
            int d = Math.Sign(Z - other.Z);
            if (d != 0) return d;

            d = Math.Sign(X - other.X);
            if (d != 0) return d;

            return Math.Sign(Y - other.Y);
        }

        public bool Equals(Vector3I other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector3I))
                return false;
            return Equals((Vector3I)obj);
        }

        public override int GetHashCode()
        {
            return X * 65537 + Z * 257 + Y;
        }
    }
}