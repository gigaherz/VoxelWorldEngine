using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Grid : DrawableGameComponent
    {
        public static int MaxTilesInProgress = PriorityScheduler.Instance.MaximumConcurrencyLevel * 2;
        public static int MaxTilesQueued = MaxTilesInProgress;
        public static int LoadRange = 18;
        public static int UnloadRange = 32;

        private readonly OcTree<Tile> _tiles = new OcTree<Tile>();

        private readonly List<Tile> _pendingTiles = new List<Tile>();

        private readonly ReaderWriterLockSlim _tilesLock = new ReaderWriterLockSlim();
        public HashSet<Tile> UnorderedTiles { get; } = new HashSet<Tile>();

        public int PendingTiles => _pendingTiles.Count;

        public int TilesInProgress { get; set; }

        public Vector3I SpawnPosition { get; set; }

        public Random Random { get; }

        public GenerationContext GenerationContext { get; } = new GenerationContext();

        public Grid(Game game)
            : base(game)
        {
            Random = new Random(GenerationContext.Seed);
        }

        Vector3D _lastPlayerPosition;

        int before = Environment.TickCount;

        bool bootstrap = true;
        public void SetPlayerPosition(Vector3D newPosition)
        {
            var difference = newPosition - _lastPlayerPosition;
            var distance = difference.Magnitude;

            int tx = (int)Math.Floor(newPosition.X / Tile.SizeXZ);
            int ty = (int)Math.Floor(newPosition.Y / Tile.SizeY);
            int tz = (int)Math.Floor(newPosition.Z / Tile.SizeXZ);
            var tp = new Vector3I(tx,ty,tz);
            /*if (!ChunkExists(newPosition))
            {
                bootstrap = true;
            }*/

            if (/*Environment.TickCount - before > 1000 && (distance > 5 || _pendingTiles.Count == 0 ||*/( bootstrap))
            {
                Debug.WriteLine("Re-computing visible chunks...");

                before = Environment.TickCount; 

                bootstrap = false;

                List<Tile> ul;

                _tilesLock.EnterReadLock();
                try
                {
                    ul = UnorderedTiles.Where(tile => (tile.Centroid - newPosition).Magnitude > UnloadRange * Tile.SizeXZ).ToList();
                }
                finally
                {
                    _tilesLock.ExitReadLock();
                }

                foreach (var tile in ul)
                {
                    SetTile(tile.IndexX, tile.IndexY, tile.IndexZ, null);
                }
                
                var fOffY = GenerationContext.Floor / Tile.SizeY - tp.Y;
                var cOffY = GenerationContext.Ceiling / Tile.SizeY - tp.Y;
                for (int r = 0; r < LoadRange; r++)
                {
                    for (int z = -r; z <= r; z++)
                    {
                        for (int x = -r; x <= r; x++)
                        {
                            for (int y = Math.Max(fOffY, -r * Tile.SizeY / Tile.SizeXZ); y < Math.Min(cOffY, r * Tile.SizeY / Tile.SizeXZ); y++)
                            {
                                if (!(Math.Sqrt(x*x + y*y + z*z) <= LoadRange))
                                    continue;

                                int tpx = tp.X + x;
                                int tpy = tp.Y + y;
                                int tpz = tp.Z + z;

                                Tile tile;
                                if (Find(tpx, tpy, tpz, out tile))
                                    continue;

                                tile = new Tile(this, tpx, tpy, tpz);
                                        
                                SetTile(tpx, tpy, tpz, tile);
                                _pendingTiles.Add(tile);
                            }
                        }
                    }
                    //if (_pendingTiles.Count >= MaxTilesQueued)
                    //    break;
                }

                ResortPending(newPosition);

                _lastPlayerPosition = newPosition;
            }
        }

        private void ResortPending(Vector3D newPosition)
        {
            Func<Vector3D, double> valueFunc = a => (a - newPosition).SqrMagnitude;
            _pendingTiles.Sort((a, b) => Math.Sign(valueFunc(b.Centroid) - valueFunc(a.Centroid)));
        }

        private void SetTile(int x, int y, int z, Tile tile)
        {
            var previous = _tiles.SetValue(x, y, z, tile);
            if (previous != tile)
            {
                _tilesLock.EnterWriteLock();
                try
                {
                    if (previous != null)
                        UnorderedTiles.Remove(previous);
                    if (tile != null)
                        UnorderedTiles.Add(tile);
                }
                finally
                {
                    _tilesLock.ExitWriteLock();
                }
            }
        }

        public bool Find(int x, int y, int z, out Tile tile)
        {
            return _tiles.TryGetValue(x, y, z, out tile);
        }

        public bool Require(int x, int y, int z, out Tile tile)
        {
            tile = null;
            if (y < (GenerationContext.Floor / Tile.SizeY) || y >= (GenerationContext.Ceiling / Tile.SizeY))
                return false;
            if (!Find(x, y, z, out tile))
            {
                tile = new Tile(this, x, y, z);
                SetTile(x, y, z, tile);
            }
            ForceTile(tile);
            return true;
        }

        //Tile lastQueried;
        private Tile GetTileCoords(ref int x, ref int y, ref int z)
        {
            int px = (int)Math.Floor(x / (double)Tile.SizeXZ);
            int py = (int)Math.Floor(y / (double)Tile.SizeY);
            int pz = (int)Math.Floor(z / (double)Tile.SizeXZ);

            /*var tile = lastQueried;
            if (tile != null &&
                tile.IndexX == px &&
                tile.IndexY == py &&
                tile.IndexZ == pz)
            {
                Debug.Assert(x - tile.OffX >= 0 && x - tile.OffX < Tile.SizeXZ);
                Debug.Assert(y - tile.OffY >= 0 && y - tile.OffY < Tile.SizeY);
                Debug.Assert(z - tile.OffZ >= 0 && z - tile.OffZ < Tile.SizeXZ);
                x -= tile.OffX;
                y -= tile.OffY;
                z -= tile.OffZ;
                return tile;
            }*/

            Tile tile;

            if (Find(px, py, pz, out tile))
            {
                x -= tile.OffX;
                y -= tile.OffY;
                z -= tile.OffZ;
                Debug.Assert(x >= 0 && x < Tile.SizeXZ);
                Debug.Assert(y >= 0 && y < Tile.SizeY);
                Debug.Assert(z >= 0 && z < Tile.SizeXZ);
                //Interlocked.Exchange(ref lastQueried, tile);
                return tile;
            }

            return null;
        }

        public int GetSolidTop(int x, int y, int z)
        {
            // TODO: Support more than one tile in height!

            var tile = GetTileCoords(ref x, ref y, ref z);
            if (tile != null)
            {
                int top = tile.GetSolidTop(x, z);

                while(top == (Tile.SizeY-1))
                {
                    if (!Find(x, ++y, z, out tile))
                        return top;

                    top = tile.GetSolidTop(x, z);
                }
            }
            return -1;
        }

        public Block GetBlock(int x, int y, int z, bool load = true)
        {
            var tile = GetTileCoords(ref x, ref y, ref z);
            return tile?.GetBlock(x,y,z) ?? Block.Air;
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            var tile = GetTileCoords(ref x, ref y, ref z);
            tile?.SetBlock(x, y, z, block);
        }
        
        public int FindGround(int x, int y, int z)
        {
            if (GetBlock(x, y, z).PhysicsMaterial.IsSolid)
            {
                while (GetBlock(x, y + 1, z).PhysicsMaterial.IsSolid)
                    y++;
            }
            else
            {
                while (y > GenerationContext.Floor && !GetBlock(x, y, z).PhysicsMaterial.IsSolid)
                    y--;
            }
            return y;
        }

        internal void FindSpawnPosition()
        {
            int x = 0;
            int y = 0;
            int z = 0;
            int range = 100;

            do
            {
                var a = Random.NextDouble() * Math.PI * 2;
                x = (int)(range * Math.Cos(a));
                z = (int)(range * Math.Sin(a));
                y = GenerationContext.WaterLevel;

                if (GenerationContext.GetDensityAt(x, y, z) < 0)
                {
                    range += 5;
                    // try again
                    continue;
                }

                while (y < GenerationContext.Ceiling)
                {
                    if (GenerationContext.GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GenerationContext.GetDensityAt(x, y++, z) < 0)
                        break;
                }

                if (y < GenerationContext.Ceiling)
                    break;

                range += 5;
                // try again

            } while (true);

            SpawnPosition = new Vector3I(x,y-4,z);
        }

        private void ForceTile(Tile tile)
        {
            if (!tile.Initialized)
                tile.Initialize();
        }

        public bool ChunkExists(Vector3D playerPosition)
        {
            int px = (int)Math.Floor(playerPosition.X / Tile.SizeXZ);
            int py = (int)Math.Floor(playerPosition.Y / Tile.SizeY);
            int pz = (int)Math.Floor(playerPosition.Z / Tile.SizeXZ);

            Tile tile;
            return Find(px, py, pz, out tile);
        }

        public void QueueTile(Tile tile)
        {
            _pendingTiles.Add(tile);
        }

        public override void Initialize()
        {
            base.Initialize();

            GenerationContext.Initialize();

            FindSpawnPosition();
        }

        public override void Update(GameTime gameTime)
        {
            while (TilesInProgress < 10 && _pendingTiles.Count > 0)
            {
                var tile = _pendingTiles[_pendingTiles.Count-1];
                _pendingTiles.RemoveAt(_pendingTiles.Count-1);

                ForceTile(tile);
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

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            foreach (var queue in RegistryManager.GetRegistry<RenderQueue>().Values)
            {
                var vgame = (VoxelGame)Game;

                foreach (var pass in vgame.TerrainDrawEffect.Techniques[0].Passes)
                {
                    pass.Apply();
                    GraphicsDevice.Textures[0] = vgame.TerrainTexture;
                    GraphicsDevice.BlendState = queue.BlendState;
                    GraphicsDevice.RasterizerState = queue.RasterizerState;
                    GraphicsDevice.DepthStencilState = queue.DepthStencilState;

                    _tilesLock.EnterReadLock();
                    try
                    {
                        foreach (var tile in UnorderedTiles)
                        {
                            tile.Graphics.Draw(gameTime, queue);
                        }
                    }
                    finally
                    {
                        _tilesLock.ExitReadLock();
                    }
                }

            }

            base.Draw(gameTime);
        }
    }
}
