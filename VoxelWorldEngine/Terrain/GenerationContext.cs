using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Util;
using VoxelWorldEngine.Util.Providers;

namespace VoxelWorldEngine.Terrain
{
    public class GenerationContext
    {
        public int Seed { get; }

        // Temporary.
        public int WorldFloor { get; }
        public int WaterLevel { get; }
        public int BeachBottom { get; }
        public int BeachTop { get; }
        public ValueProvider3D<double> RawDensityProvider { get; }
        public ValueProvider2D<double> HeightProvider { get; }
        public NoiseValueProvider2D RoughnessProvider { get; }
        public ValueProvider2D<(double,double,double)> TopologyProvider { get; }
        public ValueProvider3D<double> DensityProvider { get; }

        public GenerationContext(GenerationSettings settings)
        {
            Seed = settings.Seed;
            var PerlinDensity = new Simplex(Seed, ((Vector3D)Tile.VoxelSize) * 0.035);
            var PerlinHeight = new Simplex(Seed * 31, ((Vector3D)Tile.VoxelSize) * 0.001 /** 0.0005*/);
            var PerlinRoughness = new Simplex(Seed * 53, ((Vector3D)Tile.VoxelSize) * 0.005);
            //var PerlinSharpness = new Simplex(Seed * 71, 1 / 7.0);
            var PerlinOffset = new Simplex(Seed * 113, 1);
            WorldFloor = settings.WorldFloor;
            WaterLevel = settings.WaterLevel;
            BeachBottom = settings.BeachBottom;
            BeachTop = settings.BeachTop;
            HeightProvider = new HeightProvider(new NoiseValueProvider2D(PerlinHeight, settings.HeightOctaves));
            RoughnessProvider = new NoiseValueProvider2D(PerlinRoughness, settings.RoughnessOctaves);
            RawDensityProvider = new NoiseValueProvider3D(PerlinDensity, settings.DensityOctaves);
            TopologyProvider = new TopologyProvider(
               HeightProvider, RoughnessProvider, PerlinOffset,
               settings.HeightAmplitude, settings.FlatlandsHeightOffset, settings.WaterLevel);
            DensityProvider = new DensityProvider(RawDensityProvider, TopologyProvider);
        }

        public void Initialize()
        {
        }
    }

    public class GenerationSettings
    {
        public int Seed;

        // Temporary.
        public int WorldFloor = -64;

        // Basic Terrain configuration
        public int HeightAmplitude = 256;
        public int FlatlandsHeightOffset = 0;
        public int WaterLevel = 0;
        public int RoughnessOctaves = 2;
        public int HeightOctaves = 3;
        public int DensityOctaves = 2;

        // Surface generation configuration
        public int DirtLayers = 3;
        public int BeachTop = 3;
        public int BeachBottom = 5;

        public GenerationSettings(int seed)
        {
            Seed = seed;
        }
    }
}
