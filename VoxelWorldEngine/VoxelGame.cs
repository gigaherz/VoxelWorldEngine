using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Terrain;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine
{
    public class VoxelGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;

        public static readonly string DefaultDomain = "internal";

        public static VoxelGame Instance { get; private set; }
        public static Thread GameThread { get; } = Thread.CurrentThread;

        public Grid Grid { get; private set; }

        private TimeSpan _lastFpsTime = TimeSpan.Zero;
        private int _frames;
        private bool _pauseWasPressed;

        private RenderManager _renderManager;
        private PlayerController _playerController;

        public KeyboardState LastKeyboardState { get; private set; }

        public Vector2I MouseDelta { get; set; }

        public bool Paused { get; private set; }

        public VoxelGame()
        {
            Instance = this;

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1920,
                PreferredBackBufferHeight = 1080,
                PreferMultiSampling = false,
                SynchronizeWithVerticalRetrace = false,
                GraphicsProfile = GraphicsProfile.HiDef
            };
            _graphics.ApplyChanges();


            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            if (Window != null && MouseExtras.Instance.IsForeground(this, Window))
            {
                Window.AllowUserResizing = true;

                if (!MouseExtras.Instance.HasCapture(this, Window))
                    MouseExtras.Instance.SetCapture(Window);

                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                MouseExtras.Instance.SetPosition(Window, centerX, centerY);
            }
            
            Components.Add(Grid = new Grid(this));
            Components.Add(_renderManager = new RenderManager(this));
            Components.Add(_playerController = new PlayerController(this));

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Block.Initialize();
            PhysicsMaterial.Initialize();
            RenderingMaterial.Initialize();
            RenderQueue.Initialize();
        }

        protected override void UnloadContent()
        {
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            base.OnExiting(sender, args);
            MouseExtras.Instance.ReleaseCapture();
            PriorityScheduler.Instance.Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            if (Window == null)
                return;

            ShowFps(gameTime);

            LastKeyboardState = Keyboard.GetState();


            if (!MouseExtras.Instance.IsForeground(this, Window))
            {
                IsMouseVisible = true;
                Paused = true;
                if (MouseExtras.Instance.HasCapture(this, Window))
                    MouseExtras.Instance.ReleaseCapture();

                MouseDelta = new Vector2I();
            }
            else
            {
                bool pauseIsPressed = LastKeyboardState.IsKeyDown(Keys.Tab);
                if (!_pauseWasPressed && pauseIsPressed)
                {
                    Paused = !Paused;
                    if (Paused)
                    {
                        if (MouseExtras.Instance.HasCapture(this, Window))
                            MouseExtras.Instance.ReleaseCapture();
                    }
                    IsMouseVisible = Paused;
                }
                _pauseWasPressed = pauseIsPressed;

                var mouseX = 0;
                var mouseY = 0;
                if (!Paused && MouseExtras.Instance.HasCapture(this, Window))
                {
                    var centerX = Window.ClientBounds.Width / 2;
                    var centerY = Window.ClientBounds.Height / 2;
                    var mouse = MouseExtras.Instance.GetPosition(Window);
                    mouseX = mouse.X - centerX;
                    mouseY = mouse.Y - centerY;
                }
                MouseDelta = new Vector2I(mouseX, mouseY);
            }


            if (LastKeyboardState.IsKeyDown(Keys.Escape))
            {
                Exit();
                return;
            }

            base.Update(gameTime);

            if (!Paused && MouseExtras.Instance.IsForeground(this, Window))
            {
                if (!MouseExtras.Instance.HasCapture(this, Window))
                    MouseExtras.Instance.SetCapture(Window);

                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                MouseExtras.Instance.SetPosition(Window, centerX, centerY);
            }
        }

        private double fpsAcc = 0;
        private void ShowFps(GameTime gameTime)
        {
            var now = gameTime.TotalGameTime;
            var elapsed = (now - _lastFpsTime).TotalSeconds;
            if (elapsed >= 0.1)
            {
                fpsAcc = fpsAcc * 0.9 + (_frames / elapsed) * (1-0.9);
                Window.Title = $"FPS: {fpsAcc}; Tiles in progress: {Grid.TilesInProgress};" +
                               $" Pending tiles: {Grid.PendingTiles}; Queued tasks: {PriorityScheduler.Instance.QueuedTaskCount};" +
                               $" Player At: {_playerController.PlayerPosition}; Angles: {_playerController.PlayerOrientation};"+
                               $" Target: {_playerController.PlayerPositionTarget}" +
                               $" Mouse: {MouseDelta}";
                _frames = 0;
                _lastFpsTime = now;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            _frames++;
            //_renderManager.Draw(gameTime);
            base.Draw(gameTime);
        }

    }
}
