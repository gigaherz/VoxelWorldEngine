using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine
{
    public class VoxelGame : Game
    {
        public static readonly string DefaultDomain = "VoxelWorldEngine";

        GraphicsDeviceManager graphics;

        VoxelGrid grid;

        Effect terrainDrawEffect;
        Texture terrainTexture;

        float angleYaw = 0;
        float anglePitch = 0;

        Vector3 playerPosition;
        Vector3 playerSpeed;

        public VoxelGame()
        {
            graphics = new GraphicsDeviceManager(this)
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
            grid = new VoxelGrid(this);

            Components.Add(grid);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            float ar = GraphicsDevice.Viewport.Width / (float)GraphicsDevice.Viewport.Height;

            terrainDrawEffect = Content.Load<Effect>("BasicTextured");
            terrainDrawEffect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView(1.1f, -ar, 0.1f, 10000));

            terrainTexture = Content.Load<Texture>("Tiles");
        }

        protected override void UnloadContent()
        {
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            base.OnExiting(sender, args);
            MouseExtras.ReleaseCapture();
        }

        float targetY = 0;
        protected override void Update(GameTime gameTime)
        {
            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.Escape))
                Exit();

            ShowFps();

            var mouseX = 0;
            var mouseY = 0;

            if (Window != null && MouseExtras.IsForeground(Window))
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

            var cYaw = (float)Math.Cos(angleYaw);
            var sYaw = (float)Math.Sin(angleYaw);
            var cPitch = (float)Math.Cos(anglePitch);
            var sPitch = (float)Math.Sin(anglePitch);

            var cameraForward = new Vector3(sYaw * cPitch, -sPitch, cYaw * cPitch);

            Vector3 move = Vector3.Zero;

            if (keyboardState.IsKeyDown(Keys.A)) move.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D)) move.X += 1;
            if (keyboardState.IsKeyDown(Keys.S)) move.Z -= 1;
            if (keyboardState.IsKeyDown(Keys.W)) move.Z += 1;

#if true
            if (move.LengthSquared() > 0)
            {
                move.Normalize();

                playerPosition += new Vector3(
                    move.Z * sYaw + move.X * cYaw, 0,
                    move.Z * cYaw - move.X * sYaw) * elapsed * 4;
            }

            // Gravity
            playerSpeed.Y -= 9.8f * elapsed;
            playerPosition.Y += playerSpeed.Y * elapsed;

            int px = (int)Math.Floor(playerPosition.X / VoxelTile.VoxelSizeX - 0.4f);
            int py = (int)Math.Floor(playerPosition.Y / VoxelTile.VoxelSizeY + 0.4f);
            int pz = (int)Math.Floor(playerPosition.Z / VoxelTile.VoxelSizeZ - 0.4f);
            int qx = (int)Math.Floor(playerPosition.X / VoxelTile.VoxelSizeX + 0.4f);
            int qz = (int)Math.Floor(playerPosition.Z / VoxelTile.VoxelSizeZ + 0.4f);

            int yy = 0, xx = px, zz = pz;
            int ty;

            for (int rz = pz; rz <= qz; rz++)
            {
                for (int rx = px; rx <= qx; rx++)
                {
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

            targetY = 1.5f + blockHeight + VoxelTile.VoxelSizeY * yy;

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
#else
            playerPosition += cameraForward * move.Z * 10 * elapsed;
#endif

            terrainDrawEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(playerPosition, playerPosition + cameraForward, Vector3.UnitY));
            terrainDrawEffect.Parameters["World"].SetValue(Matrix.Identity);

            if (Window != null && MouseExtras.IsForeground(Window))
            {
                if (!MouseExtras.HasCapture(Window))
                    MouseExtras.SetCapture(Window);

                var centerX = Window.ClientBounds.Width / 2;
                var centerY = Window.ClientBounds.Height / 2;
                MouseExtras.SetPosition(Window, centerX, centerY);
            }

            base.Update(gameTime);
        }

        DateTime lastFpsTime = DateTime.Now;
        private void ShowFps()
        {
            var now = DateTime.Now;
            var elapsed = (now - lastFpsTime).TotalSeconds;
            if (elapsed >= 1)
            {
                Debug.WriteLine("FPS: {0}", frames / elapsed);
                frames = 0;
                lastFpsTime = now;
            }
        }

        int frames = 0;
        protected override void Draw(GameTime gameTime)
        {
            frames++;

            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            terrainDrawEffect.Techniques[0].Passes[0].Apply();
            GraphicsDevice.Textures[0] = terrainTexture;

            base.Draw(gameTime);
        }
    }
}
