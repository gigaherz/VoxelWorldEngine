using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine
{
    class PlayerController : GameComponent
    {
        public static PlayerController Instance { get; set; }

        private Vector3 _playerSpeed;
        private bool _walkingWasPressed;
        private bool _wasSpacePressed;

        public bool Walking { get; set; } = true;

        public EntityPosition PlayerPosition { get; private set; }
        public EntityPosition PlayerPositionTarget { get; private set; }
        public EntityOrientation PlayerOrientation { get; private set; }

        public PlayerController(Game game) : base(game)
        {
            Instance = this;
        }

        public override void Initialize()
        {
            base.Initialize();

            PlayerPosition = VoxelGame.Instance.Grid.SpawnPosition;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            Vector3 move = Vector3.Zero;
            bool walkingIsPressed = false;
            bool isSpacePressed = false;

            if (!VoxelGame.Instance.Paused)
            {
                var keyboardState = Keyboard.GetState();

                walkingIsPressed = Mouse.GetState().MiddleButton == ButtonState.Pressed;
                if (walkingIsPressed && !_walkingWasPressed)
                {
                    Walking = !Walking;
                }

                isSpacePressed = keyboardState.IsKeyDown(Keys.Space);

                if (keyboardState.IsKeyDown(Keys.A)) move.X -= 1;
                if (keyboardState.IsKeyDown(Keys.D)) move.X += 1;
                if (keyboardState.IsKeyDown(Keys.S)) move.Z -= 1;
                if (keyboardState.IsKeyDown(Keys.W)) move.Z += 1;

                var mouseDelta = VoxelGame.Instance.MouseDelta;

                PlayerOrientation = PlayerOrientation.RotateYaw(mouseDelta.X * 0.0025f);
                PlayerOrientation = PlayerOrientation.RotatePitch(mouseDelta.Y * 0.0025f);
            }

            RenderManager.Instance.CameraForward = PlayerOrientation.Forward;

            if (Walking)
            {
                if (VoxelGame.Instance.Grid.saveInitializationPhase > 0 && VoxelGame.Instance.Grid.ChunkExists(PlayerPosition))
                {
                    if (move.LengthSquared() > 0)
                    {
                        move.Normalize();

                        PlayerPosition += (
                            PlayerOrientation.HorizontalForward * move.Z +
                            PlayerOrientation.HorizontalRight * move.X) * elapsed * 20;
                    }

                    // Gravity
                    _playerSpeed.Y -= 9.8f * elapsed;
                    PlayerPosition += new Vector3(0, _playerSpeed.Y * elapsed, 0);

                    var p0 = (PlayerPosition - new Vector3(0.4f, 0.4F, 0.4F)).GridPosition;
                    var q0 = (PlayerPosition + new Vector3(0.4f, 0.4F, 0.4F)).GridPosition;

                    var xyz = new Vector3I(p0.X, int.MinValue, p0.Z);

                    for (int rz = p0.Z; rz <= q0.Z; rz++)
                    {
                        for (int rx = p0.X; rx <= q0.X; rx++)
                        {
                            int ry = VoxelGame.Instance.Grid.FindGround(new Vector3I(rx, q0.Y, rz));
                            if (ry > xyz.Y)
                            {
                                xyz = new Vector3I(rx, ry, rz);
                            }
                        }
                    }

                    var block = VoxelGame.Instance.Grid.GetBlock(xyz);

                    float blockHeight = block.PhysicsMaterial.IsSolid ? block.PhysicsMaterial.Height : 0;

                    PlayerPositionTarget = EntityPosition.FromGrid(xyz, new Vector3(0, blockHeight, 0));

                    var approach = elapsed / 0.25f;

                    var distance = PlayerPosition.RelativeTo(PlayerPositionTarget).Y;

                    if (distance < 0)
                    {
                        _playerSpeed.Y = 0;
                        if (isSpacePressed && !_wasSpacePressed)
                        {
                            PlayerPosition += new Vector3(0, -distance, 0);
                            _playerSpeed.Y = 8;
                        }
                    }

                    if (distance < -1)
                        PlayerPosition += new Vector3(0, -distance, 0);
                    else if (distance > approach)
                        PlayerPosition += new Vector3(0, -approach, 0);
                    else if (distance < -approach)
                        PlayerPosition += new Vector3(0, approach, 0);
                    else
                        PlayerPosition += new Vector3(0, -distance, 0);
                }
            }
            else
            {
                if (move.LengthSquared() > 0)
                {
                    move.Normalize();

                    PlayerPosition += (
                        PlayerOrientation.Forward * move.Z +
                        PlayerOrientation.HorizontalRight * move.X) * elapsed * 25;
                }

                _playerSpeed = Vector3.Zero;
            }

            VoxelGame.Instance.Grid.SetPlayerPosition(PlayerPosition);
            PriorityScheduler.Instance.SetPlayerPosition(PlayerPosition);

            _walkingWasPressed = walkingIsPressed;
            _wasSpacePressed = isSpacePressed;

        }
    }
}
