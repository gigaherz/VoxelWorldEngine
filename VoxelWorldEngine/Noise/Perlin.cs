using System;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Noise
{
    /* Coherent noise function over 1, 2 or 3 dimensions */
    /* (copyright Ken Perlin) */

    public abstract class PerlinCommon
    {
        protected const int B = 0x100;
        protected const int BM = 0xff;
        protected const int N = 0x1000;

        protected static double SCurve(double t)
        {
            return t * t * (3.0 - 2.0 * t);
        }

        protected static double Lerp(double t, double a, double b)
        {
            return a + t * (b - a);
        }

        protected static void Setup(double vec, out int b0, out int b1, out double r0, out double r1)
        {
            var t = vec + N;
            b0 = (int)t & BM;
            b1 = (b0 + 1) & BM;
            r0 = t - (int)t;
            r1 = r0 - 1.0;
        }

        protected readonly Random R;
        protected int[] P = new int[B + B + 2];
        protected bool FirstRun = true;

        protected PerlinCommon()
        {
            R = new Random();
        }

        protected PerlinCommon(int seed)
        {
            R = new Random(seed);
        }

    }

    public class Perlin1D : PerlinCommon
    {
        private readonly double[] _g1 = new double[B + B + 2];

        public Perlin1D(int seed)
            : base(seed)
        {
        }

        public void Initialize()
        {
            if (!FirstRun) return;

            FirstRun = false;

            int i;
            for (i = 0; i < B; i++)
            {
                P[i] = i;
                _g1[i] = R.NextDouble() * 2 - 1;
            }

            while (--i > 0)
            {
                var j = R.Next(B);
                var k = P[i];
                P[i] = P[j];
                P[j] = k;
            }

            for (i = 0; i < B + 2; i++)
            {
                P[B + i] = P[i];
                _g1[B + i] = _g1[i];
            }
        }

        private double Noise1(double x)
        {
            int bx0, bx1;
            double rx0, rx1;

            Setup(x, out bx0, out bx1, out rx0, out rx1);

            var i = P[bx0];
            var j = P[bx1];

            var sx = SCurve(rx0);
            var u = rx0 * _g1[i];
            var v = rx1 * _g1[j];

            return Lerp(sx, u, v);
        }

        /* --- My harmonic summing functions - PDB --------------------------*/

        /*
           In what follows "alpha" is the Weight when the sum is formed.
           Typically it is 2, As this approaches 1 the function is noisier.
           "beta" is the harmonic scaling/spacing, typically 2.
        */

        public double Noise(double x, int n, double alpha = 2, double beta = 2)
        {
            double sum = 0;
            double scale = 1;

            for (int i = 0; i < n; i++)
            {
                var val = Noise1(x);
                sum += val / scale;
                scale *= alpha;
                x *= beta;
            }
            return sum;
        }
    }

    public class Perlin2D : PerlinCommon
    {
        private readonly Vector2D[] _g2 = new Vector2D[B + B + 2];

        public Perlin2D(int seed)
            : base(seed)
        {
        }

        public void Initialize()
        {
            if (!FirstRun) return;

            FirstRun = false;

            int i;
            for (i = 0; i < B; i++)
            {
                P[i] = i;

                var v1 = R.NextDouble() * 2 - 1;
                var v2 = R.NextDouble() * 2 - 1;
                double s = 0;
                s += v1 * v1;
                s += v2 * v2;
                s = Math.Sqrt(s);
                _g2[i].X = v1 / s;
                _g2[i].Y = v2 / s;
            }

            while (--i > 0)
            {
                var j = R.Next(B);
                var k = P[i];
                P[i] = P[j];
                P[j] = k;
            }

            for (i = 0; i < B + 2; i++)
            {
                P[B + i] = P[i];
                _g2[B + i] = _g2[i];
            }
        }

        private double Noise2(double x, double y)
        {
            int bx0, bx1;
            double rx0, rx1;
            Setup(x, out bx0, out bx1, out rx0, out rx1);

            int by0, by1;
            double ry0, ry1;
            Setup(y, out by0, out by1, out ry0, out ry1);

            var i = P[bx0];
            var j = P[bx1];

            var b00 = P[i + by0];
            var b10 = P[j + by0];
            var b01 = P[i + by1];
            var b11 = P[j + by1];

            var sx = SCurve(rx0);
            var sy = SCurve(ry0);

            var q = _g2[b00]; var u = q.Dot(rx0, ry0);
            q = _g2[b10]; var v = q.Dot(rx1, ry0);
            var a = Lerp(sx, u, v);

            q = _g2[b01]; u = q.Dot(rx0, ry1);
            q = _g2[b11]; v = q.Dot(rx1, ry1);
            var b = Lerp(sx, u, v);

            return Lerp(sy, a, b);
        }

        /* --- My harmonic summing functions - PDB --------------------------*/

        /*
           In what follows "alpha" is the Weight when the sum is formed.
           Typically it is 2, As this approaches 1 the function is noisier.
           "beta" is the harmonic scaling/spacing, typically 2.
        */

        public double Noise(double x, double y, int n, double alpha = 2, double beta = 2)
        {
            double sum = 0;
            double scale = 1;

            for (int i = 0; i < n; i++)
            {
                var val = Noise2(x, y);
                sum += val / scale;
                scale *= alpha;
                x *= beta;
                y *= beta;
            }
            return sum;
        }
    }

    public class Perlin3D : PerlinCommon
    {
        private readonly Vector3D[] _g3 = new Vector3D[B + B + 2];

        public Perlin3D(int seed)
            : base(seed)
        {
        }

        public void Initialize()
        {
            if (!FirstRun) return;

            FirstRun = false;

            int i;
            for (i = 0; i < B; i++)
            {
                P[i] = i;

                var v1 = R.NextDouble() * 2 - 1;
                var v2 = R.NextDouble() * 2 - 1;
                var v3 = R.NextDouble() * 2 - 1;
                double s = 0;
                s += v1 * v1;
                s += v2 * v2;
                s += v3 * v3;
                s = Math.Sqrt(s);
                _g3[i].X = v1 / s;
                _g3[i].Y = v2 / s;
                _g3[i].Z = v3 / s;
            }

            while (--i > 0)
            {
                var j = R.Next(B);
                var k = P[i];
                P[i] = P[j];
                P[j] = k;
            }

            for (i = 0; i < B + 2; i++)
            {
                P[B + i] = P[i];
                _g3[B + i] = _g3[i];
            }
        }

        private double Noise3(double x, double y, double z)
        {
            int bx0, bx1;
            double rx0, rx1;
            Setup(x, out bx0, out bx1, out rx0, out rx1);

            int by0, by1;
            double ry0, ry1;
            Setup(y, out by0, out by1, out ry0, out ry1);

            int bz0, bz1;
            double rz0, rz1;
            Setup(z, out bz0, out bz1, out rz0, out rz1);

            var i = P[bx0];
            var j = P[bx1];

            var b00 = P[i + by0];
            var b10 = P[j + by0];
            var b01 = P[i + by1];
            var b11 = P[j + by1];

            var t = SCurve(rx0);
            var sy = SCurve(ry0);
            var sz = SCurve(rz0);

            var u = _g3[b00 + bz0].Dot(rx0, ry0, rz0);
            var v = _g3[b10 + bz0].Dot(rx1, ry0, rz0);
            var a = Lerp(t, u, v);

            u = _g3[b01 + bz0].Dot(rx0, ry1, rz0);
            v = _g3[b11 + bz0].Dot(rx1, ry1, rz0);
            var b = Lerp(t, u, v);

            var c = Lerp(sy, a, b);

            u = _g3[b00 + bz1].Dot(rx0, ry0, rz1);
            v = _g3[b10 + bz1].Dot(rx1, ry0, rz1);
            a = Lerp(t, u, v);

            u = _g3[b01 + bz1].Dot(rx0, ry1, rz1);
            v = _g3[b11 + bz1].Dot(rx1, ry1, rz1);
            b = Lerp(t, u, v);

            var d = Lerp(sy, a, b);

            return Lerp(sz, c, d);
        }

        /* --- My harmonic summing functions - PDB --------------------------*/

        /*
           In what follows "alpha" is the Weight when the sum is formed.
           Typically it is 2, As this approaches 1 the function is noisier.
           "beta" is the harmonic scaling/spacing, typically 2.
        */

        public double Noise(double x, double y, double z, int n, double alpha = 2, double beta = 2)
        {
            double sum = 0;
            double scale = 1;

            for (int i = 0; i < n; i++)
            {
                var val = Noise3(x, y, z);
                sum += val / scale;
                scale *= alpha;
                x *= beta;
                y *= beta;
                z *= beta;
            }
            return sum;
        }
    }
}
