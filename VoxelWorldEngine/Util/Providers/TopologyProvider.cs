using System;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Util.Providers
{
    public class TopologyProvider : ValueProvider2D<(double, double, double)>
    {
        private readonly ValueProvider2D<double> heightSource;
        private readonly ValueProvider2D<double> roughnessSource;
        private readonly NoiseOctaves noiseOffset;

        // Computed values for terrain configuration
        private int DepthLevel;
        private int TopLevel;
        private int Average;
        private int Range;

        public TopologyProvider(
                ValueProvider2D<double> heightSource,
                ValueProvider2D<double> roughnessSource,
                NoiseOctaves noiseOffset,
                int heightAmplitude,
                int flatlandsHeightOffset,
                int waterLevel)
        {
            this.heightSource = heightSource;
            this.roughnessSource = roughnessSource;
            this.noiseOffset = noiseOffset;

            // Computed values for terrain configuration
            DepthLevel = flatlandsHeightOffset + waterLevel - heightAmplitude;
            TopLevel = flatlandsHeightOffset + waterLevel + heightAmplitude;
            Average = (TopLevel + DepthLevel) / 2;
            Range = (TopLevel - DepthLevel) / 2;
        }

        public override (double, double, double) Get(int x, int z)
        {
            var height = heightSource.Get(x,z);

            double roughness = 0;
#if true
            var biomePlateauHeight = 10;
            var mountainness = MathX.Clamp((height - biomePlateauHeight) * 0.0001, -1, 1);

            roughness = 0.4 + roughnessSource.Get(x, z) * 0.25;
            roughness *= roughness;
            roughness = 0.25 * MathX.Clamp(roughness + mountainness, 0, 1); // mountains are more rough than plains
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

            var baseHeight = Average + height;
#if false
            int yOffset = 0;
            if (phc > 0)
                GetCliff(out yOffset, sxyz);
            
            baseHeight += roughness * Math.Pow(yOffset/20.0,2)*20;
#endif

            double bottom = baseHeight - Range * 0.5;
            double top = baseHeight + Range * 0.5;

            return (roughness, bottom, top);
        }

        private void GetCliff(out int yOffset, Vector2D sxyz)
        {
            var y0 = noiseOffset.Noise(sxyz.X, sxyz.Y, 5);
            var y1 = noiseOffset.Noise(-sxyz.Y, sxyz.X, 5);
            var o0 = (int)(y0 * 29);
            var o1 = (int)(y1 * 30) - 5 & o0;
            yOffset = Math.Max(0, o1 - o0);
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
