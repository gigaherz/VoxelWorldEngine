using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Rendering
{
    public class Mesh : IDisposable
    {
        VertexFormats.PosColorTexNormal[] vertices;
        int[] indices;

        private VertexBuffer _vbuffer;
        private IndexBuffer _ibuffer;
        
        public RenderQueue Queue { get; }

        public GraphicsDevice GraphicsDevice { get; set; }

        public Mesh(Game game, RenderQueue queue, VertexFormats.PosColorTexNormal[] vertices, int[] indices)
        {
            Queue = queue;

            GraphicsDevice = game.GraphicsDevice;

            this.vertices = vertices;
            this.indices = indices;
        }

        bool isInitialized;
        public void MakeBuffers()
        {
            if(isInitialized)
                return;

            isInitialized = true;

            _vbuffer?.Dispose();
            _vbuffer = new VertexBuffer(GraphicsDevice, VertexFormats.PosColorTexNormal.VertexDeclaration, vertices.Length,
                BufferUsage.WriteOnly);
            _vbuffer.SetData(vertices);
            vertices = null;

            _ibuffer?.Dispose();
            _ibuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _ibuffer.SetData(indices);
            indices = null;
        }

        public virtual void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetVertexBuffer(_vbuffer);
            GraphicsDevice.Indices = _ibuffer;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _ibuffer.IndexCount / 3);
        }

        public void Dispose()
        {
            if (isInitialized)
            {
                _vbuffer.Dispose();
                _ibuffer.Dispose();
            }
        }
    }
}
