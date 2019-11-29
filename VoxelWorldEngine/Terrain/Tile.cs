#define INSTRUMENT

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Storage;
using VoxelWorldEngine.Terrain.Graphics;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        // Tile properties
        private static readonly int RealSizeH = 32;
        private static readonly int RealSizeV = 32;
        private static readonly int GridSizeH = 32;
        private static readonly int GridSizeV = 32;
        private static readonly float VoxelSizeH = RealSizeH / (float)GridSizeH;
        private static readonly float VoxelSizeV = RealSizeV / (float)GridSizeV;
        public static readonly Vector3I GridSize = new Vector3I(GridSizeH, GridSizeV, GridSizeH);
        public static readonly Vector3I RealSize = new Vector3I(RealSizeH, RealSizeV, RealSizeH);
        public static readonly Vector3 VoxelSize = new Vector3(VoxelSizeH, VoxelSizeV, VoxelSizeH);

        private ushort[] _gridBlock;
        private int[] _heightmap;
        private readonly CubeTree<ISerializable> _gridExtra = new CubeTree<ISerializable>();

        private int dependencies1 = 0;
        private int _generationPhase = -1;
        private bool _isSparse = true;

        private bool _isSolid = false;
        private bool[] _isSolidPlane = new bool[GridSizeV];

        private object _lock = new object();

        public bool IsSparse => _isSparse;
        public bool IsSolid => _isSolid;
        public int GenerationPhase => _generationPhase;
        public int RequiredPhase { get; private set; } = 0;

        public bool Initialized { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsSaved { get; }

        public Grid Parent { get; }
        public GenerationContext Context { get; }

        public TileGraphics Graphics { get; }

        public Vector3I Index { get; }
        public Vector3I Offset { get; }

        public EntityPosition Centroid { get; }

        private int _previousGenerationPhase = -1;
        private void OnGenerationPhaseChange()
        {
            Parent.TilePhaseChanged(this);
        }

        public Tile(Grid parent, Vector3I index)
            : base(parent.Game)
        {
            Parent = parent;
            Context = parent.GenerationContext;
            Index = index;
            Offset = index * GridSize;

            Centroid = EntityPosition.Create(Index, RealSize * 0.5f);

            Graphics = new TileGraphics(this);
        }

        private readonly ConcurrentQueue<Action>[] _pendingActions = {
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>()
        };

        public void InvokeAfter(int phase, Action action)
        {
            if (_generationPhase >= phase && Thread.CurrentThread == VoxelGame.GameThread)
                action();
            else
                _pendingActions[phase + 1].Enqueue(action);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Graphics.Dispose();
        }

        public override void Initialize()
        {
            base.Initialize();

            Initialized = true;

            RequirePhase(RequiredPhase);
        }

        private void MarkNeighboursDirty(PriorityScheduler.PositionedTask task)
        {
            if (!_isSparse)
                IsDirty = true;

            Tile tile;
            if (Parent.Find(Index.Offset(-1, 0, 0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(+1, 0, 0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0, -1, 0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0, +1, 0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0, 0, -1), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0, 0, +1), out tile)) tile.MarkDirty();
        }

        public PriorityScheduler.PositionedTask RunProcess(Action process, int phase, string processName)
        {
            var processContext = Parent.BeginProcess(processName, this);
            var p = PriorityScheduler.StartNew(process, Centroid);
            p.OnCompleted += t => { processContext.End(); };
            return p;
        }

        private void MarkDirty()
        {
            if (!_isSparse)
                IsDirty = true;
        }

        public bool RequirePhase(int i)
        {
            if (GenerationPhase >= i)
                return true;

            RequiredPhase = Math.Max(RequiredPhase, i);

            if (!Initialized)
                return false;

            switch (GenerationPhase)
            {
            case -1: PreparePhase0(); break;
            case 0: PreparePhase1(); break;
            }

            return false;
        }

        void PreparePhase0()
        {
            if (_generationPhase >= 0)
                return;

            RunProcess(GenTerrain, 0, "Generating Terrain").ContinueWith(MarkNeighboursDirty);
        }


#if INSTRUMENT
        private static int _callsGen0;
        private static double _timeGen0;
#endif

        private static double lerp(double x, double y, double t) { return x + t * (y - x); }

        const int rawDensityResolution = 5;
        const double rawDensityScale = rawDensityResolution - 1.0;
        private double[] rawDensity;
        public double GetSubsampledRawDensity(int x, int y, int z)
        {
            double GZ1 = GridSizeH + 1;
            double GY1 = GridSizeV + 1;

            if (rawDensity == null)
            {
                // Compute raw densities
                rawDensity = new double[rawDensityResolution * rawDensityResolution * rawDensityResolution];

                for (int zi = 0; zi < rawDensityResolution; zi++)
                {
                    for (int xi = 0; xi < rawDensityResolution; xi++)
                    {
                        for (int yi = 0; yi < rawDensityResolution; yi++)
                        {
                            var off = new Vector3D(xi * (GZ1 / rawDensityScale), yi * (GY1 / rawDensityScale), zi * (GZ1 / rawDensityScale));
                            var pxyz = Offset + off;

                            int i = (zi * rawDensityResolution + xi) * rawDensityResolution + yi;

                            rawDensity[i] = Context.GetRawDensityAt(pxyz);
                        }
                    }
                }
            }

            // density interpolation
            return lerp3D(z, x, y, rawDensity, rawDensityResolution, rawDensityResolution, rawDensityScale, GZ1, GY1);
        }

        void GenTerrain()
        {
#if INSTRUMENT
            var stopwatchGen0 = new Stopwatch();
#endif

            using (var unused = new TaskInProgress("GenMeshes", this))
            {
                InitializeStorage();

                //lock (_gridBlock)
                {
#if INSTRUMENT
                    stopwatchGen0.Start();
#endif

                    int relativeWaterLevel = Math.Min(GridSizeV, Context.WaterLevel - Offset.Y);

                    for (int z = 0; z < GridSizeH; z++)
                    {
                        for (int x = 0; x < GridSizeH; x++)
                        {
                            var pxz = Offset.Offset(x, 0, z).XZ;
                            //var roughness = lerp2D(z, x, rawRoughness, rawRoughnessResolution, rawRoughnessScale, GZ1);
                            Context.GetTopologyAt(pxz, out var roughness, out var bottom, out var top);

                            var topSolid = -1;
                            for (int y = 0; y < GridSizeV; y++)
                            {
                                var pxyz = Offset.Offset(x, y, z);

                                var block = Block.Unbreakite;

                                // TODO: Add back optional ceiling/floor generation
                                if (true) //Offset.Y + y > Context.Floor)
                                {
                                    // density interpolation
                                    var dd = GetSubsampledRawDensity(z, x, y);
                                    var density = Context.GetDensityAt(pxyz, roughness, bottom, top, dd);

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
                                else if (block == Block.Air && y < relativeWaterLevel)
                                {
                                    block = Block.SeaWater;
                                }

                                SetBlockNoLock(x, y, z, block);
                            }

                            _heightmap[z * GridSizeH + x] = topSolid;
                        }
                    }

                    Interlocked.Exchange(ref _generationPhase, 0);

#if INSTRUMENT
                    stopwatchGen0.Stop();
                    _callsGen0++;
                    _timeGen0 += stopwatchGen0.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
#endif
                }

                PreparePhase1();
            }
        }

        private static double lerp3D(int z, int x, int y, double[] rawDensity, int dim0, int dim1, double rdgd, double gz1, double gy1)
        {
            var xp = x * (rdgd / gz1);
            var zp = z * (rdgd / gz1);
            var yp = y * (rdgd / gy1);
            var xi = NoiseOctaves.fastfloor(xp);
            var zi = NoiseOctaves.fastfloor(zp);
            var yi = NoiseOctaves.fastfloor(yp);
            var xt = xp - xi;
            var yt = yp - yi;
            var zt = zp - zi;

            var dy000 = rawDensity[(zi * dim1 + xi) * dim0 + yi];
            var dy001 = rawDensity[(zi * dim1 + xi) * dim0 + yi + 1];
            var dy010 = rawDensity[(zi * dim1 + xi + 1) * dim0 + yi];
            var dy011 = rawDensity[(zi * dim1 + xi + 1) * dim0 + yi + 1];
            var dy100 = rawDensity[((zi+1) * dim1 + xi) * dim0 + yi];
            var dy101 = rawDensity[((zi+1) * dim1 + xi) * dim0 + yi + 1];
            var dy110 = rawDensity[((zi+1) * dim1 + xi + 1) * dim0 + yi];
            var dy111 = rawDensity[((zi+1) * dim1 + xi + 1) * dim0 + yi + 1];

            var dx00 = lerp(dy000, dy001, yt);
            var dx01 = lerp(dy010, dy011, yt);
            var dx10 = lerp(dy100, dy101, yt);
            var dx11 = lerp(dy110, dy111, yt);

            var dz0 = lerp(dx00, dx01, xt);
            var dz1 = lerp(dx10, dx11, xt);

            var dd = lerp(dz0, dz1, zt);
            return dd;
        }

        private void PreparePhase1()
        {
            if (_generationPhase >= 1)
                return;

            if (_isSparse)
            {
                // We don't need to generate surfaces so don't queue the task at all
                Interlocked.Exchange(ref _generationPhase, 1);
            }
            else if (RequiredPhase >= 1)
            {
                InvokeAfter(-1, () =>
                {
                    int deps = 0;

                    // Depend on the chunk above, in order to be able to get blocks above the chunk limit
                    if (!Parent.Require(Index.Offset(0, 1, 0), 0, out var depUp))
                    {
                        Interlocked.Increment(ref dependencies1);
                        depUp.InvokeAfter(0, Phase1DependencyCallback);
                        deps++;
                    }

                    // Depend on the chunk below, in order to be able to set blocks when processing surfaces
                    if (!Parent.Require(Index.Offset(0, -1, 0), 0, out var depDown))
                    {
                        Interlocked.Increment(ref dependencies1);
                        depDown.InvokeAfter(0, Phase1DependencyCallback);
                        deps++;
                    }

                    // If all the surrounding chunks are already generated, run immediately
                    if (deps == 0)
                    {
                        RunPhase1();
                    }
                });
            }
        }

        private void Phase1DependencyCallback()
        {
            if (Interlocked.Decrement(ref dependencies1) == 0)
            {
                RunPhase1();
            }
        }

        private void RunPhase1()
        {
            RunProcess(PostProcessSurface, 1, "Processing Surface").ContinueWith(MarkNeighboursDirty);
        }

        private readonly float rsqr2 = 1.0f / (float)Math.Sqrt(2);
        private void PostProcessSurface()
        {
            using (var unused = new TaskInProgress("GenMeshes", this))
            {
                lock (_lock)
                {
                    int relativeWaterLevel = Context.WaterLevel - Offset.Y;
                    for (int z = 0; z < GridSizeH; z++)
                    {
                        for (int x = 0; x < GridSizeH; x++)
                        {
                            Context.GetTopologyAt(Offset.XZ.Offset(x, z), out var roughness, out var dbottom, out var dtop);

                            for (int y = 0; y < GridSizeV; y++)
                            {
                                var block = GetBlock(x, y, z);
                                var above = (y + 1) < GridSizeV ? GetBlock(x, y + 1, z) : GetBlockRelative(x, y + 1, z);

                                if (block.PhysicsMaterial.IsSolid && !above.PhysicsMaterial.IsSolid)
                                {
                                    // Found surface!

                                    float avgSlope = GetSlopeAt(x, y, z, 10, roughness, dbottom, dtop);
                                    if (avgSlope > 2)
                                        continue;
                                    //double avgSlope = GetGradientAt(x, y, z);
                                    //if (avgSlope > 0.01)
                                    //    continue;

                                    int yCeiling = GetProbableCeilingPosition(x, y + 1, z, 128); // search up to one chunk's height up, for now
                                    bool hasCeiling = (yCeiling - y) < 128;

                                    if (GetSurfaceMaterials(above, y, relativeWaterLevel, hasCeiling, out var replaceWith))
                                    {
                                        for (int i = 0; i < replaceWith.Length; i++)
                                        {
                                            SetBlockRelativeNoLock(x, y - i, z, replaceWith[i]);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Interlocked.Exchange(ref _generationPhase, 1);
                }
            }
        }

        private bool GetSurfaceMaterials(Block above, int y, int relativeWaterLevel, bool hasCeiling, out Block[] replaceWith)
        {
            if (above.PhysicsMaterial == PhysicsMaterial.Air)
            {
                replaceWith = new[] { hasCeiling ? Block.Dirt : Block.Grass, Block.Dirt, Block.Dirt, Block.Dirt };
                return true;
            }

            // TODO: only perform this replacement in beachy areas!
            if (y >= relativeWaterLevel - Context.BeachBottom && y <= relativeWaterLevel + Context.BeachTop)
            {
                replaceWith = new[] { Block.Sand, Block.Sand, Block.Sand, Block.Sandstone };
                return true;
            }

            if (above == Block.SeaWater || above == Block.RiverWater)
            {
                replaceWith = new[] { Block.Gravel, Block.Gravel, Block.Gravel, Block.Gravel };
                return true;
            }

            if (above == Block.Lava)
            {
                replaceWith = new[] { Block.Granite, Block.Granite, Block.Granite, Block.Granite };
                return true;
            }

            replaceWith = new Block[0];
            return false;
        }

        public int GetProbableSurfacePosition(int x, int top, int z, int maxscan, double roughness, double dbottom, double dtop)
        {
            var pos = Offset.Offset(x, top, z);
            var startY = pos.Y;

            double density;
            do
            {
                pos += new Vector3I(0, 1, 0);
                density = Context.GetDensityAt(pos, roughness, dbottom, dtop);
            }
            while (density >= 0 && (pos.Y - startY) < maxscan);

            return (pos.Y - startY) + top;
        }

        public int GetProbableCeilingPosition(int x, int top, int z, int maxscan)
        {
            var pos = Offset.Offset(x, top, z);
            var startY = pos.Y;

            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);

            double density;
            do
            {
                pos += new Vector3I(0, 1, 0);
                density = Context.GetDensityAt(pos, roughness, dbottom, dtop);
            }
            while (density < 0 && (pos.Y - startY) < maxscan);

            if ((pos.Y - startY) >= maxscan)
                return int.MaxValue;

            return (pos.Y - startY) + top;
        }

        public double GetSlowDensityAt(int x, int y, int z)
        {
            var pos = Offset.Offset(x, y, z);
            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);
            return Context.GetDensityAt(pos, roughness, dbottom, dtop);
        }

        public double GetFasterDensityAt(int x, int y, int z)
        {
            var pos = Offset.Offset(x, y, z);
            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);

            var dd = GetSubsampledRawDensity(x, y, z);
            return Context.GetDensityAt(pos, roughness, dbottom, dtop, dd);
        }

        private double GetGradientAt(int x, int y, int z)
        {
            double top  = GetFasterDensityAt(x, y, z);
            double top0 = GetFasterDensityAt(x, y, z + 1);
            double top1 = GetFasterDensityAt(x, y, z - 1);
            double top2 = GetFasterDensityAt(x - 1, y, z);
            double top3 = GetFasterDensityAt(x + 1, y, z);
            double top4 = GetFasterDensityAt(x - 1, y, z + 1);
            double top5 = GetFasterDensityAt(x + 1, y, z - 1);
            double top6 = GetFasterDensityAt(x + 1, y, z + 1);
            double top7 = GetFasterDensityAt(x - 1, y, z - 1);

            return (Math.Abs(top0 - top) + Math.Abs(top1 - top) +
                    Math.Abs(top2 - top) + Math.Abs(top3 - top) +
                    (Math.Abs(top4 - top) + Math.Abs(top5 - top) +
                     Math.Abs(top6 - top) + Math.Abs(top7 - top)) * rsqr2) * (1.0 / (4 + 4 * rsqr2));
        }

        private float GetSlopeAt(int x, int y, int z, int searchLimit, double roughness, double dbottom, double dtop)
        {
            var sl2 = searchLimit * 2;
            int top0 = GetProbableSurfacePosition(x, y - searchLimit, z + 1, sl2, roughness, dbottom, dtop);
            int top1 = GetProbableSurfacePosition(x, y - searchLimit, z - 1, sl2, roughness, dbottom, dtop);
            int top2 = GetProbableSurfacePosition(x - 1, y - searchLimit, z, sl2, roughness, dbottom, dtop);
            int top3 = GetProbableSurfacePosition(x + 1, y - searchLimit, z, sl2, roughness, dbottom, dtop);
            int top4 = GetProbableSurfacePosition(x - 1, y - searchLimit, z + 1, sl2, roughness, dbottom, dtop);
            int top5 = GetProbableSurfacePosition(x + 1, y - searchLimit, z - 1, sl2, roughness, dbottom, dtop);
            int top6 = GetProbableSurfacePosition(x + 1, y - searchLimit, z + 1, sl2, roughness, dbottom, dtop);
            int top7 = GetProbableSurfacePosition(x - 1, y - searchLimit, z - 1, sl2, roughness, dbottom, dtop);

            return (Math.Abs(top0 - top1) + Math.Abs(top2 - top3) +
                    (Math.Abs(top4 - top5) + Math.Abs(top6 - top7)) * rsqr2) * (1.0f / (4 + 4 * rsqr2));
        }
        
        public int GetSolidTopRelative(int x, int y, int z)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeH &&
                z >= 0 && z < GridSizeH)
                return GetSolidTop(x, z);

            return Parent.GetSolidTop(new Vector3I(x, y, z) + Offset);
        }

        public int GetSolidTop(int x, int z)
        {
            if (_isSparse)
                return -1;
            return _heightmap[z * GridSizeH + x];
        }

        public Block GetBlockRelative(int x, int y, int z, bool load = true)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                return GetBlock(x, y, z);

            return Parent.GetBlock(new Vector3I(x, y, z) + Offset, load);
        }

        public void SetBlockRelative(int x, int y, int z, Block block)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                SetBlock(new Vector3I(x, y, z), block);

            Parent.SetBlock(new Vector3I(x, y, z) + Offset, block);
        }

        public void SetBlockRelativeNoLock(int x, int y, int z, Block block)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                SetBlockNoLock(x, y, z, block);

            Parent.SetBlock(new Vector3I(x, y, z) + Offset, block);
        }

        public Block GetBlock(Vector3I xyz)
        {
            return GetBlock(xyz.X, xyz.Y, xyz.Z);
        }

        public Block GetBlock(int x, int y, int z)
        {
            if (_isSparse)
                return Block.Air;
            return Block.Registry[_gridBlock[(z * GridSizeH + x) * GridSizeV + y]] ?? Block.Air;
        }

        public void SetBlock(Vector3I xyz, Block block)
        {
            SetBlock(xyz.X, xyz.Y, xyz.Z, block);
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (_isSparse)
            {
                if (block == Block.Air)
                    return;

                lock (_lock)
                {
                    InitializeStorage();
                }
            }

            if (block.Key.InternalId.HasValue)
            {
                lock (_lock)
                {
                    _gridBlock[(z * GridSizeH + x) * GridSizeV + y] = block.Key.InternalId.Value;
                }
            }
        }

        public void SetBlockNoLock(int x, int y, int z, Block block)
        {
            if (_isSparse)
            {
                if (block == Block.Air)
                    return;

                InitializeStorage();
            }

            if (block.Key.InternalId.HasValue)
            {
                _gridBlock[(z * GridSizeH + x) * GridSizeV + y] = block.Key.InternalId.Value;
            }
        }

        private void InitializeStorage()
        {
            _gridBlock = new ushort[GridSizeH * GridSizeH * GridSizeV];
            _heightmap = new int[GridSizeH * GridSizeH];
            _isSparse = false;
        }

        //int _solidCheckIndex = 0;
        public override void Update(GameTime gameTime)
        {
            for (int i = 0; i < _pendingActions.Length && (i - 1) <= _generationPhase; i++)
            {
                Action action;
                while (_pendingActions[i].TryDequeue(out action))
                {
                    action();
                }
            }

            if (_previousGenerationPhase != _generationPhase)
            {
                OnGenerationPhaseChange();
                _previousGenerationPhase = _generationPhase;
            }

            if (Parent.saveInitializationPhase <= 0)
                return;

            if (IsDirty && _generationPhase >= 1)
            {
                if (Graphics.Rebuild())
                {
                    IsDirty = false;
                }
            }

            Graphics.Update(gameTime);

            base.Update(gameTime);

            //if (_isSparse)
            //    _isSolid = false;
            //else
            //{
            //    int plane = _solidCheckIndex++ % GridSizeV;

            //    bool allSolid = true;
            //    for (int z = 0; allSolid & z < GridSizeH; z++)
            //    {
            //        for (int x = 0; allSolid & x < GridSizeH; x++)
            //        {
            //            allSolid &= GetBlock(x, plane, z).PhysicsMaterial.IsSolid;
            //        }
            //    }
            //    _isSolidPlane[plane] = allSolid;

            //    if (_solidCheckIndex % GridSizeV == 0)
            //    {
            //        allSolid = true;
            //        for (int y = 0; allSolid & y < GridSizeH; y++)
            //        {
            //            allSolid &= _isSolidPlane[y];
            //        }
            //        _isSolid = allSolid;
            //    }
            //}
        }

        public CrappyChunkStorage.RegionData.TileData Serialize()
        {
            lock (_lock)
            {
                var t = new CrappyChunkStorage.RegionData.TileData();

                t._indexX = Index.X;
                t._indexY = Index.Y;
                t._indexZ = Index.Z;
                t._generationState = _generationPhase;

                if (_gridBlock != null)
                {
                    Array.Copy(_gridBlock, t._gridBlock, _gridBlock.Length);
                }

                if (_heightmap != null)
                {
                    Array.Copy(_heightmap, t._heightmap, _heightmap.Length);
                }

                t._gridExtra = new byte[0][];
                t._gridEntities = new byte[0][];

                return t;
            }
        }

        public void Deserialize(CrappyChunkStorage.RegionData.TileData data)
        {
            lock (_lock)
            {
                _generationPhase = data._generationState;

                if (_isSparse)
                {
                    if (data._gridBlock.All(b => b == Block.Air.Key.InternalId))
                        return;

                    InitializeStorage();
                }

                Array.Copy(data._gridBlock, _gridBlock, _gridBlock.Length);
                Array.Copy(data._heightmap, _heightmap, _heightmap.Length);

                IsDirty = true;
            }
        }
    }
}