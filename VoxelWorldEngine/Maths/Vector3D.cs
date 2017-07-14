using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Maths
{
    public struct Vector3D
    {
        public static Vector3D Zero { get; } = new Vector3D();

        public double X;
        public double Y;
        public double Z;

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double LengthSquared() => X * X + Y * Y + Z * Z;
        public double Length() => Math.Sqrt(LengthSquared());

        public double Dot(double x, double y, double z)
        {
            return x * X + y * Y + z * Z;
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        public static Vector3D operator -(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X - b.X,
                a.Y - b.Y,
                a.Z - b.Z);
        }

        public static Vector3D operator +(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X + b.X,
                a.Y + b.Y,
                a.Z + b.Z);
        }

        public static Vector3D operator *(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X * b.X,
                a.Y * b.Y,
                a.Z * b.Z);
        }

        public static Vector3D operator /(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X / b.X,
                a.Y / b.Y,
                a.Z / b.Z);
        }

        public static implicit operator Vector3D(Vector3 vec)
        {
            return new Vector3D(
                vec.X,
                vec.Y,
                vec.Z);
        }

        public static implicit operator Vector3D(Vector3I vec)
        {
            return new Vector3D(
                vec.X,
                vec.Y,
                vec.Z);
        }

        public static implicit operator Vector3D(double n)
        {
            return new Vector3D(n,n,n);
        }

        public static explicit operator Vector3(Vector3D vec)
        {
            return new Vector3(
                (float)vec.X,
                (float)vec.Y,
                (float)vec.Z);
        }
    }
}