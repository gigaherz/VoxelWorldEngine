using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Util;
using VoxelWorldEngine.Util.Performance;
using VoxelWorldEngine.Util.Scheduler;

namespace VoxelWorldEngine.Terrain.Graphics
{
    public class TileGraphics : DrawableGameComponent
    {
        public Grid Parent { get; }
        public Tile Tile { get; }

        public TileGraphics(Tile tile) : base(tile.Game)
        {
            Parent = tile.Parent;
            Tile = tile;
        }

        private readonly ConcurrentQueue<List<MeshBuilder>> _builders = new ConcurrentQueue<List<MeshBuilder>>();

        private readonly MeshBuilderManager _collectorManager = new MeshBuilderManager();

        private const int Ambient = 8; // the bigger this number, the lesser the AO effect

        public HashSet<TileMesh> Meshes { get; } = new HashSet<TileMesh>();

        private readonly Stopwatch stopwatch = new Stopwatch();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var mesh in Meshes)
            {
                mesh.Dispose();
            }
            Meshes.Clear();
        }

        public int Busy;
        public bool Rebuild()
        {
            if (Tile.IsSparse)
            {
                return true;
            }

            if (Interlocked.Exchange(ref Busy, 1) != 0)
                return false;

            Tile.RunProcess(GenMeshes, GenerationStage.Terrain, PriorityClass.High, "Generating Meshes").ContinueWith(_ => Tile.InvokeAfter(GenerationStage.Terrain, AfterGenMeshes));

            return true;
        }

        private void AfterGenMeshes()
        {
            using (Profiler.CurrentProfiler.Begin("After Generate Meshes"))
            {
                Debug.Assert(Thread.CurrentThread == VoxelGame.GameThread);

                List<MeshBuilder> builders;
                while (_builders.TryDequeue(out builders))
                {
                    foreach (var m in Meshes)
                        m.Dispose();

                    Meshes.Clear();

                    foreach (var builder in builders)
                    {
                        var mesh = builder.Build(Game);
                        if (mesh != null)
                            Meshes.Add(mesh);
                        builder.Clear();
                    }
                }
                Interlocked.Exchange(ref Busy, 0);

                _collectorManager.Clear();
            }
        }

        public void GenMeshes()
        {
            using (Profiler.CurrentProfiler.Begin("Generating Meshes"))
            {
                GenMeshesPlain();
            }
        }

        public void GenMeshesPlain()
        {
            _collectorManager.Clear();

            for (int z = 0; z < Tile.GridSize.Z; z++)
            {
                float pz = z + 0.5f;

                for (int x = 0; x < Tile.GridSize.X; x++)
                {
                    float px = x + 0.5f;

                    for (int y = 0; y < Tile.GridSize.Y; y++)
                    {
                        float py = y + 0.5f;

                        var block = Tile.GetBlock(new Vector3I(x, y, z));
                        var queue = block.RenderingMaterial.RenderQueue;

                        if (queue == RenderQueue.None)
                            continue;

                        bool s1 = ShowSide(x - 1, y, z, queue);
                        bool s2 = ShowSide(x + 1, y, z, queue);
                        bool s3 = ShowSide(x, y - 1, z, queue);
                        bool s4 = ShowSide(x, y + 1, z, queue);
                        bool s5 = ShowSide(x, y, z - 1, queue);
                        bool s6 = ShowSide(x, y, z + 1, queue);

                        if (s1)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(-1, 0, 0));
                        }

                        if (s2)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(1, 0, 0));
                        }

                        if (s3)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(0, -1, 0));
                        }

                        if (s4)
                        {
                            var pos1 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(0, 1, 0));
                        }

                        if (s5)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(0, 0, -1));
                        }

                        if (s6)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);

                            var color1 = Color.White;

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color1, color1, color1,
                                new Vector3(0, 0, 1));
                        }
                    }
                }
            }

            _builders.Enqueue(_collectorManager.Collectors.Values.ToList());
        }

        public void GenMeshesOcclusion()
        {
            _collectorManager.Clear();

            for (int z = 0; z < Tile.GridSize.Z; z++)
            {
                float pz = z + 0.5f;

                for (int x = 0; x < Tile.GridSize.X; x++)
                {
                    float px = x + 0.5f;

                    for (int y = 0; y < Tile.GridSize.Y; y++)
                    {
                        float py = y + 0.5f;

                        var block = Tile.GetBlock(new Vector3I(x, y, z));
                        var queue = block.RenderingMaterial.RenderQueue;

                        if (queue == RenderQueue.None)
                            continue;

                        bool s1 = ShowSide(x - 1, y, z, queue);
                        bool s2 = ShowSide(x + 1, y, z, queue);
                        bool s3 = ShowSide(x, y - 1, z, queue);
                        bool s4 = ShowSide(x, y + 1, z, queue);
                        bool s5 = ShowSide(x, y, z - 1, queue);
                        bool s6 = ShowSide(x, y, z + 1, queue);

                        bool center = (s1 || s2 || s3 || s4 || s5 || s6) && IsOpaque(x, y, z);

                        bool onno = (s3 || s1) && IsOpaque(x - 1, y - 1, z);
                        bool onpo = (s4 || s1) && IsOpaque(x - 1, y + 1, z);
                        bool opno = (s3 || s2) && IsOpaque(x + 1, y - 1, z);
                        bool oppo = (s4 || s2) && IsOpaque(x + 1, y + 1, z);
                        bool onon = (s5 || s1) && IsOpaque(x - 1, y, z - 1);
                        bool onop = (s6 || s1) && IsOpaque(x - 1, y, z + 1);
                        bool opon = (s5 || s2) && IsOpaque(x + 1, y, z - 1);
                        bool opop = (s6 || s2) && IsOpaque(x + 1, y, z + 1);
                        bool oonn = (s5 || s3) && IsOpaque(x, y - 1, z - 1);
                        bool oonp = (s6 || s3) && IsOpaque(x, y - 1, z + 1);
                        bool oopn = (s5 || s4) && IsOpaque(x, y + 1, z - 1);
                        bool oopp = (s6 || s4) && IsOpaque(x, y + 1, z + 1);

                        if (s1)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);

                            var c1 = (int)((Ambient - Occlusion(center, onno, onon)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(center, onno, onop)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(center, onpo, onop)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(center, onpo, onon)) * 255 / Ambient);

                            var color1 = new Color(c1,c1,c1);
                            var color2 = new Color(c2,c2,c2);
                            var color3 = new Color(c3,c3,c3);
                            var color4 = new Color(c4,c4,c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(-1, 0, 0));
                        }

                        if (s2)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);

                            var c1 = (int)((Ambient - Occlusion(center, opno, opop)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(center, opno, opon)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(center, oppo, opon)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(center, oppo, opop)) * 255 / Ambient);

                            var color1 = new Color(c1, c1, c1);
                            var color2 = new Color(c2, c2, c2);
                            var color3 = new Color(c3, c3, c3);
                            var color4 = new Color(c4, c4, c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(1, 0, 0));
                        }

                        if (s3)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);

                            var c1 = (int)((Ambient - Occlusion(onno, center, oonn)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(opno, center, oonn)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(opno, center, oonp)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(onno, center, oonp)) * 255 / Ambient);

                            var color1 = new Color(c1, c1, c1);
                            var color2 = new Color(c2, c2, c2);
                            var color3 = new Color(c3, c3, c3);
                            var color4 = new Color(c4, c4, c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, -1, 0));
                        }

                        if (s4)
                        {
                            var pos1 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);

                            var c1 = (int)((Ambient - Occlusion(onpo, center, oopp)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(oppo, center, oopp)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(oppo, center, oopn)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(onpo, center, oopn)) * 255 / Ambient);

                            var color1 = new Color(c1, c1, c1);
                            var color2 = new Color(c2, c2, c2);
                            var color3 = new Color(c3, c3, c3);
                            var color4 = new Color(c4, c4, c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 1, 0));
                        }

                        if (s5)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f);
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f);
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f);
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f);

                            var c1 = (int)((Ambient - Occlusion(opon, oonn, center)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(onon, oonn, center)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(onon, oopn, center)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(opon, oopn, center)) * 255 / Ambient);

                            var color1 = new Color(c1, c1, c1);
                            var color2 = new Color(c2, c2, c2);
                            var color3 = new Color(c3, c3, c3);
                            var color4 = new Color(c4, c4, c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 0, -1));
                        }

                        if (s6)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f);
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f);
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f);
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f);

                            var c1 = (int)((Ambient - Occlusion(onop, oonp, center)) * 255 / Ambient);
                            var c2 = (int)((Ambient - Occlusion(opop, oonp, center)) * 255 / Ambient);
                            var c3 = (int)((Ambient - Occlusion(opop, oopp, center)) * 255 / Ambient);
                            var c4 = (int)((Ambient - Occlusion(onop, oopp, center)) * 255 / Ambient);

                            var color1 = new Color(c1, c1, c1);
                            var color2 = new Color(c2, c2, c2);
                            var color3 = new Color(c3, c3, c3);
                            var color4 = new Color(c4, c4, c4);

                            AddFace(block.RenderingMaterial,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 0, 1));
                        }
                    }
                }
            }

            _builders.Enqueue(_collectorManager.Collectors.Values.ToList());
        }

        private uint Occlusion(bool a, bool b, bool c)
        {
            uint t = 0;
            if (a && b) t++;
            if (a && c) t++;
            if (b && c) t++;
            return t;
        }

        private bool ShowSide(int x, int y, int z, RenderQueue renderQueue)
        {
            var other = Tile.GetBlockRelative(x, y, z, false);

            var mat = other.RenderingMaterial;

            return renderQueue != mat.RenderQueue;
        }

        private bool IsOpaque(int x, int y, int z)
        {
            var other = Tile.GetBlockRelative(x, y, z, false);

            var mat = other.RenderingMaterial;
            return mat.RenderQueue != RenderQueue.None && mat.Translucency < 0.01f;
        }

        private void AddFace(RenderingMaterial mat,
            Vector3 pos1, Vector3 pos2, Vector3 pos3, Vector3 pos4,
            Color color1, Color color2, Color color3, Color color4,
            Vector3 normal)
        {
            Vector2 tex1, tex2, tex3, tex4;
            mat.GetTexCoords(out tex1, out tex2, out tex3, out tex4);

            var a = (byte)(255 * (1 - mat.Translucency));
            color1.A = a;
            color2.A = a;
            color3.A = a;
            color4.A = a;

            var collector = _collectorManager.Get(mat.RenderQueue);
            var scale = Tile.VoxelSize;
            collector.AddQuad(
                new VertexFormats.PosColorTexNormal(pos1 * scale, normal, color1, tex1),
                new VertexFormats.PosColorTexNormal(pos2 * scale, normal, color2, tex2),
                new VertexFormats.PosColorTexNormal(pos3 * scale, normal, color3, tex3),
                new VertexFormats.PosColorTexNormal(pos4 * scale, normal, color4, tex4));
        }

        internal void Draw(GameTime gameTime, BaseCamera camera, RenderQueue queue)
        {
            if (Meshes.Count == 0)
                return;

            var offset = Tile.Centroid.RelativeTo(PlayerController.Instance.PlayerPosition) - Tile.RealSize / 2.0f - new Vector3(0,2.1f,0);
            var world = Matrix.CreateTranslation(offset);

            //var boundingBox = new BoundingBox(Tile.RealSize * -0.5f + offset, Tile.RealSize * 0.5f + offset);

            var boundingBox = new BoundingBox(offset, Tile.RealSize + offset);

            if (!camera.ViewFrustum.Intersects(boundingBox))
                return;

            RenderManager.Instance.CurrentEffect.Parameters["Projection"].SetValue(camera.Projection);
            RenderManager.Instance.CurrentEffect.Parameters["View"].SetValue(camera.View);
            RenderManager.Instance.CurrentEffect.Parameters["World"].SetValue(world);
            RenderManager.Instance.CurrentEffect.Parameters["WorldViewIT"].SetValue(Matrix.Transpose(Matrix.Invert(world * camera.View)));

            foreach (var pass in RenderManager.Instance.CurrentEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.Textures[0] = RenderManager.Instance.TerrainTexture;

                foreach (var mesh in Meshes)
                {
                    if (mesh.Queue == queue)
                    {
                        mesh.Draw(gameTime);
                    }
                }
            }
        }
    }
}
