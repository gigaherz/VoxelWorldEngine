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

        public double Dot(double x, double y)
        {
            return x * X + y * Y;
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }
}