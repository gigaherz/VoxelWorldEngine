using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Terrain.Graphics;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Rendering
{
    class RenderManager : DrawableGameComponent
    {
        public static RenderManager Instance { get; set; }

        private SSAO _ssao;
        private RenderTarget2D _outputRenderTarget;
        private BaseCamera _baseCamera;
        private LightManager _lightManager;
        private DeferredRenderer _deferredRenderer;
        private GridGraphics _gridGraphics;

        public Effect TerrainDrawEffect { get; private set; }
        public Texture TerrainTexture { get; private set; }
        public SpriteFont Font { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Effect CurrentEffect { get; set; }
        public Vector3 CameraForward { get; set; }

        public RenderManager(Game game) : base(game)
        {
            Instance = this;
        }

        public override void Initialize()
        {
            base.Initialize();

            SpriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            var ar = GraphicsDevice.Viewport.Width / (float)GraphicsDevice.Viewport.Height;

            _baseCamera = new BaseCamera(GraphicsDevice)
            {
                FieldOfView = (float)Math.PI * 0.4f,
                AspectRatio = ar,
                NearClip = 0.1f,
                FarClip = 10000
            };

            TerrainDrawEffect = Game.Content.Load<Effect>("BasicTextured");
            TerrainDrawEffect.CurrentTechnique = TerrainDrawEffect.Techniques[0];

            TerrainTexture = Game.Content.Load<Texture>("Tiles");

            Font = Game.Content.Load<SpriteFont>("Font");

            var parameters = VoxelGame.Instance.ClientSize;
            _deferredRenderer = new DeferredRenderer(Game, Game.Content, parameters.X, parameters.Y);
            _lightManager = new LightManager(Game.Content);
            _lightManager.AddLight(new DirectionalLight(new Vector3(1f, -3f, 1f), Color.White, 0.9f));
            _lightManager.AddLight(new DirectionalLight(new Vector3(-2f, -1f, -1f), new Color(1.0f, 1.0f, 0.5f), 0.5f));
            _lightManager.AddLight(new DirectionalLight(new Vector3(2f, -1f, -1f), new Color(0.5f, 1.0f, 1.0f), 0.3f));
            _ssao = new SSAO(Game, Game.Content, parameters.X, parameters.Y);

            CurrentEffect = TerrainDrawEffect;

            CreateRenderTargets(parameters.X, parameters.Y);
            VoxelGame.Instance.ResolutionChanged += (sender, args) =>
            {
                CreateRenderTargets(args.Width, args.Height);
            };
        }

        private void CreateRenderTargets(int width, int height)
        {
            _outputRenderTarget?.Dispose();
            _outputRenderTarget = new RenderTarget2D(GraphicsDevice, width, height,
                false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _ssao.Modify(VoxelGame.Instance.LastKeyboardState);
        }

        bool isDebugEnabled = false;
        bool isDebugEnabledPressed = false;

        bool isSsaoEnabled = true;
        bool isSsaoEnabledPressed = false;
        public override void Draw(GameTime gameTime)
        {
            _baseCamera.Forward = CameraForward;
            _baseCamera.ViewFrustum = new BoundingFrustum(_baseCamera.View * _baseCamera.Projection);

            //Draw with SSAO unless F2 is down 
            var db = VoxelGame.Instance.LastKeyboardState.IsKeyDown(Keys.F1);
            if (!isDebugEnabledPressed && db)
                isDebugEnabled = !isDebugEnabled;
            isDebugEnabledPressed = db;

            var ss = VoxelGame.Instance.LastKeyboardState.IsKeyDown(Keys.F2);
            if (!isSsaoEnabledPressed && ss)
                isSsaoEnabled = !isSsaoEnabled;
            isSsaoEnabledPressed = ss;
            if (!isSsaoEnabled)
            {
                _deferredRenderer.Draw(gameTime, GetDrawables(), _lightManager, _baseCamera, null);
            }
            else
            {
                _deferredRenderer.Draw(gameTime, GetDrawables(), _lightManager, _baseCamera, _outputRenderTarget);

                _ssao.Draw(gameTime, _deferredRenderer, _outputRenderTarget, _baseCamera, !VoxelGame.Instance.LastKeyboardState.IsKeyDown(Keys.F2), null);
            }

            if (isDebugEnabled)
            {
                Debug();
            }

            SpriteBatch.Begin();
            if (VoxelGame.Instance.Paused)
            {
                var bounds = VoxelGame.Instance.ClientSize;
                SpriteBatch.DrawString(Font, "Paused",
                    new Vector2(bounds.X / 2.0f, bounds.Y / 2.0f), Color.White);
            }

            if (isDebugEnabled)
            {
                SpriteBatch.DrawString(Font, $"Draw Calls: {StatManager.PerFrame["DrawCalls"].Value}", new Vector2(5, 200), Color.White);
            }

            SpriteBatch.End();
        }

        private void Debug()
        {
            int size = 128, x = 0;

            x = _deferredRenderer.Debug(SpriteBatch, x, size);
            if (isSsaoEnabled) _ssao.Debug(SpriteBatch, Font, x, size);
        }

        private IEnumerable<IRenderable> GetDrawables()
        {
            return Game.Components
                .OfType<IRenderableProvider>()
                .SelectMany(component => component.GetRenderables())
                .Concat(Game.Components.OfType<IRenderable>());
        }
    }
}
