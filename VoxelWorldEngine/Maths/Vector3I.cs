using System;
using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Maths
{
    public struct Vector3I : IComparable<Vector3I>, IEquatable<Vector3I>
    {
        public int X;
        public int Y;
        public int Z;

        public Vector3I(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector2I XZ => new Vector2I(X,Z);

        public int LengthSquared() => X * X + Y * Y + Z * Z;
        public double Length() => Math.Sqrt(LengthSquared());

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

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        public Vector3I Offset(int x, int y, int z)
        {
            return new Vector3I(X + x, Y + y, Z + z);
        }

        public static bool operator ==(Vector3I a, Vector3I b)
        {
            return 
                a.X == b.X &&
                a.Y == b.Y &&
                a.Z == b.Z;
        }

        public static bool operator !=(Vector3I a, Vector3I b)
        {
            return
                a.X != b.X ||
                a.Y != b.Y ||
                a.Z != b.Z;
        }

        public static Vector3I operator +(Vector3I a, Vector3I b)
        {
            return new Vector3I(
                a.X + b.X,
                a.Y + b.Y,
                a.Z + b.Z);
        }

        public static Vector3I operator -(Vector3I a, Vector3I b)
        {
            return new Vector3I(
                a.X - b.X,
                a.Y - b.Y,
                a.Z - b.Z);
        }

        public static Vector3I operator -(Vector3I a)
        {
            return new Vector3I(-a.X, -a.Y, -a.Z);
        }

        public static Vector3I operator *(Vector3I a, Vector3I b)
        {
            return new Vector3I(
                a.X * b.X,
                a.Y * b.Y,
                a.Z * b.Z);
        }

        public static Vector3I operator /(Vector3I a, Vector3I b)
        {
            return new Vector3I(
                a.X / b.X,
                a.Y / b.Y,
                a.Z / b.Z);
        }

        public static Vector3I operator &(Vector3I a, Vector3I b)
        {
            return new Vector3I(
                a.X & b.X,
                a.Y & b.Y,
                a.Z & b.Z);
        }

        public static Vector3I operator -(Vector3I a, int b)
        {
            return new Vector3I(
                a.X - b,
                a.Y - b,
                a.Z - b);
        }

        public static Vector3I operator *(Vector3I a, int b)
        {
            return new Vector3I(
                a.X * b,
                a.Y * b,
                a.Z * b);
        }

        public static Vector3I operator /(Vector3I a, int b)
        {
            return new Vector3I(
                a.X / b,
                a.Y / b,
                a.Z / b);
        }

        public static Vector3I operator %(Vector3I a, int b)
        {
            return new Vector3I(
                a.X % b,
                a.Y % b,
                a.Z % b);
        }

        public static Vector3 operator *(Vector3I a, float b)
        {
            return new Vector3(
                a.X * b,
                a.Y * b,
                a.Z * b);
        }

        public static Vector3 operator /(Vector3I a, float b)
        {
            return new Vector3(
                a.X / b,
                a.Y / b,
                a.Z / b);
        }

        public static implicit operator Vector3(Vector3I vec)
        {
            return new Vector3(
                vec.X,
                vec.Y,
                vec.Z);
        }

        public static explicit operator Vector3I(Vector3 vec)
        {
            return new Vector3I(
                (int)vec.X,
                (int)vec.Y,
                (int)vec.Z);
        }

        public Vector3I Postincrement(int x, int y, int z)
        {
            var v = this;
            X += x;
            Y += y;
            Z += z;
            return v;
        }
    }

    public struct Vector2I : IComparable<Vector2I>, IEquatable<Vector2I>
    {
        public int X;
        public int Y;

        public Vector2I(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int LengthSquared() => X * X + Y * Y;
        public double Length() => Math.Sqrt(LengthSquared());

        public int CompareTo(Vector2I other)
        {
            int d = Math.Sign(X - other.X);
            if (d != 0) return d;

            return Math.Sign(Y - other.Y);
        }

        public bool Equals(Vector2I other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector2I))
                return false;
            return Equals((Vector2I)obj);
        }

        public override int GetHashCode()
        {
            return X * 257 + Y;
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }

        public Vector2I Offset(int x, int y)
        {
            return new Vector2I(X + x, Y + y);
        }

        public static Vector2I operator +(Vector2I a, Vector2I b)
        {
            return new Vector2I(
                a.X + b.X,
                a.Y + b.Y);
        }

        public static Vector2I operator -(Vector2I a, Vector2I b)
        {
            return new Vector2I(
                a.X - b.X,
                a.Y - b.Y);
        }

        public static Vector2I operator *(Vector2I a, Vector2I b)
        {
            return new Vector2I(
                a.X * b.X,
                a.Y * b.Y);
        }

        public static Vector2I operator /(Vector2I a, Vector2I b)
        {
            return new Vector2I(
                a.X / b.X,
                a.Y / b.Y);
        }

        public static Vector2I operator &(Vector2I a, Vector2I b)
        {
            return new Vector2I(
                a.X & b.X,
                a.Y & b.Y);
        }

        public static Vector2I operator -(Vector2I a, int b)
        {
            return new Vector2I(
                a.X - b,
                a.Y - b);
        }

        public static Vector2 operator *(Vector2I a, float b)
        {
            return new Vector2(
                a.X * b,
                a.Y * b);
        }

        public static Vector2 operator /(Vector2I a, float b)
        {
            return new Vector2(
                a.X / b,
                a.Y / b);
        }

        public static implicit operator Vector2(Vector2I vec)
        {
            return new Vector2(
                vec.X,
                vec.Y);
        }

        public static explicit operator Vector2I(Vector2 vec)
        {
            return new Vector2I(
                (int)vec.X,
                (int)vec.Y);
        }

        public Vector2I Postincrement(int x, int y)
        {
            var v = this;
            X += x;
            Y += y;
            return v;
        }
    }
}