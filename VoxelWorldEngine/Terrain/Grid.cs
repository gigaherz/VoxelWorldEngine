using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;
using VoxelWorldEngine.Rendering;
using VoxelWorldEngine.Storage;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Grid : GameComponent, IRenderableProvider
    {
        public static int MaxTilesInProgress = PriorityScheduler.Instance.MaximumConcurrencyLevel * 2;
        public static int MaxTilesQueued = MaxTilesInProgress;
        public static int LoadRadius = 300;
        public static int UnloadRadius = 650;
        public static int LoadRange = LoadRadius / Tile.GridSize.X;
        public static int UnloadRange = UnloadRadius / Tile.GridSize.X;
        public static int SpawnChunksRange = 200 / Tile.GridSize.X;
        public static Vector3 LoadScale = new Vector3(1,1,1) / Tile.VoxelSize;

        private readonly CubeTree<Tile> _tiles = new CubeTree<Tile>();

        private readonly HashSet<Tile> _pendingTilesSet = new HashSet<Tile>();
        private readonly List<Tile> _pendingTilesList = new List<Tile>();

        private readonly ReaderWriterLockSlim _tilesLock = new ReaderWriterLockSlim();
        public HashSet<Tile> UnorderedTiles { get; } = new HashSet<Tile>();

        public int PendingTiles => _pendingTilesList.Count;

        private int _tilesInProgress;
        public int TilesInProgress => _tilesInProgress;

        public EntityPosition SpawnPosition { get; set; }

        public Random Random { get; }

        public GenerationContext GenerationContext { get; } = new GenerationContext();

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

            var difference = newPosition.RelativeTo(_lastPlayerPosition) * LoadScale;
            var distance = difference.Length();

            var tp = newPosition.BasePosition;
            if (!ChunkExists(newPosition))
            {
                bootstrap = true;
            }

            if (Environment.TickCount - before <= 1000 || (distance <= 5 && !bootstrap))
                return;

            Debug.WriteLine("Re-computing visible chunks...");

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

            Debug.WriteLine($"Discarding {tempTiles.Count} tiles...");
            foreach (var tile in tempTiles)
            {
                SetTile(tile, null);
            }

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

                                Tile tile;
                                if (!Find(tpo, out tile))
                                {
                                    tile = new Tile(this, tpo);

                                    SetTile(tpo, tile);

                                    PriorityScheduler.StartNew(() =>
                                    {
                                        _storage.TryLoadTile(tile, (loaded) =>
                                        {
                                            if (tile.GenerationPhase < 1)
                                                QueueTile(tile);
                                        });
                                    }, -1);
                                }
                                else
                                {
                                    if (tile.GenerationPhase < 1)
                                        QueueTile(tile);
                                }
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

        public bool Require(Vector3I index, int phase, out Tile tile)
        {
            if (!Find(index, out tile))
            {
                var tt = tile = new Tile(this, index);
                SetTile(index, tile);

                PriorityScheduler.StartNew(() => {
                    _storage.TryLoadTile(tt, (loaded) =>
                    {
                        ForceTile(tt);
                        tt.RequirePhase(phase);
                    });
                }, -1);

                return false;
            }

            ForceTile(tile);
            return tile.RequirePhase(phase);
        }

        private Tile GetTileCoords(ref Vector3I xyz)
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

        public int GetSolidTop(Vector3I xyz)
        {
            // TODO: Support more than one tile in height!

            var tile = GetTileCoords(ref xyz);
            if (tile != null)
            {
                int top = tile.GetSolidTop(xyz.X, xyz.Z);

                while(top < 0)
                {
                    xyz.Y--;
                    if (!Find(xyz, out tile))
                        return top;

                    top = tile.GetSolidTop(xyz.X, xyz.Z);
                }

                while(top == (Tile.GridSize.Y-1))
                {
                    xyz.Y++;
                    if (!Find(xyz, out tile))
                        return top;

                    top = tile.GetSolidTop(xyz.X, xyz.Z);
                }
            }
            return -1;
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

                GenerationContext.GetTopologyAt(xyz.XZ, out var roughness, out var bottom, out var top);

                if (GenerationContext.GetDensityAt(xyz, roughness, bottom, top) < 0)
                {
                    range += 5;
                    // try again
                    continue;
                }

                while (seekLimit-- > 0)
                {
                    if (GenerationContext.GetDensityAt(xyz.Postincrement(0, 1, 0), roughness, bottom, top) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(xyz.Postincrement(0, 1, 0), roughness, bottom, top) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(xyz.Postincrement(0, 1, 0), roughness, bottom, top) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(xyz.Postincrement(0, 1, 0), roughness, bottom, top) < 0)
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

            SpawnPosition = EntityPosition.FromGrid(xyz.Offset(0,-3,0));
        }

        private void ForceTile(Tile tile)
        {
            if (!tile.Initialized)
                tile.Initialize();
        }

        public bool ChunkExists(EntityPosition playerPosition)
        {
            Tile tile;
            return Find(playerPosition.BasePosition, out tile) && tile.GenerationPhase >= 0;
        }

        public void QueueTile(Tile tile)
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

            lock (_pendingTilesList)
            {
                if (saveInitializationPhase == 0)
                {
                    if (_pendingTilesList.Count == 0 && _tilesInProgress <= 0)
                    {
                        saveInitializationPhase = 1;
                    }
                }

                if (_tilesInProgress < 10 && _pendingTilesList.Count > 0)
                {
                    for (int i = 0; i < 10 && _pendingTilesList.Count > 0; i++)
                    {
                        var tile = _pendingTilesList[_pendingTilesList.Count - 1];
                        _pendingTilesList.RemoveAt(_pendingTilesList.Count - 1);
                        _pendingTilesSet.Remove(tile);
                        tile.RequirePhase(1);
                        ForceTile(tile);
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
                PriorityScheduler.StartNew(() => {
                    lock (pendingSave)
                    {
                        List<Tile> tiles = new List<Tile>();
                        while (pendingSave.TryDequeue(out var d))
                        {
                            tiles.Add(d);
                        }
                        _storage.WriteTiles(tiles);
                    }
                }, 1);
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

                                var tile = new Tile(this, tpo);
                                SetTile(tpo, tile);
                                QueueTile(tile);
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

        class GridTaskInProgress : TaskInProgress
        {
            private readonly Grid _grid;
            public GridTaskInProgress(string taskName, object owner, Grid grid)
                : base(taskName, owner)
            {
                _grid = grid;
            }

            public override bool End()
            {
                if (!base.End()) return false;
                _grid.EndProcess();
                return true;
            }
        }

        public TaskInProgress BeginProcess(string name, object owner)
        {
            Interlocked.Increment(ref _tilesInProgress);
            return new GridTaskInProgress(name, owner, this);
        }

        private void EndProcess()
        {
            Interlocked.Decrement(ref _tilesInProgress);
        }
    }
}
