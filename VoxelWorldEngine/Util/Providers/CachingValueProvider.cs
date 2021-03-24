using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util.Providers
{
    public class CachingValueProvider2D<T> : ValueProvider2D<T>
    {
        protected readonly ValueProvider2D<T> source;
        protected readonly Vector2I offset;
        protected readonly Vector2I size;
        private T[] values;

        public CachingValueProvider2D(Vector2I offset, Vector2I size, ValueProvider2D<T> source)
        {
            this.source = source;
            this.offset = offset;
            this.size = size;
        }

        public override T Get(int x, int z)
        {
            if (values == null)
            {
                values = Compute();
            }
            x -= offset.X;
            z -= offset.Y;
            return values[z * size.X + x];
        }

        protected virtual T[] Compute()
        {
            T[] values = new T[size.X * size.Y];
            for (int z = 0; z < size.Y; z++)
            {
                int zz = z * size.X;
                for (int x = 0; x < size.X; x++)
                {
                    values[zz + x] = source.Get(x + offset.X, z + offset.Y);
                }
            }
            return values;
        }
    }

    public class CachingValueProvider3D<T> : ValueProvider3D<T>
    {
        protected readonly ValueProvider3D<T> source;
        protected readonly Vector3I offset;
        protected readonly Vector3I size;
        private T[] values;

        public CachingValueProvider3D(Vector3I offset, Vector3I size, ValueProvider3D<T> source)
        {
            this.source = source;
            this.offset = offset;
            this.size = size;
        }

        public override T Get(int x, int y, int z)
        {
            if (values == null)
            {
                values = Compute();
            }
            x -= offset.X;
            y -= offset.Y;
            z -= offset.Z;
            return values[(z * size.Y + y) * size.X + x];
        }

        protected virtual T[] Compute()
        {
            T[] values = new T[size.X * size.Y * size.Z];
            for (int z = 0; z < size.Z; z++)
            {
                int zz = z * size.Y * size.X;
                for (int y = 0; y < size.Y; y++)
                {
                    int yy = y * size.X;
                    for (int x = 0; x < size.X; x++)
                    {
                        values[zz + yy + x] = source.Get(x + offset.X, y + offset.Y, z + offset.Z);
                    }
                }
            }
            return values;
        }
    }

    public class LocalArrayValueProvider2D<T> : LocalValueProvider2D<T>
    {
        protected readonly Vector2I size;
        private T defaultValue;
        private T[] values;

        public LocalArrayValueProvider2D(Vector2I size, T defaultValue)
        {
            this.defaultValue = defaultValue;
            this.size = size;
        }

        public override T Get(int x, int z)
        {
            if (values == null)
            {
                values = Compute();
            }
            return values[z * size.X + x];
        }

        public override T Set(int x, int z, T newValue)
        {
            if (values == null)
            {
                values = Compute();
            }

            int idx = z * size.X + x;
            T oldValue = values[idx];
            values[idx] = newValue;
            return oldValue;
        }

        protected virtual T[] Compute()
        {
            T[] values = new T[size.X * size.Y];
            for (int z = 0; z < size.Y; z++)
            {
                int zz = z * size.X;
                for (int x = 0; x < size.X; x++)
                {
                    values[zz + x] = defaultValue;
                }
            }
            return values;
        }
    }

    public abstract class LocalArrayValueProvider3D<T> : LocalValueProvider3D<T>
    {
        protected readonly Vector3I size;
        private readonly T defaultValue;
        private T[] values;

        protected LocalArrayValueProvider3D(Vector3I size, T defaultValue)
        {
            this.defaultValue = defaultValue;
            this.size = size;
        }

        public override T Get(int x, int y, int z)
        {
            if (values == null)
            {
                values = Compute();
            }

            return values[(z * size.Y + y) * size.X + x];
        }

        public override T Set(int x, int y, int z, T newValue)
        {
            if (values == null)
            {
                values = Compute();
            }

            int idx = (z * size.Y + y) * size.X + x;
            T oldValue = values[idx];
            values[idx] = newValue;
            return oldValue;
        }

        protected virtual T[] Compute()
        {
            T[] values = new T[size.X * size.Y * size.Z];
            for (int z = 0; z < size.Z; z++)
            {
                int zz = z * size.Y * size.X;
                for (int y = 0; y < size.Y; y++)
                {
                    int yy = y * size.X;
                    for (int x = 0; x < size.X; x++)
                    {
                        values[zz + yy + x] = defaultValue;
                    }
                }
            }
            return values;
        }
    }
}