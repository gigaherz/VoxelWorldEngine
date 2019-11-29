using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Maths
{
    public struct Vector2D
    {
        public double X;
        public double Y;

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }

        public double Dot(double x, double y)
        {
            return x * X + y * Y;
        }

        public static Vector2D operator -(Vector2D a, Vector2D b)
        {
            return new Vector2D(
                a.X - b.X,
                a.Y - b.Y);
        }

        public static Vector2D operator +(Vector2D a, Vector2D b)
        {
            return new Vector2D(
                a.X + b.X,
                a.Y + b.Y);
        }

        public static Vector2D operator *(Vector2D a, Vector2D b)
        {
            return new Vector2D(
                a.X * b.X,
                a.Y * b.Y);
        }

        public static Vector2D operator /(Vector2D a, Vector2D b)
        {
            return new Vector2D(
                a.X / b.X,
                a.Y / b.Y);
        }

        public static implicit operator Vector2D(Vector2 vec)
        {
            return new Vector2D(
                vec.X,
                vec.Y);
        }

        public static implicit operator Vector2D(Vector2I vec)
        {
            return new Vector2D(
                vec.X,
                vec.Y);
        }

        public static implicit operator Vector2D(double n)
        {
            return new Vector2D(n, n);
        }

        public static explicit operator Vector2(Vector2D vec)
        {
            return new Vector2(
                (float)vec.X,
                (float)vec.Y);
        }
    }
}