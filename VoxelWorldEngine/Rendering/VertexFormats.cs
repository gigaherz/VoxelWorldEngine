using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelWorldEngine.Rendering
{
    public static class VertexFormats
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PosColorTexNormal : IVertexType
        {
            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0), 
                new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0), 
                new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0));

            public Vector3 Position;
            public Color Color;
            public Vector2 TextureCoordinate;
            public Vector3 Normal;

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

            public PosColorTexNormal(Vector3 position, Vector3 normal, Color color, Vector2 textureCoordinate)
            {
                Position = position;
                Normal = normal;
                Color = color;
                TextureCoordinate = textureCoordinate;
            }

            public static bool operator ==(PosColorTexNormal left, PosColorTexNormal right)
            {
                return
                    left.Position == right.Position &&
                    left.Normal == right.Normal &&
                    left.Color == right.Color && 
                    left.TextureCoordinate == right.TextureCoordinate;
            }

            public static bool operator !=(PosColorTexNormal left, PosColorTexNormal right)
            {
                return !(left == right);
            }

            public override int GetHashCode()
            {
                return 0;
            }

            public override string ToString()
            {
                return $"{{Position:{Position} Normal:{Normal} Color:{Color} TextureCoordinate:{TextureCoordinate}}}";
            }

            public override bool Equals(object obj)
            {
                if (obj == null || obj.GetType() != GetType())
                    return false;
                return this == (PosColorTexNormal)obj;
            }
        }
    }
}
