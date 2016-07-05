using System.Collections.Generic;
using System.Security;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Rendering
{
    class VertexCollectorManager
    {
        public readonly Dictionary<RenderQueue, MeshBuilder> Collectors = new Dictionary<RenderQueue, MeshBuilder>();

        public MeshBuilder Get(RenderQueue queue)
        {
            MeshBuilder collector;
            if (!Collectors.TryGetValue(queue, out collector))
            {
                collector = new MeshBuilder(queue);
                Collectors.Add(queue, collector);
            }
            return collector;
        }
    }

    class MeshBuilder
    {
        private readonly List<VertexFormats.PosColorTexNormal> _vertices = new List<VertexFormats.PosColorTexNormal>(10000);
        private readonly List<int> _indices = new List<int>(10000);

        public RenderQueue Queue { get; }

        public MeshBuilder(RenderQueue queue)
        {
            Queue = queue;
        }

        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
        }

        public Mesh Build(Game game)
        {
            if (_vertices.Count > 0 && _indices.Count > 0)
            {
                return new Mesh(game, Queue, _vertices.ToArray(), _indices.ToArray());
            }
            return null;
        }

        public void AddTriangle(
            VertexFormats.PosColorTexNormal v0,
            VertexFormats.PosColorTexNormal v1,
            VertexFormats.PosColorTexNormal v2)
        {
            var i0 = _vertices.Count;
            _vertices.Add(v0);
            _vertices.Add(v1);
            _vertices.Add(v2);

            _indices.Add(i0 + 0);
            _indices.Add(i0 + 1);
            _indices.Add(i0 + 2);
        }

        public void AddQuad(
            VertexFormats.PosColorTexNormal v0,
            VertexFormats.PosColorTexNormal v1,
            VertexFormats.PosColorTexNormal v2,
            VertexFormats.PosColorTexNormal v3)
        {
            var i0 = _vertices.Count;
            _vertices.Add(v0);
            _vertices.Add(v1);
            _vertices.Add(v2);
            _vertices.Add(v3);

            _indices.Add(i0 + 0);
            _indices.Add(i0 + 1);
            _indices.Add(i0 + 2);
            _indices.Add(i0 + 0);
            _indices.Add(i0 + 2);
            _indices.Add(i0 + 3);
        }
    }
}
