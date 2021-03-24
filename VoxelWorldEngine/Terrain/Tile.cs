using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
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
        private static int NextInstanceId = 0;
        public readonly int UniqueInstanceId = NextInstanceId++;

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

        private GenerationStage _previousCompletedPhase = GenerationStage.Unstarted;
        private bool _isSparse = true;

        private bool _isSolid = false;
        private bool[] _isSolidPlane = new bool[GridSizeV];

        private object _lock = new object();

        public bool IsSparse => _isSparse;
        public bool IsSolid => _isSolid;
        public GenerationStage RequiredPhase { get; private set; } = GenerationStage.Unstarted;
        public GenerationStage GeneratingPhase { get; private set; } = GenerationStage.Unstarted;
        public GenerationStage CompletedPhase { get; private set; } = GenerationStage.Unstarted;

        public bool IsDirty { get; private set; }
        public bool IsSaved { get; }

        public bool IsLoaded { get; private set; }

        public Grid Parent { get; }
        public GenerationContext Context { get; }

        public TileGraphics Graphics { get; }

        public Vector3I Index { get; }
        public Vector3I Offset { get; }

        public EntityPosition Centroid { get; }

        public int UpdateTaskCount => _pendingActions.Sum(t => t.Count());

        private readonly ConcurrentQueue<Action>[] _pendingActions;

        public override string ToString()
        {
            return $"{{{Index}: {CompletedPhase}/{GeneratingPhase}/{RequiredPhase}}}";
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

            List<ConcurrentQueue<Action>> acts = new List<ConcurrentQueue<Action>>();
            foreach (var v in Enum.GetValues(typeof(GenerationStage)))
            {
                acts.Add(new ConcurrentQueue<Action>());
            }

            _pendingActions = acts.ToArray();
        }

        private void OnCompletedPhaseChange()
        {
            Parent.TilePhaseChanged(this);
        }

        public void Load()
        {
            IsLoaded = true;
        }

        public void Unload(bool disposing = false)
        {
            IsLoaded = false;
            RequiredPhase = CompletedPhase;
            Parent.ClearInProgress(this);
            Parent.ClearPending(this);
            Graphics.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="phase"></param>
        /// <param name="action"></param>
        /// <returns>True if the action was executed immediately</returns>
        public bool InvokeWhenCompleted(GenerationStage phase, Action<bool> action, string reason)
        {
            action = TaskInProgress.Start(this, action, $"InvokeWhenCompleted[{phase}]:{reason}");
            if (CompletedPhase >= phase && Thread.CurrentThread == VoxelGame.GameThread)
            {
                action(true);
                return true;
            }
            else
            {
                _pendingActions[(int)phase].Enqueue(() => action(false));
                return false;
            }
        }

        public void ScheduleOnUpdate(Action<bool> action, string reason)
        {
            InvokeWhenCompleted(CompletedPhase, action, reason);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Unload(true);
        }

        public override void Initialize()
        {
            base.Initialize();

            Load();
        }

        private void OnChanged(bool ranImmediately)
        {
            using (Profiler.CurrentProfiler.Begin("Mark Neighbours Dirty"))
            {
                if (!_isSparse)
                    IsDirty = true;

                Tile tile;
                if (Parent.GetTileIfExists(Index.Offset(-1, 0, 0), out tile)) tile.MarkDirty();
                if (Parent.GetTileIfExists(Index.Offset(+1, 0, 0), out tile)) tile.MarkDirty();
                if (Parent.GetTileIfExists(Index.Offset(0, -1, 0), out tile)) tile.MarkDirty();
                if (Parent.GetTileIfExists(Index.Offset(0, +1, 0), out tile)) tile.MarkDirty();
                if (Parent.GetTileIfExists(Index.Offset(0, 0, -1), out tile)) tile.MarkDirty();
                if (Parent.GetTileIfExists(Index.Offset(0, 0, +1), out tile)) tile.MarkDirty();
            }
        }

        public void RunProcess(Action process, PriorityClass priorityClass, string processName)
        {
            process = TaskInProgress.Start(this, process, $"RunProcess[{processName}]");
            LogTileMessage($"Starting Process '{processName}'...");
            PriorityScheduler.Schedule(process, priorityClass, Centroid);
        }

        private volatile int _logCount;

        public void LogTileMessage(string message)
        {
            Program.DebugWriteLine($"[{Index}/{UniqueInstanceId}] {Interlocked.Increment(ref _logCount)} {(int)GeneratingPhase}/{GeneratingPhase}: {message}");
        }

        private void MarkDirty()
        {
            if (!_isSparse)
                IsDirty = true;
        }

        public bool IsAtPhase(GenerationStage stage)
        {
            return CompletedPhase >= stage;
        }

        public bool SetRequiredPhase(GenerationStage required)
        {
            var oldRequired = RequiredPhase;

            RequiredPhase = (GenerationStage)Math.Max(Math.Max((int)RequiredPhase, (int)required), Math.Max((int)GeneratingPhase, (int)CompletedPhase));

            if (oldRequired != RequiredPhase && !IsAtPhase(RequiredPhase))
            {
                LogTileMessage($"New RequiredPhase: {RequiredPhase}");
            }

            return IsAtPhase(RequiredPhase);
        }

        public bool ScheduleNextPhase()
        {
            if (IsAtPhase(RequiredPhase))
            {
                LogTileMessage($"No next phase, {CompletedPhase} == {RequiredPhase}.");
                Parent.ClearInProgress(this);
                return true;
            }

            if (CompletedPhase >= GenerationStage.Completed)
                return true;

            GeneratingPhase = CompletedPhase + 1;

            if (GeneratingPhase < GenerationStage.Completed) Parent.SetInProgress(this);

            switch (GeneratingPhase)
            {
                case GenerationStage.Terrain:
                    RunProcess(GenTerrain, PriorityClass.Average, "Calculating Terrain");
                    break;
                case GenerationStage.Surface:
                    if (_isSparse)
                    {
                        // We don't need to generate surfaces in a sparse tile, so don't queue any task at all!
                        LogTileMessage($"Skipping Surface Generation, Tile is Sparse...");
                        PhaseCompleted();
                    }
                    else
                    {
                        Parent.ClearInProgress(this);
                        if (!Parent.Request(Index.Offset(0, 1, 0), GenerationStage.Terrain, "surface generation up", (b, t) =>
                        {
                            LogTileMessage("Tile above loaded.");
                            if (!Parent.Request(Index.Offset(0, -1, 0), GenerationStage.Terrain, "surface generation down", (b2, t2) =>
                            {
                                LogTileMessage("Tile below loaded.");
                                Parent.SetInProgress(this);
                                RunProcess(GenSurface, PriorityClass.Average, "Processing Surface");
                            }))
                            {
                                LogTileMessage("Waiting for tile below...");
                            }
                        }))
                        {
                            LogTileMessage("Waiting for tile above...");
                        }
                    }
                    break;
                case GenerationStage.Completed:
                    LogTileMessage($"Generation Complete.");
                    CompletedPhase = GeneratingPhase;
                    Parent.ClearInProgress(this);
                    break;
                // TODO: Remaining stages.
                default:
                    PhaseCompleted();
                    break;
            }

            return false;
        }

        private void PhaseCompleted()
        {
            LogTileMessage($"Phase {GeneratingPhase} completed on the way to {RequiredPhase}...");
            CompletedPhase = GeneratingPhase;
            ScheduleOnUpdate(OnChanged, "notify neighbours");
            ScheduleNextPhase();
        }

        void GenTerrain()
        {
            if (!IsLoaded)
            {
                LogTileMessage($"Cancelling GenTerrain, Tile is Unloaded!");
                return;
            }

            using (Profiler.CurrentProfiler.Begin("Generating Terrain"))
            {
                if (Offset.Y + GridSize.Y >= Context.WorldFloor)
                {
                    var densityProvider = new CachingValueProvider3D<double>(Offset, GridSize, new DensityProvider(Context.RawDensityProvider, Context.TopologyProvider));

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

            }

            PhaseCompleted();
        }

        private readonly float rsqr2 = 1.0f / (float)Math.Sqrt(2);
        private void GenSurface()
        {
            if (!IsLoaded)
            {
                LogTileMessage($"Cancelling GenSurface, Tile is Unloaded!");
                return;
            }

            using (Profiler.CurrentProfiler.Begin("Generating Surface"))
            {
                List<(int, int, int, Block)> otherBlockChanges = new List<(int, int, int, Block)>();
                lock (_lock)
                {
                    int relativeWaterLevel = Context.WaterLevel - Offset.Y;
                    Vector2I xZ = Offset.XZ;
                    for (int z = 0; z < GridSizeH; z++)
                    {
                        for (int x = 0; x < GridSizeH; x++)
                        {
                            var y = FindSurfaceFast(x, z);
                            var block = GetBlockRelative(x, y, z);
                            var above = GetBlockRelative(x, y + 1, z);

                            if (block.PhysicsMaterial.IsSolid && !above.PhysicsMaterial.IsSolid)
                            {
                                // Found surface!

                                float avgSlope = GetSlopeAt(x, y, z, 10);
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
                                        if (x >= 0 && x < GridSizeH &&
                                            y >= 0 && y < GridSizeV &&
                                            z >= 0 && z < GridSizeH)
                                            SetBlockNoLock(x, y, z, block);

                                        otherBlockChanges.Add((x, y - i, z, replaceWith[i]));
                                    }
                                }
                            }
                        }
                    }
                }
                foreach(var (x, y, z, b) in otherBlockChanges)
                {
                    SetBlockRelativeNoLock(x, y, z, b);
                }
            }

            PhaseCompleted();
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

        private float GetSlopeAt(int x, int y, int z, int searchLimit)
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

        private int FindSurfaceFast(int x, int z)
        {
            var y = GetSolidTop(x, z);
            if (y == GridSizeV - 1)
            {
                if (Parent.GetTileIfExists(Index.Offset(0,1,0), out var above))
                {
                    return above.GetSolidTop(x, z)+GridSizeV;
                }
            }
            else if (y == 0)
            {
                if (Parent.GetTileIfExists(Index.Offset(0, 1, 0), out var below))
                {
                    return below.GetSolidTop(x, z)-GridSizeV;
                }
            }
            return y;
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

                using (Profiler.CurrentProfiler.Begin("Tile Update 2"))
                {

                    using (Profiler.CurrentProfiler.Begin("Processing Tasks"))
                    {
                        for (int i = 0; i < _pendingActions.Length && i <= (int)CompletedPhase; i++)
                        {
                            while (_pendingActions[i].TryDequeue(out var action))
                            {
                                action();
                            }
                        }
                    }

                    if (_previousCompletedPhase != CompletedPhase)
                    {
                        _previousCompletedPhase = CompletedPhase;
                        using (Profiler.CurrentProfiler.Begin("Notifying Listeners"))
                        {
                            OnCompletedPhaseChange();
                        }
                    }

                    /*if (CompletedPhase == GenerationStage.Unstarted && RequiredPhase > GenerationStage.Unstarted && GeneratingPhase == CompletedPhase)
                    {
                        ScheduleNextPhase();
                    }*/

                    if (Parent.saveInitializationPhase <= 0)
                        return;

                    if (IsDirty && CompletedPhase >= GenerationStage.Surface)
                    {
                        using (Profiler.CurrentProfiler.Begin("Graphics Rebuild"))
                        {
                            if (Graphics.Rebuild())
                            {
                                IsDirty = false;
                            }
                        }
                    }

                    using (Profiler.CurrentProfiler.Begin("Graphics Update"))
                    {
                        Graphics.Update(gameTime);
                    }

                    using (Profiler.CurrentProfiler.Begin("base.Update"))
                    {
                        base.Update(gameTime);
                    }


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
        }

        public CrappyChunkStorage.RegionData.TileData Serialize()
        {
            lock (_lock)
            {
                var t = new CrappyChunkStorage.RegionData.TileData();

                t._indexX = Index.X;
                t._indexY = Index.Y;
                t._indexZ = Index.Z;
                t._generationState = (int)CompletedPhase;

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
                GeneratingPhase = CompletedPhase = (GenerationStage)data._generationState;

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

        private readonly List<TaskInProgress> tasksInProgess = new List<TaskInProgress>();
        private class TaskInProgress
        {
            private readonly Tile tile;
            private readonly string name;
            private object action;

            private TaskInProgress(Tile tile, string name)
            {
                this.tile = tile;
                this.name = name;
            }

            public override string ToString()
            {
                return $"{{Task:{name} at {tile.Index}}}";
            }

            public static Action Start(Tile tile, Action action, string name)
            {
                var task = new TaskInProgress(tile, name);
                lock (tile.tasksInProgess)
                {
                    tile.tasksInProgess.Add(task);
                }
                task.action = action;
                return () =>
                {
                    action();

                    lock (tile.tasksInProgess)
                    {
                        tile.tasksInProgess.Remove(task);
                    }
                };
            }

            public static Action<T> Start<T>(Tile tile, Action<T> action, string name)
            {
                var task = new TaskInProgress(tile, name);
                lock (tile.tasksInProgess)
                {
                    tile.tasksInProgess.Add(task);
                }
                task.action = action;
                return x =>
                {
                    action(x);

                    lock (tile.tasksInProgess)
                    {
                        tile.tasksInProgess.Remove(task);
                    }
                };
            }
        }
    }
}