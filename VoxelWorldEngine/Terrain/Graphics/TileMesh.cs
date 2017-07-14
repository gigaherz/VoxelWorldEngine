using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Rendering;

namespace VoxelWorldEngine.Terrain.Graphics
{
    public class TileMesh : Mesh<VertexFormats.PosColorTexNormal>
    {
        public TileMesh(Game game, RenderQueue queue, VertexFormats.PosColorTexNormal[] vertices, int verticesLength, int[] indices, int indicesLength)
            : base(game, queue, vertices, verticesLength, indices, indicesLength)
        {
        }
    }
}