using System;
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

        public void Clear()
        {
            foreach(var collector in Collectors.Values)
                collector.Clear();
        }
    }

    class MeshBuilder
    {
        private VertexFormats.PosColorTexNormal[] _vertices = new VertexFormats.PosColorTexNormal[10000];
        private int[] _indices = new int[10000];

        int _vertexCount;
        int _indexCount;

        public RenderQueue Queue { get; }
        public int IndexCount => _indexCount;

        public MeshBuilder(RenderQueue queue)
        {
            Queue = queue;
        }

        public void Clear()
        {
            _vertexCount = 0;
            _indexCount = 0;
        }

        public Mesh Build(Game game)
        {
            if (_vertexCount > 0 && _indexCount > 0)
            {
                return new Mesh(game, Queue, _vertices, _vertexCount, _indices, _indexCount);
            }
            return null;
        }

        private void AddVertex(VertexFormats.PosColorTexNormal vertex)
        {
            if (_vertexCount == _vertices.Length)
            {
                Array.Resize(ref _vertices, _vertices.Length + 10000);
            }

            _vertices[_vertexCount++] = vertex;
        }

        private void AddIndex(int index)
        {
            if (_indexCount == _indices.Length)
            {
                Array.Resize(ref _indices, _indices.Length + 10000);
            }

            _indices[_indexCount++] = index;
        }

        public void AddTriangle(
            VertexFormats.PosColorTexNormal v0,
            VertexFormats.PosColorTexNormal v1,
            VertexFormats.PosColorTexNormal v2)
        {
            var i0 = _vertexCount;
            AddVertex(v0);
            AddVertex(v1);
            AddVertex(v2);

            AddIndex(i0 + 0);
            AddIndex(i0 + 1);
            AddIndex(i0 + 2);
        }

        public void AddQuad(
            VertexFormats.PosColorTexNormal v0,
            VertexFormats.PosColorTexNormal v1,
            VertexFormats.PosColorTexNormal v2,
            VertexFormats.PosColorTexNormal v3)
        {
            var i0 = _vertexCount;
            AddVertex(v0);
            AddVertex(v1);
            AddVertex(v2);
            AddVertex(v3);

            AddIndex(i0 + 0);
            AddIndex(i0 + 1);
            AddIndex(i0 + 2);
            AddIndex(i0 + 0);
            AddIndex(i0 + 2);
            AddIndex(i0 + 3);
        }
    }
}
