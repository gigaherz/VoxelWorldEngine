using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Storage;
using VoxelWorldEngine.Terrain.Graphics;
using VoxelWorldEngine.Util;
using VoxelWorldEngine.Util.Performance;
using VoxelWorldEngine.Util.Providers;
using VoxelWorldEngine.Util.Scheduler;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        // Tile properties
        private static readonly int RealSizeH = 32;
        private static readonly int RealSizeV = 32;
        private static readonly int GridSizeH = 64;
        private static readonly int GridSizeV = 64;
        private static readonly float VoxelSizeH = RealSizeH / (float)GridSizeH;
        private static readonly float VoxelSizeV = RealSizeV / (float)GridSizeV;
        public static readonly Vector3I GridSize = new Vector3I(GridSizeH, GridSizeV, GridSizeH);
        public static readonly Vector3I RealSize = new Vector3I(RealSizeH, RealSizeV, RealSizeH);
        public static readonly Vector3 VoxelSize = new Vector3(VoxelSizeH, VoxelSizeV, VoxelSizeH);

        private ushort[] _gridBlock;
        private int[] _heightmap;
        private readonly CubeTree<ISerializable> _gridExtra = new CubeTree<ISerializable>();

        private volatile GenerationStage _generationPhase = GenerationStage.Unstarted;
        private GenerationStage _previousGenerationPhase = GenerationStage.Unstarted;
        private bool _isSparse = true;

        private bool _isSolid = false;
        private bool[] _isSolidPlane = new bool[GridSizeV];

        private object _lock = new object();

        public bool IsSparse => _isSparse;
        public bool IsSolid => _isSolid;
        public GenerationStage GenerationPhase => _generationPhase;
        public GenerationStage RequiredPhase { get; private set; } = GenerationStage.Density;

        public bool Initialized { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsSaved { get; }

        public bool IsLoaded { get; private set; }

        public Grid Parent { get; }
        public GenerationContext Context { get; }

        public TileGraphics Graphics { get; }

        public Vector3I Index { get; }
        public Vector3I Offset { get; }

        public EntityPosition Centroid { get; }

        private readonly ProviderMap valueProviders = new ProviderMap();

        public ValueProvider3D<double> GetRawDensityProvider() =>
            valueProviders.GetOrAdd(ProviderType.RAW_DENSITY, k =>
                new CachingValueProvider3D<double>(Offset, GridSize, Context.RawDensityProvider));
        public ValueProvider2D<(double, double, double)> GetTopologyProvider() =>
            valueProviders.GetOrAdd(ProviderType.TOPOLOGY, k =>
                new CachingValueProvider2D<(double, double, double)>(Offset.XZ, GridSize.XZ, Context.TopologyProvider));
        public ValueProvider3D<double> GetDensityProvider() =>
            valueProviders.GetOrAdd(ProviderType.DENSITY, k => new DensityProvider(GetRawDensityProvider(), GetTopologyProvider()));

        private readonly ConcurrentQueue<Action>[] _pendingActions;

        public Tile(Grid parent, Vector3I index)
            : base(parent.Game)
        {
            Parent = parent;
            Context = parent.GenerationContext;
            Index = index;
            Offset = index * GridSize;

            Centroid = EntityPosition.Create(Index, RealSize * 0.5f);

            Graphics = new TileGraphics(this);

            List<ConcurrentQueue<Action>> acts = new List<ConcurrentQueue<Action>>();
            for(int i=0;i<=((int)GenerationStage.Completed+1);i++)
            {
                acts.Add(new ConcurrentQueue<Action>());
            }

            _pendingActions = acts.ToArray();
        }

        private void OnGenerationPhaseChange()
        {
            Parent.TilePhaseChanged(this);
        }

        public bool Load()
        {
            IsLoaded = true;
        }

        public bool Unload()
        {
            IsLoaded = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="phase"></param>
        /// <param name="action"></param>
        /// <returns>True if the action was executed immediately</returns>
        public bool InvokeAfter(GenerationStage phase, Action action)
        {
            if (_generationPhase >= phase && Thread.CurrentThread == VoxelGame.GameThread)
            {
                action();
                return true;
            }
            else
            {
                _pendingActions[(int)phase + 1].Enqueue(action);
                return false;
            }
        }

        public void ScheduleOnUpdate(Action action)
        {
            InvokeAfter(GenerationPhase, action);
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

        private void MarkNeighboursDirty()
        {
            using (Profiler.CurrentProfiler.Begin("Mark Neighbours Dirty"))
            {
                InvokeAfter(GenerationPhase, () =>
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
                });
            }
        }

        public Task RunProcess(Action process, GenerationStage phase, PriorityClass priorityClass, string processName)
        {
            //Debug.WriteLine($"Starting Process {processName} for tile {Index} during phase {phase}");
            return PriorityScheduler.Schedule(process, priorityClass, Centroid).Task;
        }

        private void MarkDirty()
        {
            if (!_isSparse)
                IsDirty = true;
        }

        public bool RequirePhase(GenerationStage required)
        {
            using (Profiler.CurrentProfiler.Begin("Require Phase"))
            {
                if (GenerationPhase >= required)
                    return true;

                RequiredPhase = (GenerationStage)Math.Max((int)RequiredPhase, (int)required);

                if (!Initialized)
                    return false;

                switch (GenerationPhase)
                {
                    case GenerationStage.Unstarted: PrepareGenerationDensity(); break;
                    case GenerationStage.Density: PrepareGenerationTerrain(); break;
                    case GenerationStage.Terrain: PrepareGenerationSurface(); break;
                    default:
                        _generationPhase = GenerationStage.Completed; break;
                }

                return false;
            }
        }

        void PrepareGenerationDensity()
        {
            if (_generationPhase >= GenerationStage.Density)
                return;

            RunProcess(GenDensity, GenerationStage.Density, PriorityClass.Average, "Generating Density Grid");
        }

        void PrepareGenerationTerrain()
        {
            if (_generationPhase >= GenerationStage.Terrain)
                return;

            RunProcess(GenTerrain, GenerationStage.Terrain, PriorityClass.Average, "Calculating Terrain");
        }

        private void PrepareGenerationSurface()
        {
            if (_generationPhase >= GenerationStage.Surface)
                return;

            if (_isSparse)
            {
                // We don't need to generate surfaces so don't queue the task at all
                _generationPhase = GenerationStage.Surface;
                return;
            }

            Task.WhenAll(
                Parent.Request(Index.Offset(0, 1, 0), GenerationStage.Terrain, "surface generation up"),
                Parent.Request(Index.Offset(0, -1, 0), GenerationStage.Terrain, "surface generation down")
            ).ContinueWith(_ => RunProcess(PostProcessSurface, GenerationStage.Surface, PriorityClass.Average, "Processing Surface"));
        }

        void GenDensity()
        {
            using (Profiler.CurrentProfiler.Begin("Generating Density"))
            {
                if (Offset.Y + GridSize.Y >= Context.WorldFloor)
                {
                    var densityProvider = GetDensityProvider();
                    densityProvider.Get(Offset);
                }
                _generationPhase = GenerationStage.Density;

                if (RequiredPhase > _generationPhase)
                    PrepareGenerationTerrain();
            }
        }

        void GenTerrain()
        {
            using (Profiler.CurrentProfiler.Begin("Generating Terrain"))
            {
                if (Offset.Y + GridSize.Y >= Context.WorldFloor)
                {
                    var densityProvider = GetDensityProvider();

                    //lock (_gridBlock)
                    {
                        int relativeWaterLevel = Math.Min(GridSizeV, Context.WaterLevel - Offset.Y);

                        for (int z = 0; z < GridSizeH; z++)
                        {
                            for (int x = 0; x < GridSizeH; x++)
                            {
                                var pxz = Offset.Offset(x, 0, z);

                                var topSolid = -1;
                                for (int y = 0; y < GridSizeV; y++)
                                {
                                    var pxyz = Offset.Offset(x, y, z);

                                    var block = Block.Unbreakite;

                                    // TODO: Add back optional ceiling/floor generation
                                    if (Offset.Y + y > Context.WorldFloor)
                                    {
                                        // density interpolation
                                        var density = densityProvider.Get(pxyz);

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

                                if (!_isSparse)
                                {
                                    _heightmap[z * GridSizeH + x] = topSolid;
                                }
                            }
                        }

                    }
                }

                _generationPhase = GenerationStage.Terrain;

                if (RequiredPhase > _generationPhase)
                    PrepareGenerationSurface();

                InvokeAfter(_generationPhase, MarkNeighboursDirty);
            }
        }


        private readonly float rsqr2 = 1.0f / (float)Math.Sqrt(2);
        private void PostProcessSurface()
        {
            using (Profiler.CurrentProfiler.Begin("Processing Surface"))
            {
                lock (_lock)
                {
                    var densityProvider = GetDensityProvider();
                    var topologyProvider = GetTopologyProvider();
                    int relativeWaterLevel = Context.WaterLevel - Offset.Y;
                    Vector2I xZ = Offset.XZ;
                    for (int z = 0; z < GridSizeH; z++)
                    {
                        for (int x = 0; x < GridSizeH; x++)
                        {
                            var (roughness, dbottom, dtop) = topologyProvider.Get(xZ.Offset(x, z));

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

                                    int yCeiling = 128; // GetProbableCeilingPosition(x, y + 1, z, GridSize.Y); // search up to one chunk's height up, for now
                                    bool hasCeiling = (yCeiling - y) < GridSize.Y;

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

                    _generationPhase = GenerationStage.Surface;
                }

                InvokeAfter(_generationPhase, MarkNeighboursDirty);
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

        public int FindCeilingAbove(int x, int top, int z, int maxscan)
        {
            using (Profiler.CurrentProfiler.Begin("Find Ceiling"))
            {
                var pos = Offset.Offset(x, top, z);
                var startY = pos.Y;

                var densityProvider = Context.DensityProvider;

                double density;
                do
                {
                    pos += new Vector3I(0, 1, 0);
                    density = densityProvider.Get(pos);
                }
                while (density < 0 && (pos.Y - startY) < maxscan);

                if ((pos.Y - startY) >= maxscan)
                    return int.MaxValue;

                return (pos.Y - startY) + top;
            }
        }

        private bool IsPosInside(Vector3I pos)
        {
            var p = pos.Offset(-Offset.X, -Offset.Y, -Offset.Z);
            return p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < GridSize.X && p.Y < GridSize.Y && p.Z < GridSize.Z;
        }

        public int GetSurfacePosition(int x, int top, int z, int maxscan)
        {
            using (Profiler.CurrentProfiler.Begin("Find Surface"))
            {
                var pos = Offset.Offset(x, top, z);
                var startY = pos.Y;

                var densityProvider = GetDensityProvider();
                double density;
                do
                {
                    pos += new Vector3I(0, 1, 0);
                    if (IsPosInside(pos))
                    {
                        density = densityProvider.Get(pos);
                    }
                    else
                    {
                        var task = Parent.RequireTileCoords(pos, GenerationStage.Terrain, "surface generation find ceiling");
                        task.Wait();
                        var tile = task.Result;
                        if (tile != null)
                        {
                            density = tile.GetDensityProvider().Get(pos);
                        }
                        else
                        {
                            density = 0;
                        }
                    }
                }
                while (density >= 0 && (pos.Y - startY) < maxscan);

                return (pos.Y - startY) + top;
            }
        }

        private float GetSlopeAt(int x, int y, int z, int searchLimit, double roughness, double dbottom, double dtop)
        {
            using (Profiler.CurrentProfiler.Begin("Calculate Slope"))
            {
                double top0 = Context.HeightProvider.Get(x, z + 1);
                double top1 = Context.HeightProvider.Get(x, z - 1);
                double top2 = Context.HeightProvider.Get(x - 1, z);
                double top3 = Context.HeightProvider.Get(x + 1, z);
                double top4 = Context.HeightProvider.Get(x - 1, z + 1);
                double top5 = Context.HeightProvider.Get(x + 1, z - 1);
                double top6 = Context.HeightProvider.Get(x + 1, z + 1);
                double top7 = Context.HeightProvider.Get(x - 1, z - 1);

                return (float)((
                    Math.Abs(top0 - top1) +
                    Math.Abs(top2 - top3) +
                    Math.Abs(top4 - top5) * rsqr2 + 
                    Math.Abs(top6 - top7) * rsqr2
                    ) * (1.0f / (2 + 2 * rsqr2)));
            }
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
            using (Profiler.CurrentProfiler.Begin("Tile Update"))
            {
                for (int i = 0; i < _pendingActions.Length && i <= ((int)_generationPhase + 1); i++)
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

                if (IsDirty && _generationPhase >= GenerationStage.Surface)
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
        }

        public CrappyChunkStorage.RegionData.TileData Serialize()
        {
            lock (_lock)
            {
                var t = new CrappyChunkStorage.RegionData.TileData();

                t._indexX = Index.X;
                t._indexY = Index.Y;
                t._indexZ = Index.Z;
                t._generationState = (int)_generationPhase;

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
                _generationPhase = (GenerationStage)data._generationState;

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