using System;
using System.Collections.Generic;
using System.IO;
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
using VoxelWorldEngine.Util.Performance;
using VoxelWorldEngine.Util.Scheduler;

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

        public Vector2I ClientSize => new Vector2I(_clientWidth, _clientHeight);
        public Vector2I MouseDelta { get; set; }

        public bool Paused { get; private set; }


        public VoxelGame()
        {
            Instance = this;

            _clientWidth = 1600;
            _clientHeight = 960;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1600,
                PreferredBackBufferHeight = 960,
                PreferMultiSampling = false,
                SynchronizeWithVerticalRetrace = false,
                GraphicsProfile = GraphicsProfile.HiDef
            };
            _graphics.ApplyChanges();

            Window.AllowUserResizing = true;

            Content.RootDirectory = "Content";
        }

        public class ResolutionEventArgs : EventArgs
        {
            public int Width { get; }
            public int Height { get; }

            public ResolutionEventArgs(int width, int height)
            {
                Width = width;
                Height = height;
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

                var centerX = _clientWidth / 2;
                var centerY = _clientHeight / 2;
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

        private int _clientWidth;
        private int _clientHeight;

        protected override void Update(GameTime gameTime)
        {
            if (Window == null)
                return;

            if (Window.ClientBounds.Width != _clientWidth || Window.ClientBounds.Height != _clientHeight)
            {
                _clientWidth = Window.ClientBounds.Width;
                _clientHeight = Window.ClientBounds.Height;
                _graphics.PreferredBackBufferWidth = _clientWidth;
                _graphics.PreferredBackBufferHeight = _clientHeight;
                _graphics.ApplyChanges();

                var p = GraphicsDevice.PresentationParameters;
                OnResolutionChanged(new ResolutionEventArgs(p.BackBufferWidth, p.BackBufferHeight));

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
                    var centerX = _clientWidth / 2;
                    var centerY = _clientHeight / 2;
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

                var centerX = _clientWidth / 2;
                var centerY = _clientHeight / 2;
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
                Window.Title = $"FPS: {fps}; Tiles In Progress: {Grid.InProgressTiles}; Pending tiles: {Grid.PendingTiles};" + 
                               $" Queued tasks: {PriorityScheduler.Instance.QueuedTaskCount};" +
                               $" Scheduled tasks: {Grid.UpdateTaskCount};" +
                               $" Player At: {_playerController.PlayerPosition.GridPosition}; Angles: {_playerController.PlayerOrientation};"+
                               $" Target: {_playerController.PlayerPositionTarget}" +
                               $" Mouse: {MouseDelta}" +
                               $" Generation Phase: {Grid.saveInitializationPhase}";
                lastFpsUpdate = now;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            using (Profiler.CurrentProfiler.Begin("Rendering"))
            {
                var now = DateTime.Now;
                frameTimes.Enqueue(now);
                base.Draw(gameTime);
                StatManager.PerFrame.Reset();
            }
        }

    }
}
