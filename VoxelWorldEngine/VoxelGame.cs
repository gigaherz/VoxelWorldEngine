using System;
using System.Linq;
using System.Diagnostics;
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

        private readonly GraphicsDeviceManager _graphics;

        private SpriteBatch _spriteBatch;

        private Grid grid;

        public Effect terrainDrawEffect;
        public Texture terrainTexture;
        public SpriteFont font;

        private float angleYaw = 0;
        private float anglePitch = 0;

        private Vector3D playerPosition;
        private Vector3D playerSpeed;

        public bool Walking = true;

        public static Thread GameThread { get; } = Thread.CurrentThread;

        public VoxelGame()
        {
            Block.Touch();
            PhysicsMaterial.Touch();
            RenderingMaterial.Touch();
            RenderQueue.Touch();

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1920,
                PreferredBackBufferHeight = 1080,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = false
            };
            _graphics.ApplyChanges();
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

            playerPosition = grid.SpawnPosition * new Vector3D(Tile.VoxelSizeX, Tile.VoxelSizeY, Tile.VoxelSizeZ);
        }

        protected override void LoadContent()
        {
            float ar = GraphicsDevice.Viewport.Width / (float)GraphicsDevice.Viewport.Height;

            terrainDrawEffect = Content.Load<Effect>("BasicTextured");
            terrainDrawEffect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView((float)Math.PI * 0.4f, -ar, 0.1f, 10000));

            terrainTexture = Content.Load<Texture>("Tiles");

            font = Content.Load<SpriteFont>("Font");
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
                angleYaw += mouseX * 0.0025f;
            anglePitch += mouseY * 0.0025f;

            if (anglePitch > 1.57f) anglePitch = 1.57f;
            if (anglePitch < -1.57f) anglePitch = -1.57f;

            var cYaw = Math.Cos(angleYaw);
            var sYaw = Math.Sin(angleYaw);
            var cPitch = Math.Cos(anglePitch);
            var sPitch = Math.Sin(anglePitch);

            var cameraForward = new Vector3D(sYaw * cPitch, -sPitch, cYaw * cPitch);

            Vector3 move = Vector3.Zero;

            if (keyboardState.IsKeyDown(Keys.A)) move.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D)) move.X += 1;
            if (keyboardState.IsKeyDown(Keys.S)) move.Z -= 1;
            if (keyboardState.IsKeyDown(Keys.W)) move.Z += 1;

            if (Walking)
            {
                if (grid.ChunkExists(playerPosition))
                {
                    if (move.LengthSquared() > 0)
                    {
                        move.Normalize();

                        playerPosition += new Vector3D(
                            move.Z * sYaw + move.X * cYaw, 0,
                            move.Z * cYaw - move.X * sYaw) * elapsed * 4;
                    }

                    // Gravity
                    playerSpeed.Y -= 9.8f * elapsed;
                    playerPosition.Y += playerSpeed.Y * elapsed;

                    int px = (int)Math.Floor(playerPosition.X / Tile.VoxelSizeX - 0.4f);
                    int py = (int)Math.Floor(playerPosition.Y / Tile.VoxelSizeY + 0.4f);
                    int pz = (int)Math.Floor(playerPosition.Z / Tile.VoxelSizeZ - 0.4f);
                    int qx = (int)Math.Floor(playerPosition.X / Tile.VoxelSizeX + 0.4f);
                    int qz = (int)Math.Floor(playerPosition.Z / Tile.VoxelSizeZ + 0.4f);

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

                    var distance = playerPosition.Y - targetY;

                    if (distance < 0)
                    {
                        playerSpeed.Y = 0;
                        if (keyboardState.IsKeyDown(Keys.Space))
                        {
                            playerPosition.Y = targetY;
                            playerSpeed.Y = 8;
                        }
                    }

                    if (distance < -1)
                        playerPosition.Y = targetY;
                    else if (distance > approach)
                        playerPosition.Y -= approach;
                    else if (distance < -approach)
                        playerPosition.Y += approach;
                    else
                        playerPosition.Y = targetY;
                }
            }
            else
            {
                if (move.LengthSquared() > 0)
                {
                    move.Normalize();

                    playerPosition += new Vector3D(
                        move.X * cYaw, 0,
                        -move.X * sYaw) * elapsed * 10;
                }

                playerPosition += cameraForward * move.Z * 10 * elapsed;
                playerSpeed = Vector3D.Zero;
            }

            grid.SetPlayerPosition(playerPosition);
            PriorityScheduler.Instance.SetPlayerPosition(playerPosition);

            terrainDrawEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(
                (Vector3)playerPosition,
                (Vector3)(playerPosition + cameraForward), Vector3.UnitY));
            terrainDrawEffect.Parameters["World"].SetValue(Matrix.Identity);

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

        TimeSpan _lastFpsTime = TimeSpan.Zero;
        private void ShowFps(GameTime gameTime)
        {
            var now = gameTime.TotalGameTime;
            var elapsed = (now - _lastFpsTime).TotalSeconds;
            if (elapsed >= 1)
            {
                Window.Title = $"FPS: {_frames / elapsed}; Tiles in progress: {grid.TilesInProgress}; Pending tiles: {grid.PendingTiles}; Queued tasks: {PriorityScheduler.Instance.QueuedTaskCount}; Player At: {playerPosition}";
                _frames = 0;
                _lastFpsTime = now;
            }
        }

        int _frames;
        protected override void Draw(GameTime gameTime)
        {
            _frames++;

            GraphicsDevice.Clear(Color.CornflowerBlue);
            
            base.Draw(gameTime);

            _spriteBatch.Begin();
            if (pause)
            {
                _spriteBatch.DrawString(font, "Paused", new Vector2(Window.ClientBounds.Width / 2.0f, Window.ClientBounds.Height / 2.0f), Color.White);

            }
            _spriteBatch.End();

        }
    }
}
