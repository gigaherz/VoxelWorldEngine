﻿/*
 * A speed-improved simplex noise algorithm for 2D, 3D and 4D in Java.
 *
 * Based on example code by Stefan Gustavson (stegu@itn.liu.se).
 * Optimisations by Peter Eastman (peastman@drizzle.stanford.edu).
 * Better rank ordering method for 4D by Stefan Gustavson in 2012.
 *
 * This could be speeded up even further, but it's useful as it is.
 *
 * Version 2012-03-09
 *
 * This code was placed in the public domain by its original author,
 * Stefan Gustavson. You may use it as you see fit, but
 * attribution is appreciated.
 *
 */

using System;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Noise
{
    public class Simplex : NoiseOctaves
    {
        public Simplex(int seed, Vector3D scale) : base(seed, scale)
        {
        }

        // Skewing and unskewing factors for 2, 3, and 4 dimensions
        private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
        private static readonly double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;
        private static readonly double F3 = 1.0 / 3.0;
        private static readonly double G3 = 1.0 / 6.0;

        protected override double SingleNoise(double xin, double yin)
        {
            // Skew the input space to determine which simplex cell we're in
            double s = (xin + yin) * F2; // Hairy factor for 2D
            int i = MathX.FastFloor(xin + s);
            int j = MathX.FastFloor(yin + s);
            double t = (i + j) * G2;
            double X0 = i - t; // Unskew the cell origin back to (x,y) space
            double Y0 = j - t;
            double x0 = xin - X0; // The x,y distances from the cell origin
            double y0 = yin - Y0;
            // For the 2D case, the simplex shape is an equilateral triangle.
            // Determine which simplex we are in.
            int i1 = 0, j1 = 1; // Offsets for second (middle) corner of simplex in (i,j) coords
            if (x0 > y0) { i1 = 1; j1 = 0; } // lower triangle, XY order: (0,0)->(1,0)->(1,1)
                                             // upper triangle, YX order: (0,0)->(0,1)->(1,1)
                                            // A step of (1,0) in (i,j) means a step of (1-c,-c) in (x,y), and
                                            // a step of (0,1) in (i,j) means a step of (-c,1-c) in (x,y), where
                                            // c = (3-Sqrt(3))/6
            double x1 = x0 - i1 + G2; // Offsets for middle corner in (x,y) unskewed coords
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2; // Offsets for last corner in (x,y) unskewed coords
            double y2 = y0 - 1.0 + 2.0 * G2;
            // Work out the hashed gradient indices of the three simplex corners
            int ii = i & 255;
            int jj = j & 255;
            int gi0 = PermMod12[ii + Perm[jj]];
            int gi1 = PermMod12[ii + i1 + Perm[jj + j1]];
            int gi2 = PermMod12[ii + 1 + Perm[jj + 1]];
            // Calculate the contribution from the three corners
            double t0 = 0.5 - x0 * x0 - y0 * y0;
            double n0 = 0.0;
            if (t0 >= 0)
            {
                t0 *= t0;
                var g = Gradients[gi0];
                n0 = t0 * t0 * (g.x * x0 + g.y * y0);  // (x,y) of Grad3 used for 2D gradient
            }
            double t1 = 0.5 - x1 * x1 - y1 * y1;
            double n1 = 0.0;
            if (t1 >= 0)
            {
                t1 *= t1;
                var g = Gradients[gi1];
                n1 = t1 * t1 * (g.x * x1 + g.y * y1);
            }
            double t2 = 0.5 - x2 * x2 - y2 * y2;
            double n2 = 0.0;
            if (t2 >= 0)
            {
                t2 *= t2;
                var g = Gradients[gi2];
                n2 = t2 * t2 * (g.x * x2 + g.y * y2);
            }
            // Add contributions from each corner to get the readonly noise value.
            // The result is scaled to return values in the interval [-1,1].
            return 70.0 * (n0 + n1 + n2);
        }

        protected override double SingleNoise(double xin, double yin, double zin)
        {
                                   // Skew the input space to determine which simplex cell we're in
            double s = (xin + yin + zin) * F3; // Very nice and simple skew factor for 3D
            int i = MathX.FastFloor(xin + s);
            int j = MathX.FastFloor(yin + s);
            int k = MathX.FastFloor(zin + s);
            double t = (i + j + k) * G3;
            double X0 = i - t; // Unskew the cell origin back to (x,y,z) space
            double Y0 = j - t;
            double Z0 = k - t;
            double x0 = xin - X0; // The x,y,z distances from the cell origin
            double y0 = yin - Y0;
            double z0 = zin - Z0;
            // For the 3D case, the simplex shape is a slightly irregular tetrahedron.
            // Determine which simplex we are in.
            int i1, j1, k1; // Offsets for second corner of simplex in (i,j,k) coords
            int i2, j2, k2; // Offsets for third corner of simplex in (i,j,k) coords
            if (x0 >= y0)
            {
                if (y0 >= z0)
                { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // X Y Z order
                else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; } // X Z Y order
                else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; } // Z X Y order
            }
            else
            { // x0<y0
                if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; } // Z Y X order
                else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; } // Y Z X order
                else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // Y X Z order
            }
            // A step of (1,0,0) in (i,j,k) means a step of (1-c,-c,-c) in (x,y,z),
            // a step of (0,1,0) in (i,j,k) means a step of (-c,1-c,-c) in (x,y,z), and
            // a step of (0,0,1) in (i,j,k) means a step of (-c,-c,1-c) in (x,y,z), where
            // c = 1/6.
            double x1 = x0 - i1 + G3; // Offsets for second corner in (x,y,z) coords
            double y1 = y0 - j1 + G3;
            double z1 = z0 - k1 + G3;
            double x2 = x0 - i2 + 2.0 * G3; // Offsets for third corner in (x,y,z) coords
            double y2 = y0 - j2 + 2.0 * G3;
            double z2 = z0 - k2 + 2.0 * G3;
            double x3 = x0 - 1.0 + 3.0 * G3; // Offsets for last corner in (x,y,z) coords
            double y3 = y0 - 1.0 + 3.0 * G3;
            double z3 = z0 - 1.0 + 3.0 * G3;
            // Work out the hashed gradient indices of the four simplex corners
            int ii = i & 255;
            int jj = j & 255;
            int kk = k & 255;
            int gi0 = PermMod12[ii + Perm[jj + Perm[kk]]];
            int gi1 = PermMod12[ii + i1 + Perm[jj + j1 + Perm[kk + k1]]];
            int gi2 = PermMod12[ii + i2 + Perm[jj + j2 + Perm[kk + k2]]];
            int gi3 = PermMod12[ii + 1 + Perm[jj + 1 + Perm[kk + 1]]];
            // Calculate the contribution from the four corners
            double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0;
            double n0 = 0.0;
            if (t0 >= 0)
            {
                t0 *= t0;
                var g = Gradients[gi0];
                n0 = t0 * t0 * (g.x * x0 + g.y * y0 + g.z * z0);
            }
            double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1;
            double n1 = 0.0;
            if (t1 >= 0)
            {
                t1 *= t1;
                var g = Gradients[gi1];
                n1 = t1 * t1 * (g.x * x1 + g.y * y1 + g.z * z1);
            }
            double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2;
            double n2 = 0.0;
            if (t2 >= 0)
            {
                t2 *= t2;
                var g = Gradients[gi2];
                n2 = t2 * t2 * (g.x * x2 + g.y * y2 + g.z * z2);
            }
            double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3;
            double n3 = 0.0;
            if (t3 >= 0)
            {
                t3 *= t3;
                var g = Gradients[gi3];
                n3 = t3 * t3 * (g.x * x3 + g.y * y3 + g.z * z3);
            }
            // Add contributions from each corner to get the readonly noise value.
            // The result is scaled to stay just inside [-1,1]
            return 32.0 * (n0 + n1 + n2 + n3);
        }
    }
}