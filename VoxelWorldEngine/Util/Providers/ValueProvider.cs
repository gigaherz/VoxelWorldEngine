using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util.Providers
{
    public interface IValueProvider
    {
    }

    public abstract class ValueProvider2D<T> : IValueProvider
    {
        public T Get(Vector2I pos) => Get(pos.X, pos.Y);

        public abstract T Get(int x, int z);
    }

    public abstract class ValueProvider3D<T> : IValueProvider
    {
        public T Get(Vector3I pos) => Get(pos.X, pos.Y, pos.Z);

        public abstract T Get(int x, int y, int z);
    }

    public abstract class LocalValueProvider2D<T> : IValueProvider
    {
        public T Get(Vector2I pos) => Get(pos.X, pos.Y);

        public abstract T Get(int x, int z);
        public abstract T Set(int x, int z, T newValue);
    }

    public abstract class LocalValueProvider3D<T> : IValueProvider
    {
        public T Get(Vector3I pos) => Get(pos.X, pos.Y, pos.Z);

        public abstract T Get(int x, int y, int z);
        public abstract T Set(int x, int y, int z, T newValue);
    }
}