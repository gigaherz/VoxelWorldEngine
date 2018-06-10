using System;
using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Maths
{
    public static class VectorUtils
    {
        public static Vector3 Floor(this Vector3 arg)
        {
            return new Vector3(
                (float)Math.Floor(arg.X),
                (float)Math.Floor(arg.Y),
                (float)Math.Floor(arg.Z));
        }

        public static Vector3D Floor(this Vector3D arg)
        {
            return new Vector3D(
                Math.Floor(arg.X),
                Math.Floor(arg.Y),
                Math.Floor(arg.Z));
        }

        public static Vector3 Add(this Vector3 a, float b)
        {
            return new Vector3(
                a.X+b,
                a.Y+b,
                a.X+b);
        }

        public static Vector2 XZ(Vector3 vector3)
        {
            return new Vector2(vector3.X, vector3.Z);
        }

        public static Vector3I FloorToInt(this Vector3 a)
        {
            return new Vector3I(
                (int)Math.Floor(a.X),
                (int)Math.Floor(a.Y),
                (int)Math.Floor(a.Z));
        }
    }
}
