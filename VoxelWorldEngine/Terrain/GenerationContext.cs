using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;

namespace VoxelWorldEngine.Terrain
{
    public class GenerationContext
    {
        public NoiseOctaves PerlinDensity { get; }
        public NoiseOctaves PerlinHeight { get; }
        public NoiseOctaves PerlinRoughness { get; }
        public NoiseOctaves PerlinSharpness { get; }
        public NoiseOctaves PerlinOffset { get; }

        public int Seed { get; } = /* 1234512345; //*/ (int)(DateTime.UtcNow.Ticks % 2147483647);

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

        // Computed values for terrain configuration
        public int DepthLevel;
        public int TopLevel;
        public int Average;
        public int Range;

        public GenerationContext()
        {
            PerlinDensity = new Simplex(Seed, 0.05);
            PerlinHeight = new Simplex(Seed * 31, 0.25);
            PerlinRoughness = new Simplex(Seed * 53, 0.333);
            PerlinSharpness = new Simplex(Seed * 71, 1/7.0);
            PerlinOffset = new Simplex(Seed * 113, 1);

            // Computed values for terrain configuration
            DepthLevel = FlatlandsHeightOffset + WaterLevel - HeightAmplitude;
            TopLevel = FlatlandsHeightOffset + WaterLevel + HeightAmplitude;
            Average = (TopLevel + DepthLevel) / 2;
            Range = (TopLevel - DepthLevel) / 2;
        }

        public void Initialize()
        {
        }

        private static double Clamp(double v, double min, double max)
        {
            return Math.Max(min, Math.Min(max, v));
        }

        private static double S(double x)
        {
            return 1 / (1 + Math.Exp(x * -4));
        }

        public void GetTopologyAt(Vector2I pxz, out double roughness, out double bottom, out double top)
        {
            var nxyz = pxz * (Vector2D)VectorUtils.XZ(Tile.VoxelSize) / 2048.0f;

            var height = HeightCurve(PerlinHeight.Noise(nxyz.X, nxyz.Y, HeightOctaves)*2);

            roughness = 0;
#if true
            var biomePlateauHeight = 10;
            var mountainness = Clamp((height - biomePlateauHeight) * 0.01, -1, 1);

            roughness = PerlinRoughness.Noise(nxyz.X, nxyz.Y, RoughnessOctaves);
            roughness *= roughness;
            roughness = Clamp(roughness + mountainness, 0, 1); // mountains are more rough than plains
#endif

#if false
            var sxyz0 = nxyz;
            var sharpness = 0; // S(PerlinSharpness.Noise(sxyz0.X, sxyz0.Y, 2) + mountainness);

            //var fxyz = nxyz * 63;
            //var fh = PerlinSharpness.Noise(fxyz.X, fxyz.Y, 2);

            var sxyz = nxyz * 7;
            height += sharpness * PerlinHeight.Noise(sxyz.X, sxyz.Y, 2, 1.5);

            //height = height - Math.Abs(fh) * 0.1 * Clamp(height * 0.25, 0, 1);
            
#endif

            var baseHeight = Average + Range * 0.35 * height;
#if false
            int yOffset = 0;
            if (phc > 0)
                GetCliff(out yOffset, sxyz);
            
            baseHeight += roughness * Math.Pow(yOffset/20.0,2)*20;
#endif

            bottom = baseHeight - Range * 0.5;
            top = baseHeight + Range * 0.5;
        }

        private void GetCliff(out int yOffset, Vector2D sxyz)
        {
            var y0 = PerlinOffset.Noise(sxyz.X, sxyz.Y, 5);
            var y1 = PerlinOffset.Noise(-sxyz.Y, sxyz.X, 5);
            var o0 = (int)(y0 * 29);
            var o1 = ((int)(y1 * 30) - 5) & o0;
            yOffset = Math.Max(0, o1 - o0);
        }

        public double GetRawDensityAt(Vector3D nxyz)
        {
            return PerlinDensity.Noise(nxyz.X, nxyz.Y, nxyz.Z, DensityOctaves);
        }

        public double GetDensityAt(Vector3I pxyz, double roughness, double bottom, double top, double rawDensity)
        {
            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (pxyz.Y - bottom) / (top - bottom)));
            var noise = 0.15 * rawDensity;
            return roughness * noise + baseDensity;
        }

        public double GetDensityAt(Vector3I pxyz, double roughness, double bottom, double top)
        {
            return GetDensityAt(pxyz, roughness, bottom, top, GetRawDensityAt(pxyz));
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
