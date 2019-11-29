using System;
using System.Reflection;

namespace VoxelWorldEngine.Noise
{
    public abstract class NoiseOctaves
    {
        // Inner class to speed upp gradient computations
        // (In Java, array access is a lot slower than member access)
        public struct Grad3
        {
            internal double x, y, z;

            internal Grad3(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        public static readonly Grad3[] Gradients = {new Grad3(1,1,0),new Grad3(-1,1,0),new Grad3(1,-1,0),new Grad3(-1,-1,0),
            new Grad3(1,0,1),new Grad3(-1,0,1),new Grad3(1,0,-1),new Grad3(-1,0,-1),
            new Grad3(0,1,1),new Grad3(0,-1,1),new Grad3(0,1,-1),new Grad3(0,-1,-1)};

        public static readonly byte[] Permutations = {
            151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,140, 36,103, 30,
            69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,247,120,234, 75,  0, 26,197, 62,
            94,252,219,203,117, 35, 11, 32, 57,177, 33, 88,237,149, 56, 87,174, 20,125,136,
            171,168, 68,175, 74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
            60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54, 65, 25, 63,161,
            1,216, 80, 73,209, 76,132,187,208, 89, 18,169,200,196,135,130,116,188,159, 86,
            164,100,109,198,173,186,  3, 64, 52,217,226,250,124,123,  5,202, 38,147,118,126,
            255, 82, 85,212,207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
            119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,129, 22, 39,253,
            19, 98,108,110, 79,113,224,232,178,185,112,104,218,246, 97,228,251, 34,242,193,
            238,210,144, 12,191,179,162,241, 81, 51,145,235,249, 14,239,107, 49,192,214, 31,
            181,199,106,157,184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
            222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
        };

        // To remove the need for index wrapping, double the permutation table length
        public static readonly int[] Perm = new int[512];

        public static readonly int[] PermMod12 = new int[512];

        static NoiseOctaves()
        {
            for (int i = 0; i < 512; i++)
            {
                Perm[i] = Permutations[i & 255];
                PermMod12[i] = (short)(Perm[i] % 12);
            }
        }
        
        private readonly double offx;
        private readonly double offy;
        private readonly double offz;
        private readonly double _scale;

        public NoiseOctaves(int seed, double scale)
        {
            seed *= 65537;
            offx = (seed >> 4) & 255;
            offy = (seed >> 12) & 255;
            offz = (seed >> 20) & 255;
            _scale = scale;
        }

        public double Noise(double x, double y, int n, double alpha, double beta)
        {
            double sum = 0;
            double scale = 1;
            alpha = 1 / alpha;

            x *= _scale;
            y *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy);
                sum += val * scale;
                scale *= alpha;
                x *= beta;
                y *= beta;
            }
            return sum;
        }

        public double Noise(double x, double y, int n, double alpha)
        {
            double sum = 0;
            double scale = 1;
            alpha = 1 / alpha;

            x *= _scale;
            y *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy);
                sum += val * scale;
                scale *= alpha;
                x *= 2;
                y *= 2;
            }
            return sum;
        }

        public double Noise(double x, double y, int n)
        {
            double sum = 0;
            double scale = 1;

            x *= _scale;
            y *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy);
                sum += val * scale;
                scale *= .5;
                x *= 2;
                y *= 2;
            }
            return sum;
        }

        public double Noise(double x, double y, double z, int n, double alpha, double beta)
        {
            double sum = 0;
            double scale = 1;
            alpha = 1 / alpha;

            x *= _scale;
            y *= _scale;
            z *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy, z + offz);
                sum += val * scale;
                scale *= alpha;
                x *= beta;
                y *= beta;
                z *= beta;
            }
            return sum;
        }

        public double Noise(double x, double y, double z, int n, double alpha)
        {
            double sum = 0;
            double scale = 1;
            alpha = 1 / alpha;

            x *= _scale;
            y *= _scale;
            z *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy, z + offz);
                sum += val * scale;
                scale *= alpha;
                x *= 2;
                y *= 2;
                z *= 2;
            }
            return sum;
        }

        public double Noise(double x, double y, double z, int n)
        {
            double sum = 0;
            double scale = 1;

            x *= _scale;
            y *= _scale;
            z *= _scale;

            for (int i = 0; i < n; i++)
            {
                var val = SingleNoise(x + offx, y + offy, z + offz);
                sum += val * scale;
                scale *= .5;
                x *= 2;
                y *= 2;
                z *= 2;
            }
            return sum;
        }

        protected abstract double SingleNoise(double x, double y);
        protected abstract double SingleNoise(double x, double y, double z);

        // This method is a *lot* faster than using (int)Math.floor(x)
        public static int fastfloor(double x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        public static double lerp(double a, double b, double t) { return a + t * (b - a); }

    }
}