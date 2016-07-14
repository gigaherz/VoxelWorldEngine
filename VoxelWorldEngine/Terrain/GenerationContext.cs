using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxelWorldEngine.Noise;

namespace VoxelWorldEngine.Terrain
{
    public class GenerationContext
    {
        public Simplex PerlinDensity { get; }
        public Simplex PerlinHeight { get; }
        public Simplex PerlinRoughness { get; }

        public int Seed { get; } = (int)DateTime.UtcNow.Ticks;

        // Basic Terrain configuration
        public int HeightAmplitude = 256;
        public int FlatlandsHeightOffset = 0;
        public int WaterLevel = 0;

        // Surface generation configuration
        public int DirtLayers = 3;
        public int BeachTop = 3;
        public int BeachBottom = 5;

        // Computed values for terrain configuration
        public int VerticalChunkCount;
        public int Floor;
        public int Ceiling;
        public int DepthLevel;
        public int TopLevel;
        public int Average;
        public int Range;

        public GenerationContext()
        {
            PerlinDensity = new Simplex(Seed);
            PerlinHeight = new Simplex(Seed * 3);
            PerlinRoughness = new Simplex(Seed * 5);

            // Computed values for terrain configuration
            VerticalChunkCount = Math.Max(1, 1 + HeightAmplitude / Tile.SizeY);
            Floor = -VerticalChunkCount * Tile.SizeY;
            Ceiling = VerticalChunkCount * Tile.SizeY;
            DepthLevel = FlatlandsHeightOffset + WaterLevel - HeightAmplitude;
            TopLevel = FlatlandsHeightOffset + WaterLevel + HeightAmplitude;
            Average = (TopLevel + DepthLevel) / 2;
            Range = (TopLevel - DepthLevel) / 2;

        }

        public void Initialize()
        {
            PerlinDensity.Initialize();
            PerlinHeight.Initialize();
            PerlinRoughness.Initialize();
        }

        public double GetDensityAt(int px, int py, int pz)
        {
            var nx = px * Tile.VoxelSizeX / 128.0;
            var ny = py * Tile.VoxelSizeY / 128.0;
            var nz = pz * Tile.VoxelSizeZ / 128.0;

            var hx = nx / 2;
            var hz = nz / 2;
            var sx = nx *8;
            var sz = nz *8;

            var rx = nx / 3;
            var rz = nz / 3;

            var ph = PerlinHeight.Noise(hx, hz, 2);
            var height = HeightCurve(ph);

            var sh = PerlinHeight.Noise(sx, sz, 2);
            var sharpness = SharpnessCurve(ph) * (1+0.25*sh);

            var rh = PerlinRoughness.Noise(rx, rz, 2);
            var roughness = RoughnessCurve(rh);

            var baseHeight = Average + Range * 0.15 * height * sharpness;

            var bottom = baseHeight - Range;
            var top = baseHeight + Range;

            var baseDensity = 0.5 - Math.Max(0, Math.Min((double) 1, (py - bottom) / (top - bottom)));

            var noise = PerlinDensity.Noise(nx, ny, nz, 4);

            return 0.1f * roughness * noise + baseDensity;
        }

        private double HeightCurve(double initial)
        {//1.3*x^5 - 0.55*x^3 + 0.15*x
            double powered3 = Math.Pow(initial, 3);
            double powered5 = Math.Pow(initial, 5);
            double powered = 1.3 * powered5 - 0.55 * powered3 + 0.15 * initial;
            return powered;
        }

        private double SharpnessCurve(double initial)
        {
            return Math.Pow(initial, 8);
        }

        private double RoughnessCurve(double initial)
        {
            double powered = Math.Pow(initial, 3);
            return initial + 0.45 * (powered - initial);
        }
    }
}
