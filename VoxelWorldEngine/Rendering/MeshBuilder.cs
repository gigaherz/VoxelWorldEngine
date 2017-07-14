using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Terrain.Graphics;

namespace VoxelWorldEngine.Rendering
{
    internal class MeshBuilder
    {
        private VertexFormats.PosColorTexNormal[] _vertices;
        private int[] _indices;

        int _vertexCount;
        int _indexCount;

        public RenderQueue Queue { get; set; }
        public int IndexCount => _indexCount;

        public void Clear()
        {
            _vertexCount = 0;
            _indexCount = 0;
        }

        public TileMesh Build(Game game)
        {
            if (_vertexCount > 0 && _indexCount > 0)
            {
                return new TileMesh(game, Queue, _vertices, _vertexCount, _indices, _indexCount);
            }
            return null;
        }

        private void AddVertex(VertexFormats.PosColorTexNormal vertex)
        {
            if (_vertices == null)
            {
                _vertices = new VertexFormats.PosColorTexNormal[1000];
            }
            else if (_vertexCount == _vertices.Length)
            {
                Array.Resize(ref _vertices, _vertices.Length + 1000);
            }

            _vertices[_vertexCount++] = vertex;
        }

        private void AddIndex(int index)
        {
            if (_indices == null)
            {
                _indices = new int[1000];
            }
            else if (_indexCount == _indices.Length)
            {
                Array.Resize(ref _indices, _indices.Length + 1000);
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
