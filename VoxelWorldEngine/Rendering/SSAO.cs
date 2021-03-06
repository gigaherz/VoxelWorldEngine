﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace VoxelWorldEngine.Rendering
{
    internal class SSAO : DrawableGameComponent
    {
        private readonly Effect _ssao;
        private readonly Effect _ssaoBlur;
        private readonly Effect _composer;

        private readonly Texture2D _randomNormals;

        private RenderTarget2D _ssaoTarget;
        private RenderTarget2D _blurTarget;

        public Vector3[] _sampleSphere;

        private readonly FullscreenQuad _fsq;

        private float SampleRadius { get; set; }
        private float Bias { get; set; }

        public SSAO(Game game, ContentManager content, int width, int height)
            : base(game)
        {
            _ssao = content.Load<Effect>("SSAO");
            _ssao.CurrentTechnique = _ssao.Techniques[0];

            _ssaoBlur = content.Load<Effect>("SSAOBlur");
            _ssaoBlur.CurrentTechnique = _ssaoBlur.Techniques[0];

            _composer = content.Load<Effect>("SSAOFinal");
            _composer.CurrentTechnique = _composer.Techniques[0];

            _fsq = new FullscreenQuad(game);

            _randomNormals = content.Load<Texture2D>("RandomNormals");

            SampleRadius = 0.5f; // 0.25f;
            Bias = 0.025f; //3.25f;

            Random rnd = new Random();
            List<Vector3> ssaoKernel = new List<Vector3>();
            for (int i = 0; i < 64; ++i)
            {
                var sample = new Vector3(
                    (float)(rnd.NextDouble() * 2.0 - 1.0), // -1 .. 1
                    (float)rnd.NextDouble(),                // 0 .. 1
                    (float)(rnd.NextDouble() * 2.0 - 1.0) // -1 .. 1
                );
                sample.Normalize();
                sample *= (float)rnd.NextDouble();
                ssaoKernel.Add(sample);
            }
            _sampleSphere = ssaoKernel.ToArray();

            CreateRenderTargets(width, height);
            VoxelGame.Instance.ResolutionChanged += (sender, args) =>
            {
                CreateRenderTargets(args.Width, args.Height);
            };
        }

        private void CreateRenderTargets(int width, int height)
        {
            _ssaoTarget?.Dispose();
            _blurTarget?.Dispose();
            _ssaoTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            _blurTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        }

        public void Draw(GameTime gameTime, DeferredRenderer deferred, RenderTarget2D scene, BaseCamera camera, bool blur, RenderTarget2D output)
        {
            RenderSsao(gameTime, deferred, camera);
            BlurSsao(gameTime, deferred);
            Compose(gameTime, scene, output, blur);
        }

        void RenderSsao(GameTime gameTime, DeferredRenderer deferred, BaseCamera camera)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetRenderTarget(_ssaoTarget);
            GraphicsDevice.Clear(Color.TransparentBlack);

            var ar = camera.AspectRatio;
            var farz = camera.FarClip;
            var farFov = (float)Math.Tan(camera.FieldOfView * 0.5) * farz;
            var cornerFrustum = new Vector3(-farFov * ar, farFov, farz);

            _ssao.Parameters["NormalBuffer"]?.SetValue(deferred.Normals);
            _ssao.Parameters["DepthBuffer"]?.SetValue(deferred.Depth);
            _ssao.Parameters["RandNormal"]?.SetValue(_randomNormals);
            _ssao.Parameters["Projection"]?.SetValue(camera.Projection);
            _ssao.Parameters["cornerFustrum"]?.SetValue(cornerFrustum);
            _ssao.Parameters["sampleRadius"]?.SetValue(SampleRadius);
            _ssao.Parameters["sampleSphere"]?.SetValue(_sampleSphere);
            _ssao.Parameters["bias"]?.SetValue(Bias);
            _ssao.Parameters["BufferTextureSize"]?.SetValue(new Vector2(_ssaoTarget.Width, _ssaoTarget.Height));
            _ssao.CurrentTechnique.Passes[0].Apply();

            _fsq.Draw(gameTime);
        }

        void BlurSsao(GameTime gameTime, DeferredRenderer deferred)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetRenderTarget(_blurTarget);
            GraphicsDevice.Clear(Color.TransparentBlack);

            _ssaoBlur.Parameters["NormalBuffer"]?.SetValue(deferred.Normals);
            _ssaoBlur.Parameters["DepthBuffer"]?.SetValue(deferred.Depth);
            _ssaoBlur.Parameters["SSAO"]?.SetValue(_ssaoTarget);
            _ssaoBlur.Parameters["BlurDirection"]?.SetValue(new Vector2(0,1));
            _ssaoBlur.Parameters["TargetSize"]?.SetValue(new Vector2(_ssaoTarget.Width, _ssaoTarget.Height));
            _ssaoBlur.CurrentTechnique.Passes[0].Apply();
            
            _fsq.Draw(gameTime);
        }

        void Compose(GameTime gameTime, Texture scene, RenderTarget2D output, bool useBlurredSsao)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(Color.White);

            _composer.Parameters["Scene"]?.SetValue(scene);
            _composer.Parameters["SSAO"]?.SetValue(useBlurredSsao ? _blurTarget : _ssaoTarget);
            _composer.CurrentTechnique.Passes[0].Apply();

            _fsq.Draw(gameTime);
        }

        public void Modify(KeyboardState current)
        {
            var speed = 0.01f;
            if (current.IsKeyDown(Keys.Z)) SampleRadius -= speed;
            if (current.IsKeyDown(Keys.X)) SampleRadius += speed;
            if (current.IsKeyDown(Keys.C)) Bias -= speed;
            if (current.IsKeyDown(Keys.V)) Bias += speed;
        }

        public int Debug(SpriteBatch spriteBatch, SpriteFont spriteFont, int x, int size)
        {
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp);

            var rect = new Rectangle(x, 0, size, size);
            spriteBatch.Draw(_ssaoTarget, rect, Color.White);
            rect.X += size;
            spriteBatch.Draw(_blurTarget, rect, Color.White);

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            spriteBatch.DrawString(spriteFont, "Radius: " + SampleRadius, new Vector2(0, size + 10), Color.Red);
            spriteBatch.DrawString(spriteFont, "Bias: " + Bias, new Vector2(0, size + 30), Color.Blue);

            spriteBatch.End();

            return rect.X + size;
        }
    }
}
