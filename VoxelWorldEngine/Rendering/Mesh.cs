using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Rendering
{
    public class Mesh : IDisposable
    {
        bool isInitialized;

        private VertexBuffer _vbuffer;
        private IndexBuffer _ibuffer;
        
        public RenderQueue Queue { get; }

        public GraphicsDevice GraphicsDevice { get; set; }

        public Mesh(Game game, RenderQueue queue, VertexFormats.PosColorTexNormal[] vertices, int verticesLength, int[] indices, int indicesLength)
        {
            Queue = queue;

            GraphicsDevice = game.GraphicsDevice;

            _vbuffer?.Dispose();
            _vbuffer = new VertexBuffer(GraphicsDevice, VertexFormats.PosColorTexNormal.VertexDeclaration, verticesLength, BufferUsage.WriteOnly);
            _vbuffer.SetData(vertices, 0, verticesLength);

            _ibuffer?.Dispose();
            _ibuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indicesLength, BufferUsage.WriteOnly);
            _ibuffer.SetData(indices, 0, indicesLength);
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
