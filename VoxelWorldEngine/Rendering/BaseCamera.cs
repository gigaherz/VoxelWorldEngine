using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelWorldEngine.Rendering
{
    public class BaseCamera
    {
        public float NearClip { get; set; }
        public float FieldOfView { get; set; }
        public float FarClip { get; set; }
        public float AspectRatio { get; set; }
        public GraphicsDevice GraphicsDevice { get; private set; }
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; }

        public Matrix Projection => Matrix.CreatePerspectiveFieldOfView(FieldOfView, -AspectRatio, NearClip, FarClip);
        public Matrix View => Matrix.CreateLookAt(Vector3.Zero, Forward, Vector3.UnitY);
        public Matrix World { get; set; }

        public BoundingFrustum ViewFrustum { get; set; }

        public BaseCamera(GraphicsDevice device)
        {
            GraphicsDevice = device;
        }
    }
}