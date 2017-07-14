using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;

namespace VoxelWorldEngine.Terrain
{
    public class GenerationContext
    {
        public Simplex PerlinDensity { get; }
        public Simplex PerlinHeight { get; }
        public Simplex PerlinRoughness { get; }
        public Simplex PerlinSharpness { get; }
        public Simplex PerlinOffset { get; }

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
        public int DepthLevel;
        public int TopLevel;
        public int Average;
        public int Range;

        public GenerationContext()
        {
            PerlinDensity = new Simplex(Seed);
            PerlinHeight = new Simplex(Seed * 3);
            PerlinRoughness = new Simplex(Seed * 5);
            PerlinSharpness = new Simplex(Seed * 7);
            PerlinOffset = new Simplex(Seed * 11);

            // Computed values for terrain configuration
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
        
        public void GetTopologyAt(Vector2I pxz, out double roughness, out double bottom, out double top)
        {
            var nxyz = pxz * VectorUtils.XZ(Tile.VoxelSize) / 2048.0f;

            var hxyz = nxyz / 2;
            var sxyz = nxyz * 7;
            var rxyz = nxyz / 3;
            var fxyz = nxyz * 63;

            int yOffset;
            GetCliff(out yOffset, sxyz);

            var rh = PerlinRoughness.Noise(rxyz.X, rxyz.Y, 2);
            roughness = rh; // RoughnessCurve(rh);

            var sh = PerlinSharpness.Noise(sxyz.X, sxyz.Y, 2);
            var fh = PerlinSharpness.Noise(fxyz.X, fxyz.Y, 2);
            var ash = (1 / (1+Math.Exp(sh*4))) - 0.5;
            var ph = PerlinHeight.Noise(hxyz.X, hxyz.Y, 5, 1.5 + 0.5 * ash);
            var phc = HeightCurve(ph);
            var height = phc - Math.Abs(fh) * 0.1 * Math.Max(0, Math.Min(1, phc * 0.25));

            roughness += height * 0.25;

            var baseHeight = Average + Range * 0.35 * height + roughness * Math.Pow(yOffset/20.0,2)*20;

            bottom = baseHeight - Range * 0.5;
            top = baseHeight + Range * 0.5;
        }

        private void GetCliff(out int yOffset, Vector2 sxyz)
        {
            var y0 = PerlinOffset.Noise(sxyz.X, sxyz.Y, 5);
            var y1 = PerlinOffset.Noise(-sxyz.Y, sxyz.X, 5);
            var o0 = (int)(y0 * 29);
            var o1 = (((int)(y1 * 30) - 5) & o0);
            yOffset = Math.Max(0, o1 - o0);
        }

        public void GetTopologyAt1(Vector2I pxz, out double roughness, out double bottom, out double top, out int yOffset)
        {
            var nxyz = pxz * VectorUtils.XZ(Tile.VoxelSize) / 2048.0f;

            var hxyz = nxyz / 2;
            var sxyz = nxyz * 7;
            var fxyz = nxyz * 63;
            
            yOffset = 0;
            roughness = 0;

            var ph = PerlinHeight.Noise(hxyz.X, hxyz.Y, 5, 1.5);
            var phc = HeightCurve(ph);
            var height = phc;

            roughness += height * 0.25;

            var baseHeight = Average + Range * 0.35 * height;

            bottom = baseHeight - Range * 0.5;
            top = baseHeight + Range * 0.5;
        }

        public double GetDensityAt(Vector3I pxyz, double roughness, double bottom, double top)
        {
            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (pxyz.Y - bottom) / (top - bottom)));

            var nxyz = pxyz * Tile.VoxelSize / 128.0f;

            var noise = 0.15 * PerlinDensity.Noise(nxyz.X, nxyz.Y, nxyz.Z, 4);

            return roughness * noise + baseDensity;
        }

        private double HeightCurve(double inp)
        {
            //0.3*x+0.7*x^3+0.065*sin(x*2*3.141592)
            return 0.3 * inp + 0.7 * Math.Pow(inp, 3) + 0.065 * Math.Sin(inp * 2 * Math.PI);
        }

        private double SharpnessCurve(double initial)
        {
            return Math.Abs(Math.Pow(initial, 3));
        }

        private double RoughnessCurve(double initial)
        {
            double powered = Math.Pow(initial, 3);
            return initial + 0.45 * (powered - initial);
        }
    }
}
