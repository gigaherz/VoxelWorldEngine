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
        public static int LoadRadius = 360;
        public static int UnloadRadius = 600;
        public static int SpawnChunksRange = 0; // 200 / Tile.GridSize.X;

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
            //new GenerationSettings("VoxelGame".GetHashCode())
            new GenerationSettings((int)(DateTime.UtcNow.Ticks % 2147483647))
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
        List<Vector3I> tempTiles = new List<Vector3I>();
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

        public bool GetTileIfExists(Vector3I index, out Tile tile)
        {
            return _tileManager.GetTileIfExists(index, out tile);
        }

        public bool Request(Vector3I index, GenerationStage stage, string reason, Action<bool, Tile> action
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

        public bool Load(Vector3I index, Action<bool, Tile> action)
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


        public Tile GetTileCoords(ref Vector3I xyz)
        {
            var oo = xyz & (Tile.GridSize - 1);
            var pp = xyz - oo;

            var pxyz = pp / Tile.GridSize;

            Tile tile;
            if (_tileManager.GetTileIfExists(pxyz, out tile))
            {
                xyz = oo;
                return tile;
            }

            return null;
        }

        public static Vector3I BlockToTile(Vector3I pos)
        {
            var oo = pos & (Tile.GridSize - 1);
            var pp = pos - oo;

            return pp / Tile.GridSize;
        }

        public int GetSolidTop(Vector3I xyz)
        {
            using (Profiler.CurrentProfiler.Begin("Finding Solid Top"))
            {
                // TODO: Support more than one tile in height!

                var tile = GetTileCoords(ref xyz);
                if (tile != null)
                {
                    int top = tile.GetSolidTop(xyz.X, xyz.Z);

                    while (top < 0)
                    {
                        xyz.Y--;
                        if (!_tileManager.GetTileIfExists(xyz, out tile))
                            return top;

                        top = tile.GetSolidTop(xyz.X, xyz.Z);
                    }

                    while (top == (Tile.GridSize.Y - 1))
                    {
                        xyz.Y++;
                        if (!_tileManager.GetTileIfExists(xyz, out tile))
                            return top;

                        top = tile.GetSolidTop(xyz.X, xyz.Z);
                    }
                }
                return -1;
            }
        }

        public Block GetBlock(Vector3I xyz, bool load = true)
        {
            var tile = GetTileCoords(ref xyz);
            return tile?.GetBlock(xyz) ?? Block.Air;
        }

        public void SetBlock(Vector3I xyz, Block block)
        {
            var tile = GetTileCoords(ref xyz);
            tile?.SetBlock(xyz, block);
        }
        
        public int FindGround(Vector3I xyz)
        {
            int seekLimit = 256;
            if (GetBlock(xyz).PhysicsMaterial.IsSolid)
            {
                while (GetBlock(xyz.Offset(0,1,0)).PhysicsMaterial.IsSolid)
                    xyz.Y++;
            }
            else
            {
                while (seekLimit-- > 0 && !GetBlock(xyz).PhysicsMaterial.IsSolid)
                    xyz.Y--;
            }
            return xyz.Y;
        }

        internal void FindSpawnPosition()
        {
            using (Profiler.CurrentProfiler.Begin("Finding Spawn Position"))
            {
                Vector3I xyz;
                int range = 100;
                int seekLimit = 256;

                do
                {
                    var a = Random.NextDouble() * Math.PI * 2;
                    xyz = new Vector3I(
                        (int)(range * Math.Cos(a)),
                        GenerationContext.WaterLevel,
                        (int)(range * Math.Sin(a)));

                    var oo = xyz & (Tile.GridSize - 1);
                    var pp = xyz - oo;

                    var densityProvider = GenerationContext.DensityProvider;

                    if (densityProvider.Get(xyz.Postincrement(0, 1, 0)) < 0)
                    {
                        range += 5;
                        // try again
                        continue;
                    }

                    while (seekLimit-- > 0)
                    {
                        if (densityProvider.Get(xyz.Postincrement(0, 1, 0)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(xyz.Postincrement(0, 1, 0)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(xyz.Postincrement(0, 1, 0)) >= 0)
                        {
                            continue;
                        }
                        if (densityProvider.Get(xyz.Postincrement(0, 1, 0)) < 0)
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

                SpawnPosition = EntityPosition.FromGrid(xyz.Offset(0, -3, 0));
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

        public override void Update(GameTime gameTime)
        {
            if (saveInitializationPhase < 0)
            {
                saveInitializationPhase = 0;
                StartGeneratingSpawnChunks();
            }

            using (Profiler.CurrentProfiler.Begin("Queuing Pending Tiles"))
            {
                lock (_pendingTilesList)
                {
                    if (saveInitializationPhase == 0)
                    {
                        if (_pendingTilesList.Count == 0 && PriorityScheduler.Instance.QueuedTaskCount == 0)
                        {
                            saveInitializationPhase = 1;
                        }
                    }

                    for (int j=0;j<1 && InProgressTiles < TileInProgressThreshold && _pendingTilesList.Count > 0; j++)
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
                            closestTile.ScheduleNextPhase();
                        }
                    }
                }
            }

            int taskCount = 0;

            using (Profiler.CurrentProfiler.Begin("Updating Tiles"))
            {
                List<Tile> tileList = new List<Tile>();
                //List<Tile> tileList2 = new List<Tile>();
                _tileManager.AccessUnordered(unorderedTiles =>
                {
                    foreach (var tile in unorderedTiles)
                    {
                        tileList.Add(tile);
                        //if (tile.UpdateTaskCount > 0) tileList2.Add(tile);
                        taskCount += tile.UpdateTaskCount;
                    }
                });

                /*bool b = false;
                if (b)
                {
                    int tc = 0;
                    int[] counts = new int[9];
                    foreach (var tile in tileList2)
                    {
                        for(int i=0;i<=8;i++)
                        {
                            var list = tile._pendingActions[i];
                            if (list.Count > 0)
                            {
                                counts[i]++;
                                tc++;
                            }
                        }
                    }
                    for (int i = 0; i <= 8; i++)
                    {
                        Debug.Write($"[{i}] = {counts[i]}; ");
                    }
                    Debug.WriteLine("");
                }*/

                foreach (var tile in tileList)
                {
                    tile.Update(gameTime);
                }
            }

            UpdateTaskCount = taskCount;

            if (pendingSave.Count > 0 && !_storage.BusyWriting)
            {
                PriorityScheduler.Schedule(() => {
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

            base.Update(gameTime);
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
