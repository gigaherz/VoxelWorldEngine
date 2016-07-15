using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
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

        private readonly VertexCollectorManager _collectorManager = new VertexCollectorManager();

        private const int Ambient = 8; // the bigger this number, the lesser the AO effect

        public HashSet<Mesh> Meshes { get; } = new HashSet<Mesh>();

        private readonly Stopwatch stopwatch = new Stopwatch();

        private Vector3 Offset;
        private Vector3 Scale;

        public int Busy;
        public bool Rebuild()
        {
            if (Tile.IsSparse)
            {
                return true;
            }

            if (Interlocked.Exchange(ref Busy, 1) != 0)
                return false;

            Offset = new Vector3(Tile.OffX * Tile.VoxelSizeX,
                                 Tile.OffY * Tile.VoxelSizeY,
                                 Tile.OffZ * Tile.VoxelSizeZ);
            Scale = new Vector3(Tile.VoxelSizeX, Tile.VoxelSizeY, Tile.VoxelSizeZ);
#if false
            stopwatch.Restart();
            GenMeshes();
            stopwatch.Stop();
            var middle = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            AfterGenMeshes();
            stopwatch.Stop();
            var after = stopwatch.ElapsedMilliseconds;

            Debug.WriteLine($"Generating meshes... a={middle}, b={after}, p=({Tile.IndexX},{Tile.IndexY},{Tile.IndexZ})");
#else
            Tile.RunProcess(GenMeshes, 0).ContinueWith(_ => Tile.InvokeAfter(-1, AfterGenMeshes));
#endif

            return true;
        }

        private void AfterGenMeshes()
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
        }

        public void GenMeshes()
        {
            _collectorManager.Clear();
            
            for (int z = 0; z < Tile.SizeXZ; z++)
            {
                float pz = z + 0.5f;

                for (int x = 0; x < Tile.SizeXZ; x++)
                {
                    float px = x + 0.5f;

                    for (int y = 0; y < Tile.SizeY; y++)
                    {
                        float py = y + 0.5f;

                        var block = Tile.GetBlock(x, y, z);
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
            collector.AddQuad(
                new VertexFormats.PosColorTexNormal(pos1 * Scale + Offset, normal, color1, tex1),
                new VertexFormats.PosColorTexNormal(pos2 * Scale + Offset, normal, color2, tex2),
                new VertexFormats.PosColorTexNormal(pos3 * Scale + Offset, normal, color3, tex3),
                new VertexFormats.PosColorTexNormal(pos4 * Scale + Offset, normal, color4, tex4));
        }

        internal void Draw(GameTime gameTime, RenderQueue queue)
        {
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
