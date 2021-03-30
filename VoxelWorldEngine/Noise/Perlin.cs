using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Noise
{
    public class Perlin : NoiseOctaves
    {
        public Perlin(int seed, Vector3D scale) : base(seed, scale)
        {
        }

        protected double SingleNoise(double x)
        {
            int ix = MathX.FastFloor(x);
            double fx0 = x - ix;
            double fx1 = fx0 - 1;
            int jx = ix & 255;

            int index = PermMod12[jx];
            int index1 = PermMod12[jx + 1];
            var g0 = Gradients[index];
            var g1 = Gradients[index1];

            double vx0 = g0.x * fx0;
            double vx1 = g1.x * fx1;
            return vx0 + fx0 * (vx1 - vx0);
        }

        protected override double SingleNoise(double x, double y)
        {
            int ix = MathX.FastFloor(x);
            double fx0 = x - ix;
            double fx1 = fx0 - 1;
            int jx = ix & 255;

            int iy = MathX.FastFloor(y);
            double fy0 = y - iy;
            double fy1 = fy0 - 1;
            int jy = iy & 255;

            var py = Perm[jy];
            int index = PermMod12[jx + py];
            int index1 = PermMod12[jx + 1 + py];
            var g0 = Gradients[index];
            var g1 = Gradients[index1];

            double vx0 = g0.x * fx0 + g0.y * fy0;
            double vx1 = g1.x * fx1 + g1.y * fy0;
            double vy0 = vx0 + fx0 * (vx1 - vx0);

            var py1 = Perm[jy + 1];
            int index2 = PermMod12[jx + py1];
            int index3 = PermMod12[jx + 1 + py1];
            var g2 = Gradients[index2];
            var g3 = Gradients[index3];

            vx0 = g2.x * fx0 + g2.y * fy1;
            vx1 = g3.x * fx1 + g3.y * fy1;
            double vy1 = vx0 + fx0 * (vx1 - vx0);

            return vy0 + fy0 * (vy1 - vy0);
        }

        protected override double SingleNoise(double x, double y, double z)
        {
            int ix = MathX.FastFloor(x);
            double fx0 = x - ix;
            double fx1 = fx0 - 1;
            int jx = ix & 255;

            int iy = MathX.FastFloor(y);
            double fy0 = y - iy;
            double fy1 = fy0 - 1;
            int jy = iy & 255;

            int iz = MathX.FastFloor(z);
            double fz0 = z - iz;
            double fz1 = fz0 - 1;
            int jz = iz & 255;

            var pz = Perm[jz];
            var pyz = Perm[jy + pz];
            int index = PermMod12[jx + pyz];
            int index1 = PermMod12[jx + 1 + pyz];
            var g0 = Gradients[index];
            var g1 = Gradients[index1];

            double vx0 = g0.x * fx0 + g0.y * fy0 + g0.z * fz0;
            double vx1 = g1.x * fx1 + g1.y * fy0 + g1.z * fz0;
            double vy0 = vx0 + fx0 * (vx1 - vx0);

            var py1z = Perm[jy + 1 + pz];
            int index2 = PermMod12[jx + py1z];
            int index3 = PermMod12[jx + 1 + py1z];
            var g2 = Gradients[index2];
            var g3 = Gradients[index3];

            vx0 = g2.x * fx0 + g2.y * fy1 + g2.z * fz0;
            vx1 = g3.x * fx1 + g3.y * fy1 + g3.z * fz0;
            double vy1 = vx0 + fx0 * (vx1 - vx0);
            double vz0 = vy0 + fy0 * (vy1 - vy0);

            var pz1 = Perm[jz + 1];
            var pzy1 = Perm[jy + pz1];
            int index4 = PermMod12[jx + pzy1];
            int index5 = PermMod12[jx + 1 + pzy1];
            var g4 = Gradients[index4];
            var g5 = Gradients[index5];

            vx0 = g4.x * fx0 + g4.y * fy0 + g4.z * fz1;
            vx1 = g5.x * fx1 + g5.y * fy0 + g5.z * fz1;
            vy0 = vx0 + fx0 * (vx1 - vx0);

            var py1z1 = Perm[jy + 1 + pz1];
            int index6 = PermMod12[jx + py1z1];
            int index7 = PermMod12[jx + 1 + py1z1];
            var g6 = Gradients[index6];
            var g7 = Gradients[index7];

            vx0 = g6.x * fx0 + g6.y * fy1 + g6.z * fz1;
            vx1 = g7.x * fx1 + g7.y * fy1 + g7.z * fz1;
            vy1 = vx0 + fx0 * (vx1 - vx0);
            double vz1 = vy0 + fy0 * (vy1 - vy0);

            return vz0 + fz0 * (vz1 - vz0);
        }
    }
}
