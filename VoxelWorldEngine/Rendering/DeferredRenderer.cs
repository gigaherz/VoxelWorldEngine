using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelWorldEngine.Rendering
{
    class DeferredRenderer : DrawableGameComponent
    {
        private readonly Effect _clear;
        private readonly Effect _directionalLight;
        //private Effect pointLight;
        //private Effect spotLight; 
        private readonly Effect _compose;
        private readonly BlendState _lightMapBs;
        private readonly FullscreenQuad _fullScreenQuad;
        private RenderTargetBinding[] _gBufferTargets;
        private Vector2 _bufferTextureSize;
        private RenderTarget2D _lightMap;
        //private Model pointLightGeometry;
        //private Model spotLightGeometry;

        public RenderTarget2D Colors { get; private set; }
        public RenderTarget2D Albedo { get; private set; }
        public RenderTarget2D Normals { get; private set; }
        public RenderTarget2D Position { get; private set; }

        public Color ClearColor { get; set; } = new Color(Color.CornflowerBlue, 0);

        public DeferredRenderer(Game game, ContentManager content, int width, int height)
            : base(game)
        {
            _clear = content.Load<Effect>("Clear");
            _clear.CurrentTechnique = _clear.Techniques[0];

            _directionalLight = content.Load<Effect>("DirectionalLight");
            _directionalLight.CurrentTechnique = _directionalLight.Techniques[0];

            //pointLight = content.Load<Effect>("PointLight");
            //pointLight.CurrentTechnique = pointLight.Techniques[0];

            //spotLight = content.Load<Effect>("SpotLight");
            //spotLight.CurrentTechnique = spotLight.Techniques[0];

            _compose = content.Load<Effect>("Composition");
            _compose.CurrentTechnique = _compose.Techniques[0];

            _lightMapBs = new BlendState
            {
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.One,
                ColorBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One,
                AlphaBlendFunction = BlendFunction.Add
            };

            _fullScreenQuad = new FullscreenQuad(game);

            //pointLightGeometry = content.Load<Model>("PointLightGeometry");
            //spotLightGeometry = content.Load<Model>("SpotLightGeometry");

            CreateRenderTargets(width, height);
            VoxelGame.Instance.ResolutionChanged += (sender, args) =>
            {
                CreateRenderTargets(args.Width, args.Height);
            };
        }

        private void CreateRenderTargets(int width, int height)
        {
            Colors?.Dispose();
            Albedo?.Dispose();
            Normals?.Dispose();
            Position?.Dispose();

#if OPENGL
            Colors = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            Albedo = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            Normals = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
            Depth = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Vector2, DepthFormat.Depth24Stencil8);
#else
            Colors = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8);
            Albedo = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8);
            Normals = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8);
            Position = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Vector4, DepthFormat.Depth24Stencil8);
#endif
            _bufferTextureSize = new Vector2(width, height);
            _gBufferTargets = new[] {
                new RenderTargetBinding(Colors),
                new RenderTargetBinding(Normals),
                new RenderTargetBinding(Position),
                new RenderTargetBinding(Albedo)
            };

            _lightMap?.Dispose();
            _lightMap = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        }

        public void Draw(GameTime gameTime, IEnumerable<IRenderable> renderables, LightManager lights, BaseCamera camera, RenderTarget2D output)
        {
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(ClearColor);

            ClearGBuffer(gameTime);

            MakeGBuffer(gameTime, renderables, camera);

            MakeLightMap(gameTime, lights, camera);

            MakeFinal(gameTime, output);
        }

        private void ClearGBuffer(GameTime gameTime)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            GraphicsDevice.SetRenderTargets(_gBufferTargets);
            _clear.Parameters["ClearColor"]?.SetValue(ClearColor.ToVector4());
            _clear.CurrentTechnique.Passes[0].Apply();
            _fullScreenQuad.Draw(gameTime);
        }

        private void MakeGBuffer(GameTime gameTime, IEnumerable<IRenderable> renderables, BaseCamera camera)
        {
            GraphicsDevice.SetRenderTargets(_gBufferTargets);

            foreach (var renderable in renderables)
            {
                //if (renderable.)
                renderable.Draw(gameTime, camera);
            }

            GraphicsDevice.SetRenderTargets(null);
        }

        private void MakeLightMap(GameTime gameTime, LightManager lights, BaseCamera camera)
        {
            GraphicsDevice.SetRenderTarget(_lightMap);
            GraphicsDevice.Clear(/*new Color(0.3f,0.3f,0.3f,0.0f) /**/Color.TransparentBlack/**/);
            GraphicsDevice.BlendState = _lightMapBs;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _directionalLight.Parameters["ColorBuffer"]?.SetValue(Colors);
            _directionalLight.Parameters["NormalBuffer"]?.SetValue(Normals);
            _directionalLight.Parameters["DepthBuffer"]?.SetValue(Position);
            _directionalLight.Parameters["AlbedoBuffer"]?.SetValue(Albedo);

            var inverseView = Matrix.Invert(camera.View);
            var inverseViewProjection = Matrix.Invert(camera.View * camera.Projection);

            _directionalLight.Parameters["InverseViewProjection"]?.SetValue(inverseViewProjection);
            _directionalLight.Parameters["inverseView"]?.SetValue(inverseView);
            _directionalLight.Parameters["CameraPosition"]?.SetValue(camera.Position);
            _directionalLight.Parameters["BufferTextureSize"]?.SetValue(_bufferTextureSize);

            _fullScreenQuad.ReadyBuffers();

            foreach (var light in lights.DirectionalLights)
            {
                _directionalLight.Parameters["L"]?.SetValue(Vector3.Normalize(light.Direction));
                _directionalLight.Parameters["LightColor"]?.SetValue(light.Color);
                _directionalLight.Parameters["LightIntensity"]?.SetValue(light.Intensity);

                _directionalLight.CurrentTechnique.Passes[0].Apply();

                _fullScreenQuad.JustDraw(gameTime);
            }

            GraphicsDevice.SetRenderTarget(null);
        }

        void MakeFinal(GameTime gameTime, RenderTarget2D output)
        {
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(Color.Transparent);

            _compose.Parameters["Color"]?.SetValue(Colors);
            _compose.Parameters["Albedo"]?.SetValue(Albedo);
            _compose.Parameters["LightMap"]?.SetValue(_lightMap);
            _compose.Parameters["BufferTextureSize"]?.SetValue(_bufferTextureSize); 

            _compose.CurrentTechnique.Passes[0].Apply();

            _fullScreenQuad.Draw(gameTime);
        }

        public int Debug(SpriteBatch spriteBatch, int x, int size)
        {
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);

            var rect = new Rectangle(x, 0, size, size);
            spriteBatch.Draw(Colors, rect, Color.White);
            rect.X += size;
            spriteBatch.Draw(Albedo, rect, Color.White);
            rect.X += size;
            spriteBatch.Draw(Normals, rect, Color.White);
            rect.X += size;
            spriteBatch.Draw(Position, rect, Color.White);
            rect.X += size;
            spriteBatch.Draw(_lightMap, rect, Color.White);

            spriteBatch.End();
            return rect.X + size;
        }
    }
}