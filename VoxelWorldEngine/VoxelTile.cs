using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Rendering;

namespace VoxelWorldEngine
{
    public class VoxelTile : DrawableGameComponent
    {
        public const float VoxelSizeX = 1.0f;
        public const float VoxelSizeY = 0.5f;
        public const float VoxelSizeZ = 1.0f;
        public const int TileSizeX = 16;
        public const int TileSizeY = 32;
        public const int TileSizeZ = 16;
        public const int WaterLevel = 64;
        public const int DepthLevel = 32;
        public const int TopLevel = 128;

        public const int BeachTop = 3;
        public const int BeachBottom = 5;

        private readonly VoxelGrid _parent;

        private readonly int _indexX;
        private readonly int _indexY;
        private readonly int _indexZ;
        private readonly int _offX;
        private readonly int _offY;
        private readonly int _offZ;

        private readonly Block[,,] _gridBlock = new Block[TileSizeZ, TileSizeX, TileSizeY];
        private readonly uint[,,]  _occlusion = new uint[TileSizeZ, TileSizeX, TileSizeY];
        private readonly Dictionary<Vector3I, object> _gridExtra = new Dictionary<Vector3I, object>();
        private readonly int[,] _heightmap = new int[TileSizeX, TileSizeZ]; // Solids only

        private bool _modified;
        private bool _saved;
        private bool _processing;
        private bool _generating;

        public Simplex PerlinDensity { get; }
        public Simplex PerlinHeight { get; }
        public Simplex PerlinRoughness { get; }

        private HashSet<Mesh> _meshes = new HashSet<Mesh>();
        private ConcurrentQueue<List<MeshBuilder>> _builders = new ConcurrentQueue<List<MeshBuilder>>();

        public VoxelTile(VoxelGrid parent, int idxX, int idxY, int idxZ)
            : base(parent.Game)
        {
            _parent = parent;
            _indexX = idxX;
            _indexY = idxY;
            _indexZ = idxZ;
            _offX = idxX * TileSizeX;
            _offY = idxY * TileSizeY;
            _offZ = idxZ * TileSizeZ;

            PerlinDensity = new Simplex(parent.Seed);
            PerlinHeight = new Simplex(parent.Seed * 3);
            PerlinRoughness = new Simplex(parent.Seed * 5);

            _processing = true;
        }

        public override void Initialize()
        {
            base.Initialize();
            
            VoxelTile dependant = null;
            if(_indexX > 0)
            {
                if (_parent.Find(_indexX,_indexY-1,_indexZ,out dependant))
                {
                    dependant.InvokeAfterGen(RunGeneration);
                }
            }

            if (dependant == null)
            {
                RunGeneration();
            }
        }

        Queue<Action> pendingActions = new Queue<Action>();
        private void InvokeAfterGen(Action action)
        {
            if (!_generating)
                action();
            else
                pendingActions.Enqueue(action);
        }

        private void RunGeneration()
        {
            Task.Factory.StartNew(MeasureTerrain(GenTerrain),
                CancellationToken.None,
                TaskCreationOptions.None,
                PriorityScheduler.Lowest).ContinueWith(task =>
            {
                _modified = true;
                _processing = false;

                for (int z = _indexZ - 1; z <= _indexZ + 1; z++)
                {
                    for (int x = _indexX - 1; x <= _indexX + 1; x++)
                    {
                        for (int y = _indexY - 1; y <= _indexY + 1; y++)
                        {
                            VoxelTile tile;
                            if (_parent.Find(x, y, z, out tile))
                                tile.MarkDirty();
                        }
                    }
                }

            });
        }

        private void MarkDirty()
        {
            _modified = true;
        }
        
        static DateTime lastPrint = DateTime.Now;
        static readonly ConcurrentQueue<TimeSpan> terrainTimes = new ConcurrentQueue<TimeSpan>();
        static readonly ConcurrentQueue<TimeSpan> meshTimes = new ConcurrentQueue<TimeSpan>();

        private Action MeasureTerrain(Action action)
        {
            return () =>
            {
                var start = DateTime.Now;

                action();

                var end = DateTime.Now;
                terrainTimes.Enqueue(end-start);
            };
        }

        private Action MeasureMesh(Action action)
        {
            return () =>
            {
                var start = DateTime.Now;

                action();

                var end = DateTime.Now;
                meshTimes.Enqueue(end-start);
            };
        }

        static double noiseMax = double.MinValue;
        static double noiseMin = double.MaxValue;

        void GenTerrain()
        {
            PerlinDensity.Initialize();
            PerlinHeight.Initialize();
            PerlinRoughness.Initialize();

            var average = (TopLevel + DepthLevel) / 2;
            var range = (TopLevel - DepthLevel) / 2;

            for (int z = 0; z < TileSizeZ; z++)
            {
                int pz = _indexZ * TileSizeZ + z;
                var nz = (pz + 0.5) / 128;
                var hz = (pz + 0.5) / 256;
                var rz = (pz + 0.5) / 96;

                for (int x = 0; x < TileSizeX; x++)
                {
                    int px = _indexX * TileSizeX + x;
                    var nx = (px + 0.5) / 128;
                    var hx = (px + 0.5) / 256;
                    var rx = (px + 0.5) / 96;

                    var roughness = 1.8 + 0.4 * PerlinRoughness.Noise(rx, rz, 2);

                    var ph = PerlinHeight.Noise(hx, hz, 2);
                    var heightChange = (1 + 0.75 * ph);

                    noiseMin = Math.Min(noiseMin, ph);
                    noiseMax = Math.Max(noiseMax, ph);

                    var baseHeight = average * heightChange;

                    var bottom = baseHeight - range;
                    var top = baseHeight + range;

                    var topSolid = 0;
                    for (int y = 0; y < TileSizeY; y++)
                    {
                        int py = _indexY * TileSizeY + y;
                        var ny = (py + 0.5) / 256;

                        Block block = Block.Unbreakite;

                        if ((_offY + y) > 0)
                        {
                            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (py - bottom) / (top - bottom)));

                            var density = baseDensity + 0.26 * PerlinDensity.Noise(nx, ny, nz, 4, roughness);

                            if (density < 0.0)
                            {
                                block = Block.Air;
                            }
                            else if (density < 0.50)
                            {
                                block = Block.Stone;
                            }
                            else
                            {
                                block = Block.Granite;
                            }
                        }

                        if (block.PhysicsMaterial.IsSolid)
                        {
                            topSolid = Math.Max(topSolid, y);
                        }

                        SetBlock(x, y, z, block);
                    }

                    _heightmap[x, z] = topSolid;
                }
            }

            // Add seawater
            for (int z = 0; z < TileSizeZ; z++)
            {
                for (int x = 0; x < TileSizeX; x++)
                {
                    int y = Math.Min(TileSizeY, WaterLevel - _offY);
                    while (y >= 0 && _parent.GetBlock(_offX+x,_offY+y,_offZ+z) == Block.Air)
                    {
                        SetBlock(x, y, z, Block.SeaWater);
                        y--;
                    }
                }
            }

            // Post-process (add dirt/sand/gravel)
            const int DirtLayers = 3;
            for (int z = 0; z < TileSizeZ; z++)
            {
                for (int x = 0; x < TileSizeX; x++)
                {
                    int top = GetSolidTop(x, z);
                    int yy = top;

                    while (yy >= 0)
                    {
                        var above = _parent.GetBlock(_offX+x, _offY+yy + 1, _offZ+z);
                        for (int y = Math.Max(0, yy - DirtLayers + 1); y <= yy; y++)
                        {
                            var below = _parent.GetBlock(_offX+x, _offY+yy - 1, _offZ+z);
                            if (!below.PhysicsMaterial.IsSolid)
                                break;
                            if (above == Block.Lava)
                            {
                                SetBlock(x, y, z, Block.Granite);
                            }
                            else if (yy >= (WaterLevel - BeachBottom - _offY) && yy <= (WaterLevel + BeachTop - _offY))
                            {
                                SetBlock(x, y, z, Block.Sand);
                            }
                            else if(above.PhysicsMaterial == PhysicsMaterial.Air)
                            {
                                SetBlock(x, y, z, (yy == top) && (y == yy)
                                    ? Block.Grass
                                    : Block.Dirt);
                            }
                            else if (above == Block.SeaWater || above == Block.RiverWater)
                            {
                                SetBlock(x, y, z, Block.Gravel);
                            }
                        }
                        while (--yy >= 0 && GetBlock(x, yy, z).PhysicsMaterial == PhysicsMaterial.Air)
                        {
                        }
                    }
                }
            }
        }

        public int GetSolidTop(int x, int z)
        {
            return _heightmap[x, z];
        }

        public Block GetBlock(int x, int y, int z)
        {
            return y >= TileSizeY || y < 0
                ? Block.Air
                : (_gridBlock[z, x, y] ?? Block.Air);
        }

        public uint GetOcclusion(int x, int y, int z)
        {
            return y >= TileSizeY || y < 0
                ? 0u
                : _occlusion[z, x, y];
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (y < TileSizeY && y >= 0)
                _gridBlock[z, x, y] = block;
        }

        float Drk = 8.0f;
        public void GenMeshes()
        {
            var collectorManager = new VertexCollectorManager();

            var scale = new Vector3(VoxelSizeX, VoxelSizeY, VoxelSizeZ);

            for (int z = 0; z < TileSizeZ; z++)
            {
                int oz = z + _offZ;
                float pz = oz + 0.5f;

                for (int x = 0; x < TileSizeX; x++)
                {
                    int ox = x + _offX;
                    float px = ox + 0.5f;

                    for (int y = 0; y < TileSizeY; y++)
                    {
                        int oy = y + _offY;
                        float py = oy + 0.5f;

                        var block = GetBlock(x, y, z);
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

                        if (s1)
                        {
                            var pos1 = new Vector3(px - 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos2 = new Vector3(px - 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos3 = new Vector3(px - 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            var pos4 = new Vector3(px - 0.5f, py + 0.5f, pz - 0.5f) * scale;

                            bool o_down = IsOpaque(ox - 1, oy - 1, oz);
                            bool o_uppp = IsOpaque(ox - 1, oy + 1, oz);
                            bool o_south= IsOpaque(ox - 1, oy, oz - 1);
                            bool o_north= IsOpaque(ox - 1, oy, oz + 1);
                            
                            uint EDS = Occlusion(center, o_down, o_south);
                            uint EDN = Occlusion(center, o_down, o_north);
                            uint EUS = Occlusion(center, o_uppp, o_south);
                            uint EUN = Occlusion(center, o_uppp, o_north);

                            var color1 = new Color((Drk - EDS) / Drk * Vector3.One);
                            var color2 = new Color((Drk - EDN) / Drk * Vector3.One);
                            var color3 = new Color((Drk - EUN) / Drk * Vector3.One);
                            var color4 = new Color((Drk - EUS) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue, 
                                pos1, pos2, pos3, pos4, 
                                color1,color2,color3,color4,
                                new Vector3(-1, 0, 0));
                        }

                        if (s2)
                        {
                            var pos1 = new Vector3(px + 0.5f, py - 0.5f, pz + 0.5f) * scale;
                            var pos2 = new Vector3(px + 0.5f, py - 0.5f, pz - 0.5f) * scale;
                            var pos3 = new Vector3(px + 0.5f, py + 0.5f, pz - 0.5f) * scale;
                            var pos4 = new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f) * scale;
                            
                            bool o_down = IsOpaque(ox+1, oy - 1, oz);
                            bool o_uppp = IsOpaque(ox+1, oy + 1, oz);
                            bool o_south = IsOpaque(ox+1, oy, oz - 1);
                            bool o_north = IsOpaque(ox+1, oy, oz + 1);

                            uint WDS = Occlusion(center, o_down, o_south);
                            uint WDN = Occlusion(center, o_down, o_north);
                            uint WUS = Occlusion(center, o_uppp, o_south);
                            uint WUN = Occlusion(center, o_uppp, o_north);

                            var color1 = new Color((Drk - WDN) / Drk * Vector3.One);
                            var color2 = new Color((Drk - WDS) / Drk * Vector3.One);
                            var color3 = new Color((Drk - WUS) / Drk * Vector3.One);
                            var color4 = new Color((Drk - WUN) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue,
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

                            bool o_west = IsOpaque(ox - 1, oy-1, oz);
                            bool o_east = IsOpaque(ox + 1, oy-1, oz);
                            bool o_south = IsOpaque(ox, oy-1, oz - 1);
                            bool o_north = IsOpaque(ox, oy-1, oz + 1);
                            
                            uint WUS = Occlusion(o_west, center, o_south);
                            uint EUS = Occlusion(o_east, center, o_south);
                            uint WUN = Occlusion(o_west, center, o_north);
                            uint EUN = Occlusion(o_east, center, o_north);

                            var color1 = new Color((Drk - WUS) / Drk * Vector3.One);
                            var color2 = new Color((Drk - EUS) / Drk * Vector3.One);
                            var color3 = new Color((Drk - EUN) / Drk * Vector3.One);
                            var color4 = new Color((Drk - WUN) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue,
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

                            bool o_west = IsOpaque(ox - 1, oy+1, oz);
                            bool o_east = IsOpaque(ox + 1, oy+1, oz);
                            bool o_south = IsOpaque(ox, oy+1, oz - 1);
                            bool o_north = IsOpaque(ox, oy+1, oz + 1);

                            uint WDS = Occlusion(o_west, center, o_south);
                            uint EDS = Occlusion(o_east, center, o_south);
                            uint WDN = Occlusion(o_west, center, o_north);
                            uint EDN = Occlusion(o_east, center, o_north);

                            var color1 = new Color((Drk - WDN) / Drk * Vector3.One);
                            var color2 = new Color((Drk - EDN) / Drk * Vector3.One);
                            var color3 = new Color((Drk - EDS) / Drk * Vector3.One);
                            var color4 = new Color((Drk - WDS) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue,
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

                            bool o_west = IsOpaque(ox - 1, oy, oz-1);
                            bool o_east = IsOpaque(ox + 1, oy, oz-1);
                            bool o_down = IsOpaque(ox, oy - 1, oz-1);
                            bool o_uppp = IsOpaque(ox, oy + 1, oz-1);
                            
                            uint WDN = Occlusion(o_west, o_down, center);
                            uint EDN = Occlusion(o_east, o_down, center);
                            uint WUN = Occlusion(o_west, o_uppp, center);
                            uint EUN = Occlusion(o_east, o_uppp, center);

                            var color1 = new Color((Drk - EDN) / Drk * Vector3.One);
                            var color2 = new Color((Drk - WDN) / Drk * Vector3.One);
                            var color3 = new Color((Drk - WUN) / Drk * Vector3.One);
                            var color4 = new Color((Drk - EUN) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue,
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

                            bool o_west = IsOpaque(ox - 1, oy, oz+1);
                            bool o_east = IsOpaque(ox + 1, oy, oz+1);
                            bool o_down = IsOpaque(ox, oy - 1, oz+1);
                            bool o_uppp = IsOpaque(ox, oy + 1, oz+1);

                            uint WDS = Occlusion(o_west, o_down, center);
                            uint EDS = Occlusion(o_east, o_down, center);
                            uint WUS = Occlusion(o_west, o_uppp, center);
                            uint EUS = Occlusion(o_east, o_uppp, center);

                            var color1 = new Color((Drk - WDS) / Drk * Vector3.One);
                            var color2 = new Color((Drk - EDS) / Drk * Vector3.One);
                            var color3 = new Color((Drk - EUS) / Drk * Vector3.One);
                            var color4 = new Color((Drk - WUS) / Drk * Vector3.One);

                            AddFace(
                                collectorManager, block, queue,
                                pos1, pos2, pos3, pos4,
                                color1, color2, color3, color4,
                                new Vector3(0, 0, 1));
                        }
                    }
                }
            }

            _builders.Enqueue(collectorManager.Collectors.Values.ToList());
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
            var other = _parent.GetBlock(ox, oy, oz, false);

            var mat = other.RenderingMaterial;

            return renderQueue != mat.RenderQueue;
        }

        private bool IsOpaque(int ox, int oy, int oz)
        {
            var other = _parent.GetBlock(ox, oy, oz, false);

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
            if (!_generating)
            {
                while(pendingActions.Count > 0)
                {
                    pendingActions.Dequeue()();
                }
            }

            if (_modified && !_generating && !_processing)
            {
                _modified = false;
                _generating = true;
                Task.Factory.StartNew(MeasureMesh(GenMeshes),
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    PriorityScheduler.BelowNormal);
                _generating = false;
            }

            List<MeshBuilder> builders;
            while (_builders.TryDequeue(out builders))
            {
                foreach(var m in _meshes)
                    m.Dispose();

                _meshes.Clear();

                foreach(var builder in builders)
                {
                    var mesh = builder.Build(Game);
                    if (mesh != null)
                        _meshes.Add(mesh);
                }
            }

            var now = DateTime.Now;
            if ((now - lastPrint).TotalSeconds > 10)
            {
                lastPrint = now;

                var nt = 0;
                var total = TimeSpan.Zero;
                TimeSpan sp;
                while (terrainTimes.TryDequeue(out sp))
                {
                    nt++;
                    total += sp;
                }
                if (nt > 0) Debug.WriteLine("Terrain generation average: " + (total.TotalSeconds * 1000 / nt));

                nt = 0;
                total = TimeSpan.Zero;
                while (meshTimes.TryDequeue(out sp))
                {
                    nt++;
                    total += sp;
                }
                if (nt > 0) Debug.WriteLine("Mesh generation average: " + (total.TotalSeconds * 1000 / nt));
            
            }

            base.Update(gameTime);
        }

        internal void Draw(GameTime gameTime, RenderQueue queue)
        {
            //lock (_meshes)
            {
                foreach (var mesh in _meshes)
                {
                    if (mesh.Queue == queue)
                    {
                        mesh.MakeBuffers();
                        mesh.Draw(gameTime);
                    }
                }
            }
        }
    }
}