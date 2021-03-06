using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Storage;
using VoxelWorldEngine.Util;
using VoxelWorldEngine.Util.Performance;
using VoxelWorldEngine.Util.Scheduler;

namespace VoxelWorldEngine.Terrain
{
    public class Grid : GameComponent, IRenderableProvider
    {
        public static int TileInProgressThreshold = 100; //*/ PriorityScheduler.Instance.MaximumConcurrencyLevel * 10;
        public static int LoadRadius = 120;
        public static int UnloadRadius = 240;
        public static int SpawnChunksRange = 200 / Tile.GridSize.X;

        public struct PendingContext {
            public readonly GenerationStage stage;
            public readonly Tile tile;
            public PendingContext(GenerationStage stage, Tile tile)
            {
                this.stage = stage;
                this.tile = tile;
            }
        }

        private readonly Dictionary<Tile, GenerationStage> _pendingTilesSet = new Dictionary<Tile, GenerationStage>();
        private readonly List<Tile> _pendingTilesList = new List<Tile>();
        private readonly HashSet<Tile> _inProgressTilesSet = new HashSet<Tile>();

        private readonly ThreadSafeTileAccess _tileManager;

        public int PendingTiles => _pendingTilesList.Count;
        public int InProgressTiles => _inProgressTilesSet.Count;
        public int UpdateTaskCount { get; private set; }

        public EntityPosition SpawnPosition { get; set; }

        public Random Random { get; }

        public GenerationContext GenerationContext { get; } = new GenerationContext(
            new GenerationSettings("COHERENT-fr".GetHashCode())
            //new GenerationSettings((int)(DateTime.UtcNow.Ticks % 2147483647))
            );

        private CrappyChunkStorage _storage;

        public int saveInitializationPhase = -1; // phase 0 = generating spawn chunks. phase 1 = runtime

        public Grid(Game game)
            : base(game)
        {
            Random = new Random(GenerationContext.Seed);
            _tileManager = new ThreadSafeTileAccess(this);
            _storage = new CrappyChunkStorage() {
                SaveFolder = new DirectoryInfo(@"D:\Projects\VoxelWorldEngine.MonoGame\Saves\" + GenerationContext.Seed)
            };
            _storage.SaveFolder.Create();
        }

        EntityPosition _lastPlayerPosition;

        int before = Environment.TickCount;

        readonly ConcurrentQueue<Tile> pendingSave = new ConcurrentQueue<Tile>();

        bool bootstrap = true;
        List<TilePos> tempTiles = new List<TilePos>();
        public void SetPlayerPosition(EntityPosition newPosition)
        {
            if (saveInitializationPhase < 1)
                return;

            using (Profiler.CurrentProfiler.Begin("Updating Player Visibility"))
            {
                var difference = newPosition.RelativeTo(_lastPlayerPosition);
                var distance = difference.Length();

                var tp = newPosition.BasePosition;
                if (!TileExists(newPosition))
                {
                    bootstrap = true;
                }

                if (Environment.TickCount - before <= 1000 || (distance <= 5 && !bootstrap))
                    return;

                using (Profiler.CurrentProfiler.Begin("Computing Visible Tiles"))
                {
                    Debug.WriteLine("Re-computing visible tiles...");

                    before = Environment.TickCount;

                    bootstrap = false;

                    tempTiles.Clear();

                    _tileManager.AccessUnordered(unorderedTile =>
                    {
                        tempTiles.AddRange(unorderedTile.Where(tile => Distance(newPosition, tile) > UnloadRadius).Select(t => t.Index));
                    });
                }

                using (Profiler.CurrentProfiler.Begin("Discarding Inactive Tiles"))
                {
                    Debug.WriteLine($"Discarding {tempTiles.Count} tiles...");
                    _tileManager.RemoveTiles(tempTiles);
                }

                using (Profiler.CurrentProfiler.Begin("Loading Visible Tiles"))
                {
                    lock (_pendingTilesList)
                    {
                        tempTiles.Clear();
                        //_pendingTilesSet.Clear();
                        //_pendingTilesList.Clear();

                        //for (int r = 0; r < LoadRange; r++)
                        var rX = LoadRadius / Tile.RealSize.X;
                        var rY = LoadRadius / Tile.RealSize.Y;
                        var rZ = LoadRadius / Tile.RealSize.Z;
                        {
                            for (int z = -rZ; z <= rZ; z++)
                            {
                                for (int x = -rX; x <= rX; x++)
                                {
                                    for (int y = -rY; y <= rY; y++)
                                    {
                                        if (!((new Vector3D(x, y, z) * Tile.RealSize).Length() <= LoadRadius))
                                            continue;

                                        var tpo = tp.Offset(x, y, z);
                                        if (tempTiles.Contains(tpo))
                                            continue;

                                        Request(tpo, GenerationStage.Completed, "Player Nearby", (b,t) => {});
                                    }
                                }
                            }
                            //if (_pendingTiles.Count >= MaxTilesQueued)
                            //    break;
                        }

                        _lastPlayerPosition = newPosition;

                        ResortPending();
                    }
                }
            }
        }

        public bool GetTileIfExists(TilePos index, out Tile tile)
        {
            return _tileManager.GetTileIfExists(index, out tile);
        }

        public bool Request(TilePos index, GenerationStage stage, string reason, Action<bool, Tile> action
                         /*    ,   [CallerMemberName] string memberName = "",
                                [CallerFilePath] string sourceFilePath = "",
                                [CallerLineNumber] int sourceLineNumber = 0*/)
        {
            using (Profiler.CurrentProfiler.Begin("Requesting Tile Stage"))
            {
                //Debug.WriteLine($"Requested Tile {index} stage {stage} because: {reason}");
                return Load(index, (wasImmediate0, tile) =>
                {
                    tile.ScheduleOnUpdate(wasImmediate1 =>
                    {
                        if (tile.IsAtPhase(stage))
                        {
                            action(wasImmediate0 && wasImmediate1, tile);
                        }
                        else
                        {
                            tile.InvokeWhenCompleted(stage, b => action(b && wasImmediate0 && wasImmediate1, tile), $"awaiting stage {stage}");// from {memberName} at {sourceFilePath}:{sourceLineNumber}");
                            QueueTile(tile, stage);
                        }
                    }, "running task in-thread");
                });
            }
        }

        public bool Load(TilePos index, Action<bool, Tile> action)
        {
            var (existed, tile) = _tileManager.GetOrCreateTile(index);
            if (existed)
            {
                action(true, tile);
                return true;
            }

            PriorityScheduler.Schedule(() =>
            {
                using (Profiler.CurrentProfiler.Begin("Loading Tile From Disk"))
                {
                    _storage.TryLoadTile(tile, loaded =>
                    {
                        action(false,tile);
                    });
                }
            }, PriorityClass.Average, -1);
            return false;
        }

        private static float Distance(EntityPosition newPosition, Tile tile)
        {
            return (tile.Centroid.RelativeTo(newPosition)).Length();
        }

        private double RelativeToPlayer(EntityPosition centroid)
        {
            return _lastPlayerPosition.RelativeTo(centroid).LengthSquared();
        }

        private void ResortPending()
        {
            _pendingTilesList.Sort((a, b) => Math.Sign(RelativeToPlayer(b.Centroid) - RelativeToPlayer(a.Centroid)));
        }


        public (Tile, Vector3I) GetTileCoords(BlockPos pos)
        {
            var (index, oo) = pos.Split();

            Tile tile;
            if (_tileManager.GetTileIfExists(index, out tile))
            {
                return (tile,oo);
            }

            return (null,oo);
        }

        public (Tile, Vector3I) GetOrCreateTile(BlockPos pos)
        {
            var (index, oo) = pos.Split();
            var (_, tile) = _tileManager.GetOrCreateTile(index);
            return (tile, oo);
        }

        public Tile GetOrCreateTile(TilePos index)
        {
            var (_, tile) = _tileManager.GetOrCreateTile(index);
            return tile;
        }

        public int GetSolidTop(BlockPos pos)
        {
            using (Profiler.CurrentProfiler.Begin("Finding Solid Top"))
            {
                var (tile,tc) = GetTileCoords(pos);
                if (tile != null)
                {
                    int top = tile.GetSolidTop(tc.X, tc.Z);

                    if (top >= Tile.GridSize.Y)
                    {
                        return GetSolidTop(tile.Offset.Offset(tc.X, top, tc.Y));
                    }
                    else if (top < 0)
                    {
                        return GetSolidTop(tile.Offset.Offset(tc.X, top, tc.Y));
                    }
                    else
                    {
                        return tile.Offset.Y + top;
                    }
                }
                return -1;
            }
        }

        public int GetSolidBottom(BlockPos pos)
        {
            using (Profiler.CurrentProfiler.Begin("Finding Solid Top"))
            {
                var (tile,tc) = GetTileCoords(pos);
                if (tile != null)
                {
                    int bottom = tile.GetSolidBottom(tc.X, tc.Z);

                    if (bottom >= Tile.GridSize.Y)
                    {
                        return GetSolidBottom(tile.Offset.Offset(tc.X, bottom, tc.Y));
                    }
                    else if (bottom < 0)
                    {
                        return GetSolidBottom(tile.Offset.Offset(tc.X, bottom, tc.Y));
                    }
                    else
                    {
                        return tile.Offset.Y + bottom;
                    }
                }
                return -1;
            }
        }

        public Block GetBlock(BlockPos pos, bool load = true)
        {
            var (tile,tc) = GetTileCoords(pos);
            return tile?.GetBlock(tc) ?? Block.Air;
        }

        public void SetBlock(BlockPos pos, Block block)
        {
            var (tile, tc) = GetTileCoords(pos);
            tile?.SetBlock(tc, block);
        }
        
        public int FindGround(BlockPos pos)
        {
            int seekLimit = 256;
            int y = 0;
            if (GetBlock(pos).PhysicsMaterial.IsSolid)
            {
                while (GetBlock(pos.Offset(0, y+1, 0)).PhysicsMaterial.IsSolid)
                    y++;
            }
            else
            {
                while (seekLimit-- > 0 && !GetBlock(pos.Offset(0, y, 0)).PhysicsMaterial.IsSolid)
                    y--;
            }
            return pos.Y+y;
        }

        internal void FindSpawnPosition()
        {
            using (Profiler.CurrentProfiler.Begin("Finding Spawn Position"))
            {
                int range = 100;
                int seekLimit = 256;
                int x, y, z;

                do
                {
                    var a = Random.NextDouble() * Math.PI * 2;

                    x = (int)(range * Math.Cos(a));
                    y = GenerationContext.WaterLevel;
                    z = (int)(range * Math.Sin(a));

                    var densityProvider = GenerationContext.DensityProvider;

                    if (densityProvider.Get(new Vector3I(x, y++, z)) < 0)
                    {
                        range += 5;
                        // try again
                        continue;
                    }

                    while (seekLimit-- > 0)
                    {
                        if (densityProvider.Get(new Vector3I(x, y++, z)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(new Vector3I(x, y++, z)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(new Vector3I(x, y++, z)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(new Vector3I(x, y++, z)) < 0)
                            break;
                    }

                    if (seekLimit <= 0)
                        break;

                    range += 5;
                    // try again

                } while (true);

                if (seekLimit <= 0)
                {
                    // TODO: Build spawn platform
                }

                SpawnPosition = EntityPosition.FromGrid(new BlockPos(x, y-3, z));
            }
        }

        public bool TileExists(EntityPosition playerPosition)
        {
            return _tileManager.GetTileIfExists(playerPosition.BasePosition, out var tile) && tile.GeneratingPhase >= GenerationStage.Unstarted;
        }

        private void QueueTile(Tile tile, GenerationStage targetStage)
        {
            lock (_pendingTilesList)
            {
                if (!_pendingTilesSet.TryGetValue(tile, out var currentStage))
                { 
                    _pendingTilesSet.Add(tile, targetStage);
                    _pendingTilesList.Add(tile);
                }
                else if (currentStage < targetStage)
                {
                    _pendingTilesSet[tile] = targetStage;
                }
            }
        }

        internal void ClearPending(Tile tile)
        {
            lock (_pendingTilesList)
            {
                _pendingTilesSet.Remove(tile);
                _pendingTilesList.Remove(tile);
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            GenerationContext.Initialize();

            FindSpawnPosition();
        }

        private List<Tile> tileList = new List<Tile>();

        DateTime previousUpdate = DateTime.Now;

        public override void Update(GameTime gameTime)
        {
            using (Profiler.CurrentProfiler.Begin("StartGeneratingSpawnChunks"))
            {
                if (saveInitializationPhase < 0)
                {
                    saveInitializationPhase = 0;
                    StartGeneratingSpawnChunks();
                }
            }

            var currentUpdate = DateTime.Now;
            if ((currentUpdate - previousUpdate).TotalSeconds < 0.05)
                return;
            previousUpdate = currentUpdate;

            QueuePendingTiles();

            if (saveInitializationPhase == 0)
            {
                if (_inProgressTilesSet.Count == 0 && _pendingTilesList.Count == 0 && PriorityScheduler.Instance.QueuedTaskCount == 0)
                {
                    saveInitializationPhase = 1;
                }
            }

            int taskCount = 0;

            using (Profiler.CurrentProfiler.Begin("Updating Tiles"))
            {
                tileList.Clear();
                _tileManager.AccessUnordered(unorderedTiles =>
                {
                    tileList.AddRange(unorderedTiles);
                    foreach (var tile in unorderedTiles)
                    {
                        taskCount += tile.UpdateTaskCount;
                    }
                });

                foreach (var tile in tileList)
                {
                    tile.Update(gameTime);
                }
            }

            UpdateTaskCount = taskCount;

            DoSave();

            base.Update(gameTime);
        }

        private void DoSave()
        {
            if (pendingSave.Count > 0 && !_storage.BusyWriting)
            {
                PriorityScheduler.Schedule(() =>
                {
                    using (Profiler.CurrentProfiler.Begin("Saving Tiles"))
                    {
                        lock (pendingSave)
                        {
                            List<Tile> tiles = new List<Tile>();
                            while (pendingSave.TryDequeue(out var d))
                            {
                                tiles.Add(d);
                            }
                            _storage.WriteTiles(tiles);
                        }
                    }
                }, PriorityClass.Low, 1);
            }
        }

        private void QueuePendingTiles()
        {
            using (Profiler.CurrentProfiler.Begin("Queuing Pending Tiles"))
            {
                lock (_pendingTilesList)
                {
                    for (int j = 0; j < 1 && InProgressTiles < TileInProgressThreshold && _pendingTilesList.Count > 0; j++)
                    {
                        Tile closestTile = null;
                        GenerationStage closestStage = GenerationStage.Unstarted;
                        double closestDistance = -1;
                        int closestIndex = -1;

                        for (int i = 0; i < _pendingTilesList.Count; i++)
                        {
                            var tile = _pendingTilesList[i];
                            var stage = _pendingTilesSet[tile];
                            var distance = tile.Centroid.RelativeTo(_lastPlayerPosition).Length();
                            if (closestTile == null || distance < closestDistance)
                            {
                                closestTile = tile;
                                closestStage = stage;
                                closestDistance = distance;
                                closestIndex = i;
                            }
                        }

                        if (closestTile != null && _pendingTilesList.Count > 0)
                        {
                            _pendingTilesList.RemoveAt(closestIndex);
                            _pendingTilesSet.Remove(closestTile);
                            closestTile.SetRequiredPhase(closestStage);
                            closestTile.ScheduleNextPhase(false);
                        }
                    }
                }
            }
        }

        private void StartGeneratingSpawnChunks()
        {
            lock (_pendingTilesList)
            {
                tempTiles.Clear();
                _pendingTilesSet.Clear();
                _pendingTilesList.Clear();

                var r = SpawnChunksRange/2;
                {
                    for (int z = -r; z <= r; z++)
                    {
                        for (int x = -r; x <= r; x++)
                        {
                            for (int y = -r; y <= r; y++)
                            {
                                var tpo = SpawnPosition.BasePosition.Offset(x, y, z);

                                Request(tpo, GenerationStage.Completed, "Spawn Chunk", (b,t) => { });
                            }
                        }
                    }
                }
            }
        }

        class QueueRenderable : IRenderable
        {
            private readonly RenderQueue queue;
            private readonly Grid grid;

            public QueueRenderable(Grid grid, RenderQueue queue)
            {
                this.queue = queue;
                this.grid = grid;
            }

            public void Draw(GameTime gameTime, BaseCamera camera)
            {
                camera.GraphicsDevice.BlendState = queue.BlendState;
                camera.GraphicsDevice.RasterizerState = queue.RasterizerState;
                camera.GraphicsDevice.DepthStencilState = queue.DepthStencilState;

                grid._tileManager.AccessUnordered(unorderedTiles =>
                {
                    foreach (var tile in unorderedTiles)
                    {
                        tile.Graphics.Draw(gameTime, camera, queue);
                    }
                });
            }
        }

        public IEnumerable<IRenderable> GetRenderables()
        {
            foreach (var queue in RegistryManager.GetRegistry<RenderQueue>().Values)
            {
                yield return new QueueRenderable(this, queue);
            }
        }

        public void TilePhaseChanged(Tile tile)
        {
            pendingSave.Enqueue(tile);
        }

        internal void SetInProgress(Tile tile)
        {
            lock(_inProgressTilesSet)
                _inProgressTilesSet.Add(tile);
            tile.LogTileMessage("In Progress");
        }

        internal void ClearInProgress(Tile tile)
        {
            lock (_inProgressTilesSet)
                _inProgressTilesSet.Remove(tile);
            tile.LogTileMessage("Not in Progress");
        }
    }
}
