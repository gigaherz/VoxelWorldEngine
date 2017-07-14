using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Rendering
{
    public class FullscreenQuad : Mesh<VertexPositionTexture>
    {
        public FullscreenQuad(Game game)
            : base(game, null, new [] {
            new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0))
            },  new [] { 0, 1, 2, 2, 3, 0 })
        {
        }
    }
}