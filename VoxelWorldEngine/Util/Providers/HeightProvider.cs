using System;

namespace VoxelWorldEngine.Util.Providers
{
    public class HeightProvider : ValueProvider2D<double>
    {
        private readonly ValueProvider2D<double> source;

        public HeightProvider(ValueProvider2D<double> source)
        {
            this.source = source;
        }

        public override double Get(int x, int z)
        {
            return HeightCurve(source.Get(x, z));
        }

        private double HeightCurve(double x)
        {
            //0.3*x+0.1*x^3+0.065*sin(x*2*3.141592)
            //var outp = 0.3 * inp + 0.1 * Math.Pow(inp, 3) + 0.065 * Math.Sin(inp * 2 * Math.PI);
            x *= 2;
            var outp = Math.Sign(x) * (Math.Sin(x * x * 4) + 4.5 * x * x) * 7;
            return outp;
        }

    }
}
