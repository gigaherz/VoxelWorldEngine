using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public static int TaskThrottleThreshold = PriorityScheduler.Instance.MaximumConcurrencyLevel * 5;
        public static int LoadRadius = 200;
        public static int UnloadRadius = 300;
        public static int LoadRange = LoadRadius / Tile.GridSize.X;
        public static int UnloadRange = UnloadRadius / Tile.GridSize.X;
        public static int SpawnChunksRange = 0; // 200 / Tile.GridSize.X;
        public static Vector3 LoadScale = new Vector3(1,1,1) / Tile.VoxelSize;

        private readonly CubeTree<Tile> _tiles = new CubeTree<Tile>();

        private readonly HashSet<Tile> _pendingTilesSet = new HashSet<Tile>();
        private readonly List<Tile> _pendingTilesList = new List<Tile>();

        private readonly ReaderWriterLockSlim _tilesLock = new ReaderWriterLockSlim();
        public HashSet<Tile> UnorderedTiles { get; } = new HashSet<Tile>();

        public int PendingTiles => _pendingTilesList.Count;

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
                var difference = newPosition.RelativeTo(_lastPlayerPosition) * LoadScale;
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

                    _tilesLock.EnterReadLock();
                    try
                    {
                        tempTiles.AddRange(UnorderedTiles.Where(tile => Distance(newPosition, tile) > UnloadRadius).Select(t => t.Index));
                    }
                    finally
                    {
                        _tilesLock.ExitReadLock();
                    }
                }

                using (Profiler.CurrentProfiler.Begin("Discarding Inactive Tiles"))
                {
                    Debug.WriteLine($"Discarding {tempTiles.Count} tiles...");
                    foreach (var tile in tempTiles)
                    {
                        SetTile(tile, null);
                    }
                }

                using (Profiler.CurrentProfiler.Begin("Loading Visible Tiles"))
                {
                    lock (_pendingTilesList)
                    {
                        tempTiles.Clear();
                        _pendingTilesSet.Clear();
                        _pendingTilesList.Clear();

                        //for (int r = 0; r < LoadRange; r++)
                        var r = LoadRange;
                        {
                            for (int z = -r; z <= r; z++)
                            {
                                for (int x = -r; x <= r; x++)
                                {
                                    for (int y = -r; y <= r; y++)
                                    {
                                        if (!((new Vector3D(x, y, z) * LoadScale).Length() <= LoadRange))
                                            continue;

                                        var tpo = tp.Offset(x, y, z);
                                        if (tempTiles.Contains(tpo))
                                            continue;

                                        Request(tpo, GenerationStage.Completed, "Player Nearby");
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

        public Task<Tile> Request(Vector3I index, GenerationStage stage, string reason)
        {
            using (Profiler.CurrentProfiler.Begin("Requesting Tile Stage"))
            {
                //Debug.WriteLine($"Requested Tile {index} stage {stage} because: {reason}");
                return Load(index).ContinueWith(task =>
                {
                    var tile = task.Result;
                    var t = new TaskCompletionSource<Tile>();
                    tile.ScheduleOnUpdate(()=> {
                        tile.RequirePhase(stage);
                        if (!tile.InvokeAfter(stage, () => t.TrySetResult(tile)))
                        {
                            QueueTile(tile);
                        }
                    });
                    return t.Task;
                }).Unwrap();
            }
        }

        private Tile CreateAndInitializeTile(Vector3I index)
        {
            var tile = new Tile(this, index);

            tile.Initialize();

            SetTile(index, tile);

            return tile;
        }

        public Task<Tile> Load(Vector3I index)
        {
            Tile tile;
            if (Find(index, out tile))
            {
                return Task.FromResult(tile);
            }

            tile = CreateAndInitializeTile(index);

            var t = new TaskCompletionSource<Tile>();
            PriorityScheduler.Schedule(() =>
            {
                using (Profiler.CurrentProfiler.Begin("Loading Tile From Disk"))
                {
                    _storage.TryLoadTile(tile, loaded =>
                    {
                        t.TrySetResult(tile);
                    });
                }
            }, PriorityClass.Average, -1);
            return t.Task;
        }

        private static float Distance(EntityPosition newPosition, Tile tile)
        {
            return (tile.Centroid.RelativeTo(newPosition) * LoadScale).Length();
        }

        private double RelativeToPlayer(EntityPosition centroid)
        {
            return _lastPlayerPosition.RelativeTo(centroid).LengthSquared();
        }

        private void ResortPending()
        {
            _pendingTilesList.Sort((a, b) => Math.Sign(RelativeToPlayer(b.Centroid) - RelativeToPlayer(a.Centroid)));
        }

        private void SetTile(Vector3I index, Tile tile)
        {
            _tilesLock.EnterWriteLock();
            try
            {
                var previous = _tiles.SetValue(index.X, index.Y, index.Z, tile);
                if (previous != tile)
                {
                    if (previous != null)
                    {
                        UnorderedTiles.Remove(previous);
                        previous.Dispose();
                    }
                    if (tile != null)
                    {
                        UnorderedTiles.Add(tile);
                    }
                }
            }
            finally
            {
                _tilesLock.ExitWriteLock();
            }
        }

        public bool Find(Vector3I pos, out Tile tile)
        {
            return _tiles.TryGetValue(pos.X, pos.Y, pos.Z, out tile);
        }

        public Tile GetTileCoords(ref Vector3I xyz)
        {
            var oo = xyz & (Tile.GridSize - 1);
            var pp = xyz - oo;

            var pxyz = pp / Tile.GridSize;

            Tile tile;
            if (Find(pxyz, out tile))
            {
                xyz = oo;
                return tile;
            }

            return null;
        }

        public Task<Tile> RequireTileCoords(Vector3I pos, GenerationStage stage, string reason)
        {
            return Request(BlockToTile(pos), stage, reason);
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
                        if (!Find(xyz, out tile))
                            return top;

                        top = tile.GetSolidTop(xyz.X, xyz.Z);
                    }

                    while (top == (Tile.GridSize.Y - 1))
                    {
                        xyz.Y++;
                        if (!Find(xyz, out tile))
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
            Tile tile;
            return Find(playerPosition.BasePosition, out tile) && tile.GenerationPhase >= 0;
        }

        private void QueueTile(Tile tile)
        {
            lock (_pendingTilesList)
            {
                if (!_pendingTilesSet.Contains(tile))
                {
                    _pendingTilesSet.Add(tile);
                    _pendingTilesList.Add(tile);
                }
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

                    while (PriorityScheduler.Instance.QueuedTaskCount < TaskThrottleThreshold && _pendingTilesList.Count > 0)
                    {
                        Tile closestTile = null;
                        double closestDistance = -1;
                        int closestIndex = -1;

                        for (int i = 0; i < _pendingTilesList.Count; )
                        {
                            var tile = _pendingTilesList[i];
                            var distance = tile.Centroid.RelativeTo(_lastPlayerPosition).Length();
                            if (closestTile == null || distance < closestDistance)
                            {
                                closestTile = tile;
                                closestDistance = distance;
                                closestIndex = i;
                            }
                            if (distance > UnloadRadius)
                            {
                                _pendingTilesList.RemoveAt(i);
                                _pendingTilesSet.Remove(tile);
                            }
                            else
                            {
                                i++;
                            }
                        }

                        if (closestTile != null && _pendingTilesList.Count > 0)
                        {
                            _pendingTilesList.RemoveAt(closestIndex);
                            _pendingTilesSet.Remove(closestTile);
                            closestTile.RequirePhase(closestTile.RequiredPhase);
                        }
                    }
                }
            }

            List<Tile> tileList;
            _tilesLock.EnterReadLock();
            try
            {
                tileList = UnorderedTiles.ToList();
            }
            finally
            {
                _tilesLock.ExitReadLock();
            }

            foreach (var tile in tileList)
            {
                tile.Update(gameTime);
            }

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

                                Request(tpo, GenerationStage.Completed, "Spawn Chunk");
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

                grid._tilesLock.EnterReadLock();
                try
                {
                    foreach (var tile in grid.UnorderedTiles)
                    {
                        tile.Graphics.Draw(gameTime, camera, queue);
                    }
                }
                finally
                {
                    grid._tilesLock.ExitReadLock();
                }
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
    }
}
