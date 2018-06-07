using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Rendering
{
    public class Mesh<T> : IDisposable
        where T: struct, IVertexType
    {
        private readonly VertexBuffer _vbuffer;
        private readonly IndexBuffer _ibuffer;
        
        public RenderQueue Queue { get; }

        public GraphicsDevice GraphicsDevice { get; set; }

        private Game _game;

        public Mesh(Game game, RenderQueue queue, T[] vertices, int[] indices)
            : this(game, queue, vertices, vertices.Length, indices, indices.Length)
        {
        }

        public Mesh(Game game, RenderQueue queue, T[] vertices, int verticesLength, int[] indices, int indicesLength)
        {
            _game = game;

            Queue = queue;

            GraphicsDevice = game.GraphicsDevice;

            _vbuffer = new VertexBuffer(GraphicsDevice, FindDeclaration(typeof(T)), verticesLength, BufferUsage.WriteOnly);
            _vbuffer.SetData(vertices, 0, verticesLength);

            _ibuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indicesLength, BufferUsage.WriteOnly);
            _ibuffer.SetData(indices, 0, indicesLength);
        }

        private static VertexDeclaration FindDeclaration(Type type)
        {
            var field = type.GetField("VertexDeclaration");
            var decl = (VertexDeclaration)field?.GetValue(null);
            return decl;
        }

        public virtual void Draw(GameTime gameTime)
        {
            ReadyBuffers();
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _ibuffer.IndexCount / 3);
            StatManager.PerFrame["DrawCalls"].Increment();
        }

        public void ReadyBuffers()
        {
            GraphicsDevice.SetVertexBuffer(_vbuffer);
            GraphicsDevice.Indices = _ibuffer;
        }

        public void JustDraw(GameTime gameTime)
        {
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _ibuffer.IndexCount / 3);
            StatManager.PerFrame["DrawCalls"].Increment();
        }

        public void Dispose()
        {
            _vbuffer?.Dispose();
            _ibuffer?.Dispose();
        }
    }
}
