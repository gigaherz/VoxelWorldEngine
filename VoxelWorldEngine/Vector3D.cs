namespace VoxelWorldEngine
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

        public double Dot(double x, double y, double z)
        {
            return x * X + y * Y + z * Z;
        }
    }
}