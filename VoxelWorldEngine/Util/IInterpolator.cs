using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelWorldEngine.Util
{
    public abstract class Interpolator<T>
    {
        public abstract T Lerp(T v0, T v1, double t);

        public virtual T Lerp(
            T v000, T v001, T v010, T v011,
            T v100, T v101, T v110, T v111,
            double tx, double ty, double tz)
        {
            return Lerp(
                    Lerp(v000, v001, v010, v011, tx, ty),
                    Lerp(v100, v101, v110, v111, tx, ty),
                    tz
                );
        }

        public virtual T Lerp(T v00, T v01, T v10, T v11, double tx, double ty)
        {
            return Lerp(
                    Lerp(v00, v01, tx),
                    Lerp(v10, v11, tx),
                    ty
                );
        }

    }

    public class DoubleInterpolator : Interpolator<double>
    {
        public override double Lerp(double a, double b, double t)
        {
            return a + t * (b - a);
        }
    }
}
