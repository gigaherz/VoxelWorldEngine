using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Rendering;
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
#if false
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720,
#else
                PreferredBackBufferWidth = 1600,
                PreferredBackBufferHeight = 960,
#endif
                PreferMultiSampling = false,
                SynchronizeWithVerticalRetrace = false,
                GraphicsProfile = GraphicsProfile.HiDef
            };
            _graphics.ApplyChanges();

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += Window_ClientSizeChanged;

            Content.RootDirectory = "Content";
        }

        private bool _resizePending = false;
        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            _resizePending = true;
        }

        public class ResolutionEventArgs : EventArgs
        {
            public int BackBufferWidth { get; }
            public int BackBufferHeight { get; }

            public ResolutionEventArgs(int backBufferWidth, int backBufferHeight)
            {
                BackBufferWidth = backBufferWidth;
                BackBufferHeight = backBufferHeight;
            }
        }

        public event EventHandler<ResolutionEventArgs> ResolutionChanged;
        private void OnResolutionChanged(ResolutionEventArgs args)
        {
            ResolutionChanged?.Invoke(this, args);
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

            if(_resizePending)
            {
                _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
                _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
                _graphics.ApplyChanges();

                var p = GraphicsDevice.PresentationParameters;
                OnResolutionChanged(new ResolutionEventArgs(p.BackBufferWidth, p.BackBufferHeight));
                _resizePending = false;
            }

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

        private readonly Queue<DateTime> frameTimes = new Queue<DateTime>();
        private DateTime lastFpsUpdate = DateTime.Now;
        private void ShowFps(GameTime gameTime)
        {
            var now = DateTime.Now;
            while (frameTimes.Count > 2 && (now - frameTimes.Peek()).TotalSeconds > 1)
                frameTimes.Dequeue();

            if (frameTimes.Count >= 2 && (now-lastFpsUpdate).TotalSeconds > 0.25)
            {
                var fps = (frameTimes.Count - 1) / (frameTimes.Last() - frameTimes.Peek()).TotalSeconds;
                Window.Title = $"FPS: {fps}; Tiles in progress: {Grid.TilesInProgress};" +
                               $" Pending tiles: {Grid.PendingTiles}; Queued tasks: {PriorityScheduler.Instance.QueuedTaskCount};" +
                               $" Player At: {_playerController.PlayerPosition}; Angles: {_playerController.PlayerOrientation};"+
                               $" Target: {_playerController.PlayerPositionTarget}" +
                               $" Mouse: {MouseDelta}" +
                               $" Generation Phase: {Grid.saveInitializationPhase}";
                lastFpsUpdate = now;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            var now = DateTime.Now;
            frameTimes.Enqueue(now);
            base.Draw(gameTime);
            StatManager.PerFrame.Reset();
        }

    }
}
