namespace VoxelWorldEngine.Noise
{
    public abstract class NoiseOctaves
    {
        private readonly double offx;
        private readonly double offy;
        private readonly double offz;

        public NoiseOctaves(int seed)
        {
            seed *= 65537;
            offx = (seed >> 4) & 255;
            offy = (seed >> 12) & 255;
            offz = (seed >> 20) & 255;
        }

        public double Noise(double x, double y, int n, double alpha, double beta)
        {
            double sum = 0;
            double scale = 1;
            alpha = 1 / alpha;

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

        public virtual void Initialize()
        {
        }
    }
}