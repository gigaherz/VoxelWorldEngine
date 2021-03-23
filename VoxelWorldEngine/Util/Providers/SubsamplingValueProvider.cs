using System;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Util.Providers;

namespace VoxelWorldEngine.Util
{
    public class SubsamplingValueProvider<T> : CachingValueProvider3D<T>
    {
        private readonly int subsample;
        private readonly Interpolator<T> interpolator;

        public SubsamplingValueProvider(Vector3I offset, Vector3I size, ValueProvider3D<T> source, int subsample, Interpolator<T> interpolator)
            : base(offset, size, source)
        {
            this.subsample = subsample;
            this.interpolator = interpolator;
        }

        protected override T[] Compute()
        {
            int xS = Math.Max(1, size.X / subsample);
            int yS = Math.Max(1, size.Y / subsample);
            int zS = Math.Max(1, size.Z / subsample);
            int dx = size.X / xS;
            int dy = size.Y / yS;
            int dz = size.Z / zS;

            var xSS = xS + 1;
            var ySS = yS + 1;
            var zSS = zS + 1;
            T[] subsampled = new T[xSS * ySS * zSS];
            for (int z = 0; z <= zS; z++)
            {
                int zz = z * ySS * xSS;
                for (int y = 0; y <= yS; y++)
                {
                    int yy = y * xSS;
                    for (int x = 0; x <= xS; x++)
                    {
                        subsampled[zz + yy + x] = source.Get(dx * x + offset.X, dy * y + offset.X, dz * z + offset.X);
                    }
                }
            }

            T[] data = new T[size.X * size.Y * size.Z];
            for (int z = 0; z < size.Z; z++)
            {
                int zz = z * size.Y * size.X;
                for (int y = 0; y < size.Y; y++)
                {
                    int yy = y * size.X;
                    for (int x = 0; x < size.X; x++)
                    {
                        double xFraction = x * (double)xS / size.X;
                        double yFraction = y * (double)yS / size.Y;
                        double zFraction = z * (double)zS / size.Z;
                        int xIntegral = (int)Math.Floor(xFraction);
                        int yIntegral = (int)Math.Floor(yFraction);
                        int zIntegral = (int)Math.Floor(zFraction);

                        T v000 = subsampled[xIntegral + xSS * (yIntegral + ySS * zIntegral)];
                        T v001 = subsampled[xIntegral + 1 + xSS * (yIntegral + ySS * zIntegral)];
                        T v010 = subsampled[xIntegral + xSS * (yIntegral + 1 + ySS * zIntegral)];
                        T v011 = subsampled[xIntegral + 1 + xSS * (yIntegral + 1 + ySS * zIntegral)];
                        T v100 = subsampled[xIntegral + xSS * (yIntegral + ySS * (zIntegral + 1))];
                        T v101 = subsampled[xIntegral + 1 + xSS * (yIntegral + ySS * (zIntegral + 1))];
                        T v110 = subsampled[xIntegral + xSS * (yIntegral + 1 + ySS * (zIntegral + 1))];
                        T v111 = subsampled[xIntegral + 1 + xSS * (yIntegral + 1 + ySS * (zIntegral + 1))];

                        data[zz + yy + x] = interpolator.Lerp(v000, v001, v010, v011, v100, v101, v110, v111,
                            xFraction - xIntegral, yFraction - yIntegral, zFraction - zIntegral);
                    }
                }
            }
            return data;
        }
    }
}