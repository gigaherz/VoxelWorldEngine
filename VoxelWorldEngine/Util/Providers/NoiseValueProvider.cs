using System;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;

namespace VoxelWorldEngine.Util.Providers
{
    public class NoiseValueProvider2D : ValueProvider2D<double>
    {
        private readonly NoiseOctaves noiseSource;
        private readonly int octaves;

        public NoiseValueProvider2D(NoiseOctaves noiseSource, int octaves)
        {
            this.noiseSource = noiseSource;
            this.octaves = octaves;
        }

        public override double Get(int x, int z)
        {
            return noiseSource.Noise(x: x, y: z, octaves);
        }
    }

    public class NoiseValueProvider3D : ValueProvider3D<double>
    {
        private readonly NoiseOctaves noiseSource;
        private readonly int octaves;

        public NoiseValueProvider3D(NoiseOctaves noiseSource, int octaves)
        {
            this.noiseSource = noiseSource;
            this.octaves = octaves;
        }

        public override double Get(int x, int y, int z)
        {
            return noiseSource.Noise(x: x, y: y, z: z, octaves);
        }
    }
}