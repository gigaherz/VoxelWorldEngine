using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Util;
using DirectionalLight = VoxelWorldEngine.Rendering.DirectionalLight;

namespace VoxelWorldEngine
{
    class RenderManager : DrawableGameComponent
    {
        public static RenderManager Instance { get; set; }

        private SSAO _ssao;
        private RenderTarget2D _outputRenderTarget;
        private BaseCamera _baseCamera;
        private LightManager _lightManager;
        private DeferredRenderer _deferredRenderer;

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

            var parameters = GraphicsDevice.PresentationParameters;
            _deferredRenderer = new DeferredRenderer(Game, Game.Content, parameters.BackBufferWidth, parameters.BackBufferHeight);
            _lightManager = new LightManager(Game.Content);
            _lightManager.AddLight(new DirectionalLight(new Vector3(0.2f, -0.5f, 0.2f), Color.White, 1.0f));
            _lightManager.AddLight(new DirectionalLight(new Vector3(-0.2f, -0.2f, 0.2f), new Color(0.8f, 1, 0.9f), 0.3f));
            _lightManager.AddLight(new DirectionalLight(new Vector3(0.2f, -0.2f, -0.2f), new Color(0.9f, 1, 0.8f), 0.3f));
            _ssao = new SSAO(Game, Game.Content, parameters.BackBufferWidth, parameters.BackBufferHeight);
            _outputRenderTarget = new RenderTarget2D(GraphicsDevice, parameters.BackBufferWidth, parameters.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

            CurrentEffect = TerrainDrawEffect;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _ssao.Modify(VoxelGame.Instance.LastKeyboardState);
        }

        public override void Draw(GameTime gameTime)
        {
            _baseCamera.Forward = CameraForward;

            //Draw with SSAO unless F1 is down 
            if (VoxelGame.Instance.LastKeyboardState.IsKeyDown(Keys.F1))
            {
                _deferredRenderer.Draw(gameTime, GetDrawables(), _lightManager, _baseCamera, null);
            }
            else
            {
                _deferredRenderer.Draw(gameTime, GetDrawables(), _lightManager, _baseCamera, _outputRenderTarget);

                _ssao.Draw(gameTime, _deferredRenderer, _outputRenderTarget, _baseCamera, !VoxelGame.Instance.LastKeyboardState.IsKeyDown(Keys.F2), null);
            }

            Debug();

            SpriteBatch.Begin();
            if (VoxelGame.Instance.Paused)
            {
                var bounds = Game.Window.ClientBounds;
                SpriteBatch.DrawString(Font, "Paused",
                    new Vector2(bounds.Width / 2.0f, bounds.Height / 2.0f), Color.White);
            }
            SpriteBatch.End();
        }

        private void Debug()
        {
            int size = 128, x = 0;

            x = _deferredRenderer.Debug(SpriteBatch, x, size);
            _ssao.Debug(SpriteBatch, Font, x, size);
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
