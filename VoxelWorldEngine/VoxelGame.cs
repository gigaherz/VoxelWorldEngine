using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Terrain;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine
{
    public class VoxelGame : Game
    {
        public static readonly string DefaultDomain = "VoxelWorldEngine";

        private SpriteBatch _spriteBatch;

        private Grid grid;

        public Effect TerrainDrawEffect;
        public Texture TerrainTexture;
        public SpriteFont Font;

        private float _angleYaw = 0;
        private float _anglePitch = 0;

        private Vector3D _playerPosition;
        private Vector3D _playerSpeed;
        private Vector3D _cameraForward;

        public bool Walking = true;

        public static Thread GameThread { get; } = Thread.CurrentThread;

        public Effect CurrentEffect => TerrainDrawEffect;

        TimeSpan _lastFpsTime = TimeSpan.Zero;

        int _frames;

        public VoxelGame()
        {
            Block.Touch();
            PhysicsMaterial.Touch();
            RenderingMaterial.Touch();
            RenderQueue.Touch();
            
            var graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1920,
                PreferredBackBufferHeight = 1080,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = false
            };
            graphics.ApplyChanges();

            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            if (Window != null && MouseExtras.IsForeground(Window))
            {
                Window.AllowUserResizing = true;

                if (!MouseExtras.HasCapture(Window))
                    MouseExtras.SetCapture(Window);

                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                MouseExtras.SetPosition(Window, centerX, centerY);
            }

            // TODO: Add your initialization logic here
            grid = new Grid(this);

            Components.Add(grid);

            base.Initialize();

            _playerPosition = grid.SpawnPosition * new Vector3D(Tile.VoxelSizeX, Tile.VoxelSizeY, Tile.VoxelSizeZ);
        }

        protected override void LoadContent()
        {
            float ar = GraphicsDevice.Viewport.Width / (float)GraphicsDevice.Viewport.Height;

            TerrainDrawEffect = Content.Load<Effect>("BasicTextured");
            TerrainDrawEffect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView((float)Math.PI * 0.4f, -ar, 0.1f, 10000));

            TerrainTexture = Content.Load<Texture>("Tiles");

            Font = Content.Load<SpriteFont>("Font");
        }

        protected override void UnloadContent()
        {
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            base.OnExiting(sender, args);
            MouseExtras.ReleaseCapture();
        }

        bool pauseWasPressed;
        bool pause;
        float targetY = 0;
        bool walkingWasPressed;
        protected override void Update(GameTime gameTime)
        {
            if (Window == null)
                return;

            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                Exit();
                return;
            }

            bool pauseIsPressed = keyboardState.IsKeyDown(Keys.Tab);
            if (!pauseWasPressed && pauseIsPressed)
            {
                pause = !pause;
                if (pause)
                {
                    if (MouseExtras.HasCapture(Window))
                        MouseExtras.ReleaseCapture();
                }
                IsMouseVisible = pause;
            }
            pauseWasPressed = pauseIsPressed;

            var walkingIsPressed = Mouse.GetState().MiddleButton == ButtonState.Released;
            if (walkingIsPressed && !walkingWasPressed)
            {
                Walking = !Walking;
            }
            walkingWasPressed = walkingIsPressed;

            if (!MouseExtras.IsForeground(Window))
            {
                IsMouseVisible = true;
                pause = true;
                if (MouseExtras.HasCapture(Window))
                    MouseExtras.ReleaseCapture();
            }

            ShowFps(gameTime);

            var mouseX = 0;
            var mouseY = 0;

            if (!pause && MouseExtras.IsForeground(Window) && MouseExtras.HasCapture(Window))
            {
                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                var mouse = MouseExtras.GetPosition(Window);
                mouseX = mouse.X - centerX;
                mouseY = mouse.Y - centerY;
            }

            if (mouseX != 0)
                _angleYaw += mouseX * 0.0025f;
            _anglePitch += mouseY * 0.0025f;

            if (_anglePitch > 1.57f) _anglePitch = 1.57f;
            if (_anglePitch < -1.57f) _anglePitch = -1.57f;

            var cYaw = Math.Cos(_angleYaw);
            var sYaw = Math.Sin(_angleYaw);
            var cPitch = Math.Cos(_anglePitch);
            var sPitch = Math.Sin(_anglePitch);

            _cameraForward = new Vector3D(sYaw * cPitch, -sPitch, cYaw * cPitch);

            Vector3 move = Vector3.Zero;

            if (keyboardState.IsKeyDown(Keys.A)) move.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D)) move.X += 1;
            if (keyboardState.IsKeyDown(Keys.S)) move.Z -= 1;
            if (keyboardState.IsKeyDown(Keys.W)) move.Z += 1;

            if (Walking)
            {
                if (grid.ChunkExists(_playerPosition))
                {
                    if (move.LengthSquared() > 0)
                    {
                        move.Normalize();

                        _playerPosition += new Vector3D(
                            move.Z * sYaw + move.X * cYaw, 0,
                            move.Z * cYaw - move.X * sYaw) * elapsed * 4;
                    }

                    // Gravity
                    _playerSpeed.Y -= 9.8f * elapsed;
                    _playerPosition.Y += _playerSpeed.Y * elapsed;

                    int px = (int)Math.Floor(_playerPosition.X / Tile.VoxelSizeX - 0.4f);
                    int py = (int)Math.Floor(_playerPosition.Y / Tile.VoxelSizeY + 0.4f);
                    int pz = (int)Math.Floor(_playerPosition.Z / Tile.VoxelSizeZ - 0.4f);
                    int qx = (int)Math.Floor(_playerPosition.X / Tile.VoxelSizeX + 0.4f);
                    int qz = (int)Math.Floor(_playerPosition.Z / Tile.VoxelSizeZ + 0.4f);

                    int yy = grid.GenerationContext.Floor - 1, xx = px, zz = pz;

                    for (int rz = pz; rz <= qz; rz++)
                    {
                        for (int rx = px; rx <= qx; rx++)
                        {
                            int ty;
                            if ((ty = grid.FindGround(rx, py, rz)) > yy)
                            {
                                yy = ty;
                                xx = rx;
                                zz = rz;
                            }
                        }
                    }

                    Block block = grid.GetBlock(xx, yy, zz);

                    float blockHeight = block.PhysicsMaterial.IsSolid ? block.PhysicsMaterial.Height : 0;

                    targetY = 1.5f + Tile.VoxelSizeY * (blockHeight + yy);

                    var approach = elapsed / 0.25f;

                    var distance = _playerPosition.Y - targetY;

                    if (distance < 0)
                    {
                        _playerSpeed.Y = 0;
                        if (keyboardState.IsKeyDown(Keys.Space))
                        {
                            _playerPosition.Y = targetY;
                            _playerSpeed.Y = 8;
                        }
                    }

                    if (distance < -1)
                        _playerPosition.Y = targetY;
                    else if (distance > approach)
                        _playerPosition.Y -= approach;
                    else if (distance < -approach)
                        _playerPosition.Y += approach;
                    else
                        _playerPosition.Y = targetY;
                }
            }
            else
            {
                if (move.LengthSquared() > 0)
                {
                    move.Normalize();

                    _playerPosition += new Vector3D(
                        move.X * cYaw, 0,
                        -move.X * sYaw) * elapsed * 10;
                }

                _playerPosition += _cameraForward * move.Z * 10 * elapsed;
                _playerSpeed = Vector3D.Zero;
            }

            grid.SetPlayerPosition(_playerPosition);
            PriorityScheduler.Instance.SetPlayerPosition(_playerPosition);

            if (!pause && MouseExtras.IsForeground(Window))
            {
                if (!MouseExtras.HasCapture(Window))
                    MouseExtras.SetCapture(Window);

                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                MouseExtras.SetPosition(Window, centerX, centerY);
            }

            base.Update(gameTime);
        }

        private void ShowFps(GameTime gameTime)
        {
            var now = gameTime.TotalGameTime;
            var elapsed = (now - _lastFpsTime).TotalSeconds;
            if (elapsed >= 1)
            {
                Window.Title = $"FPS: {_frames / elapsed}; Tiles in progress: {grid.TilesInProgress}; Pending tiles: {grid.PendingTiles}; Queued tasks: {PriorityScheduler.Instance.QueuedTaskCount}; Player At: {_playerPosition}";
                _frames = 0;
                _lastFpsTime = now;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            _frames++;

            GraphicsDevice.Clear(Color.CornflowerBlue);

            TerrainDrawEffect.Parameters["World"].SetValue(Matrix.Identity);
            TerrainDrawEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(
                (Vector3)_playerPosition,
                (Vector3)(_playerPosition + _cameraForward), Vector3.UnitY));

            base.Draw(gameTime);

            _spriteBatch.Begin();
            if (pause)
            {
                _spriteBatch.DrawString(Font, "Paused", new Vector2(Window.ClientBounds.Width / 2.0f, Window.ClientBounds.Height / 2.0f), Color.White);

            }
            _spriteBatch.End();

        }
    }
}
