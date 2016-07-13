using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
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

        private const float Ambient = 8.0f; // the bigger this number, the lesser the AO effect

        public HashSet<Mesh> Meshes { get; } = new HashSet<Mesh>();

        public void GenMeshes()
        {
            Parent.TilesInProgress++;

            _collectorManager.Clear();

            var scale = new Vector3(Tile.VoxelSizeX, Tile.VoxelSizeY, Tile.VoxelSizeZ);

            for (int z = 0; z < Tile.SizeXZ; z++)
            {
                int oz = z + Tile.OffZ;
                float pz = oz + 0.5f;

                for (int x = 0; x < Tile.SizeXZ; x++)
                {
                    int ox = x + Tile.OffX;
                    float px = ox + 0.5f;

                    for (int y = 0; y < Tile.SizeY; y++)
                    {
                        int oy = y + Tile.OffY;
                        float py = oy + 0.5f;

                        var block = Parent.GetBlock(ox, oy, oz);
                        var queue = block.RenderingMaterial.RenderQueue;

                        if (queue == RenderQueue.None)
                            continue;

                        bool s1 = ShowSide(ox - 1, oy, oz, queue);
                        bool s2 = ShowSide(ox + 1, oy, oz, queue);
                        bool s3 = ShowSide(ox, oy - 1, oz, queue);
                        bool s4 = ShowSide(ox, oy + 1, oz, queue);
                        bool s5 = ShowSide(ox, oy, oz - 1, queue);
                        bool s6 = ShowSide(ox, oy, oz + 1, queue);

                        bool center = IsOpaque(ox, oy, oz);

                        bool onno = IsOpaque(ox - 1, oy - 1, oz);
                        bool onpo = IsOpaque(ox - 1, oy + 1, oz);
                        bool onon = IsOpaque(ox - 1, oy, oz - 1);
                        bool onop = IsOpaque(ox - 1, oy, oz + 1);
                        bool opno = IsOpaque(ox + 1, oy - 1, oz);
                        bool oppo = IsOpaque(ox + 1, oy + 1, oz);
                        bool opon = IsOpaque(ox + 1, oy, oz - 1);
                        bool opop = IsOpaque(ox + 1, oy, oz + 1);
                        bool oonn = IsOpaque(ox, oy - 1, oz - 1);
                        bool oonp = IsOpaque(ox, oy - 1, oz + 1);
                        bool oopn = IsOpaque(ox, oy + 1, oz - 1);
                        bool oopp = IsOpaque(ox, oy + 1, oz + 1);

                        if (s1)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f) * scale;

                            uint eds = Occlusion(center, onno, onon);
                            uint edn = Occlusion(center, onno, onop);
                            uint eus = Occlusion(center, onpo, onon);
                            uint eun = Occlusion(center, onpo, onop);

                            var color1 = new Color((Ambient - eds) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - edn) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - eun) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - eus) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(-1, 0, 0));
                        }

                        if (s2)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f) * scale;
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f) * scale;

                            uint wds = Occlusion(center, opno, opon);
                            uint wdn = Occlusion(center, opno, opop);
                            uint wus = Occlusion(center, oppo, opon);
                            uint wun = Occlusion(center, oppo, opop);

                            var color1 = new Color((Ambient - wdn) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - wds) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - wus) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - wun) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(1, 0, 0));
                        }

                        if (s3)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos3 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos4 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f) * scale;

                            uint wus = Occlusion(onno, center, oonn);
                            uint eus = Occlusion(opno, center, oonn);
                            uint wun = Occlusion(onno, center, oonp);
                            uint eun = Occlusion(opno, center, oonp);

                            var color1 = new Color((Ambient - wus) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - eus) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - eun) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - wun) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, -1, 0));
                        }

                        if (s4)
                        {
                            var pos1 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            var pos2 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f) * scale;
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f) * scale;

                            uint wds = Occlusion(onpo, center, oopn);
                            uint eds = Occlusion(oppo, center, oopn);
                            uint wdn = Occlusion(onpo, center, oopp);
                            uint edn = Occlusion(oppo, center, oopp);

                            var color1 = new Color((Ambient - wdn) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - edn) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - eds) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - wds) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 1, 0));
                        }

                        if (s5)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f) * scale;
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f) * scale;

                            uint wdn = Occlusion(onon, oonn, center);
                            uint edn = Occlusion(opon, oonn, center);
                            uint wun = Occlusion(onon, oopn, center);
                            uint eun = Occlusion(opon, oopn, center);

                            var color1 = new Color((Ambient - edn) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - wdn) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - wun) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - eun) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 0, -1));
                        }

                        if (s6)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f) * scale;

                            uint wds = Occlusion(onop, oonp, center);
                            uint eds = Occlusion(opop, oonp, center);
                            uint wus = Occlusion(onop, oopp, center);
                            uint eus = Occlusion(opop, oopp, center);

                            var color1 = new Color((Ambient - wds) / Ambient * Vector3.One);
                            var color2 = new Color((Ambient - eds) / Ambient * Vector3.One);
                            var color3 = new Color((Ambient - eus) / Ambient * Vector3.One);
                            var color4 = new Color((Ambient - wus) / Ambient * Vector3.One);

                            AddFace(
                                _collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 0, 1));
                        }
                    }
                }
            }

            _builders.Enqueue(_collectorManager.Collectors.Values.ToList());

            Parent.TilesInProgress--;
        }

        private uint Occlusion(bool a, bool b, bool c)
        {
            uint t = 0;
            if (a && b) t++;
            if (a && c) t++;
            if (b && c) t++;
            return t;
        }

        private bool ShowSide(int ox, int oy, int oz, RenderQueue renderQueue)
        {
            var other = Parent.GetBlock(ox, oy, oz, false);

            var mat = other.RenderingMaterial;

            return renderQueue != mat.RenderQueue;
        }

        private bool IsOpaque(int ox, int oy, int oz)
        {
            var other = Parent.GetBlock(ox, oy, oz, false);

            var mat = other.RenderingMaterial;
            return mat.RenderQueue != RenderQueue.None && mat.Translucency < 0.01f;
        }

        private static void AddFace(
            VertexCollectorManager collectorManager, Block block, RenderQueue q,
            Vector3 pos1, Vector3 pos2, Vector3 pos3, Vector3 pos4,
            Color color1, Color color2, Color color3, Color color4,
            Vector3 normal)
        {
            Vector2 tex1, tex2, tex3, tex4;
            block.RenderingMaterial.GetTexCoords(out tex1, out tex2, out tex3, out tex4);

            var a = (byte)(255 * (1 - block.RenderingMaterial.Translucency));
            color1.A = a;
            color2.A = a;
            color3.A = a;
            color4.A = a;

            var collector = collectorManager.Get(q);
            collector.AddQuad(
                new VertexFormats.PosColorTexNormal(pos1, normal, color1, tex1),
                new VertexFormats.PosColorTexNormal(pos2, normal, color2, tex2),
                new VertexFormats.PosColorTexNormal(pos3, normal, color3, tex3),
                new VertexFormats.PosColorTexNormal(pos4, normal, color4, tex4));
        }

        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        internal void Draw(GameTime gameTime, RenderQueue queue)
        {
            //lock (_meshes)
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

        public int Busy;
        public bool Rebuild()
        {
            if (Tile.IsSparse)
            {
                return true;
            }

            if (Interlocked.Exchange(ref Busy, 1) != 0)
                return false;

            PriorityScheduler.StartNew(GenMeshes, Tile.Centroid).ContinueWith(task => {
                Tile.InvokeAfter(-1, () =>
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
                });
            });

            return true;
        }
    }
}
