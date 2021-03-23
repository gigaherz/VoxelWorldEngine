using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxelWorldEngine.Noise;

namespace VoxelWorldEngine.Util
{
    public static class MathX
    {
        public static double Clamp(double v, double min, double max)
        {
            return Math.Max(min, Math.Min(max, v));
        }

        public static double S(double x)
        {
            return 1 / (1 + Math.Exp(x * -4));
        }

        // This method is a *lot* faster than using (int)Math.floor(x)
        public static int FastFloor(double x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }

        public static double Lerp(double a, double b, double t)
        {
            return a + t * (b - a);
        }

        public static double Lerp3D(int z, int x, int y, double[] rawDensity, int dim0, int dim1, double rdgd, double gz1, double gy1)
        {
            var xp = x * (rdgd / gz1);
            var zp = z * (rdgd / gz1);
            var yp = y * (rdgd / gy1);
            var xi = FastFloor(xp);
            var zi = FastFloor(zp);
            var yi = FastFloor(yp);
            var xt = xp - xi;
            var yt = yp - yi;
            var zt = zp - zi;

            var dy000 = rawDensity[(zi * dim1 + xi) * dim0 + yi];
            var dy001 = rawDensity[(zi * dim1 + xi) * dim0 + yi + 1];
            var dy010 = rawDensity[(zi * dim1 + xi + 1) * dim0 + yi];
            var dy011 = rawDensity[(zi * dim1 + xi + 1) * dim0 + yi + 1];
            var dy100 = rawDensity[((zi + 1) * dim1 + xi) * dim0 + yi];
            var dy101 = rawDensity[((zi + 1) * dim1 + xi) * dim0 + yi + 1];
            var dy110 = rawDensity[((zi + 1) * dim1 + xi + 1) * dim0 + yi];
            var dy111 = rawDensity[((zi + 1) * dim1 + xi + 1) * dim0 + yi + 1];

            var dx00 = Lerp(dy000, dy001, yt);
            var dx01 = Lerp(dy010, dy011, yt);
            var dx10 = Lerp(dy100, dy101, yt);
            var dx11 = Lerp(dy110, dy111, yt);

            var dz0 = Lerp(dx00, dx01, xt);
            var dz1 = Lerp(dx10, dx11, xt);

            var dd = Lerp(dz0, dz1, zt);
            return dd;
        }

    }
}
