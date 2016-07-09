using System;
using System.CodeDom;
using System.Xml.Serialization;

namespace VoxelWorldEngine.Util
{
    public struct Vector3D
    {
        public double X;
        public double Y;
        public double Z;

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public double Dot(double x, double y, double z)
        {
            return x * X + y * Y + z * Z;
        }

        public static Vector3D operator -(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X-b.X,
                a.Y-b.Y,
                a.Z-b.Z
                );
        }

        public static Vector3D operator *(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.X * b.X,
                a.Y * b.Y,
                a.Z * b.Z
                );
        }
    }
}