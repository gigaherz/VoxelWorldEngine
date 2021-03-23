using System;

namespace VoxelWorldEngine.Util.Providers
{
    public class DensityProvider : ValueProvider3D<double>
    {
        private readonly ValueProvider3D<double> densityProvider;
        private readonly ValueProvider2D<(double, double, double)> topologyProvider;

        public DensityProvider(ValueProvider3D<double> densityProvider, ValueProvider2D<(double, double, double)> topologyProvider)
        {
            this.densityProvider = densityProvider;
            this.topologyProvider = topologyProvider;
        }

        public override double Get(int x, int y, int z)
        {
            var rawDensity = densityProvider.Get(x, y, z);
            var (roughtness, bottom, top) = topologyProvider.Get(x, z);
            return GetDensityAt(x, y, z, roughtness, bottom, top, rawDensity);
        }

        public double GetDensityAt(int x, int y, int z, double roughness, double bottom, double top, double rawDensity)
        {
            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (y - bottom) / (top - bottom)));
            var noise = 0.15 * rawDensity;
            return roughness * noise + baseDensity;
        }
    }
}
