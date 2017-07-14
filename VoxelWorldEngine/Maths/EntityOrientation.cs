using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Maths
{
    struct EntityOrientation
    {
        public float Yaw { get; set; }
        public float Pitch { get; set; }

        public Vector3 Forward
        {
            get
            {
                var cYaw = (float)Math.Cos(Yaw);
                var sYaw = (float)Math.Sin(Yaw);
                var cPitch = (float)Math.Cos(Pitch);
                var sPitch = (float)Math.Sin(Pitch);

                return new Vector3(sYaw * cPitch, -sPitch, cYaw * cPitch);
            }
        }

        public Vector3 HorizontalForward
        {
            get
            {
                var cYaw = (float)Math.Cos(Yaw);
                var sYaw = (float)Math.Sin(Yaw);

                return new Vector3(sYaw, 0, cYaw);
            }
        }

        public Vector3 HorizontalRight
        {
            get
            {
                var cYaw = (float)Math.Cos(Yaw);
                var sYaw = (float)Math.Sin(Yaw);

                return new Vector3(cYaw, 0, -sYaw);
            }
        }

        public void RotateYaw(float angle)
        {
            Yaw += angle;
        }

        public void RotatePitch(float angle)
        {
            Pitch = MathHelper.Clamp(Pitch + angle, -1.56f, 1.56f);
        }

        public override string ToString()
        {
            return $"{Yaw},{Pitch}";
        }
    }
}
