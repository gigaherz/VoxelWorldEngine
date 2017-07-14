using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Rendering
{
    class DirectionalLight
    {
        Vector3 _direction;
        public Vector3 Direction
        {
            get
            {
                return _direction;
            }

            set
            {
                value.Normalize();
                _direction = value;
            }
        }

        public Vector4 Color { get; set; }
        public float Intensity { get; set; }

        //Constructor 
        public DirectionalLight(Vector3 Direction, Vector4 Color, float Intensity)
        {
            this.Direction = Direction;
            this.Color = Color;
            this.Intensity = Intensity;
        }

        public DirectionalLight(Vector3 Direction, Color Color, float Intensity)
        {
            this.Direction = Direction;
            this.Color = Color.ToVector4();
            this.Intensity = Intensity;
            this.Direction.Normalize();
        }
    }
}