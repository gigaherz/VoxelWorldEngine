using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Grid : DrawableGameComponent
    {
        public static int MaxTilesInProgress = PriorityScheduler.Instance.MaximumConcurrencyLevel * 2;
        public static int LoadRange = 32;
        public static int UnloadRange = 32;

        private readonly OcTree<Tile> _tiles = new OcTree<Tile>();

        private readonly Queue<Tile> _pendingTiles = new Queue<Tile>();

        private readonly ReaderWriterLockSlim _tilesLock = new ReaderWriterLockSlim();
        public HashSet<Tile> UnorderedTiles { get; } = new HashSet<Tile>();

        public int PendingTiles => _pendingTiles.Count;

        public int Seed { get; } = (int)DateTime.UtcNow.Ticks;
        public int TilesInProgress { get; set; }

        public Vector3I SpawnPosition { get; set; }

        public Simplex PerlinDensity { get; }
        public Simplex PerlinHeight { get; }
        public Simplex PerlinRoughness { get; }
        public Random Random { get; }

        public Grid(Game game)
            : base(game)
        {
            PerlinDensity = new Simplex(Seed);
            PerlinHeight = new Simplex(Seed * 3);
            PerlinRoughness = new Simplex(Seed * 5);
            Random = new Random(Seed);
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
            if (!ChunkExists(newPosition))
            {
                bootstrap = true;
            }

            if ((distance > 5 && (Environment.TickCount - before) > 1000) || bootstrap)
            {
                before = Environment.TickCount; 

                bootstrap = false;

                List<Tile> ul = _pendingTiles.ToList();
                _pendingTiles.Clear();

                _tilesLock.EnterReadLock();
                try
                {
                    ul.AddRange(UnorderedTiles.Where(tile => (tile.Centroid - newPosition).Magnitude > UnloadRange * Tile.SizeXZ));
                }
                finally
                {
                    _tilesLock.ExitReadLock();
                }

                foreach (var tile in ul)
                {
                    SetTile(tile.IndexX, tile.IndexY, tile.IndexZ, null);
                }

                var l = new List<Vector3I>();
                var fOffY = Tile.Floor / Tile.SizeY - tp.Y;
                var cOffY = Tile.Ceiling / Tile.SizeY - tp.Y;
                for (int r = 0; r < LoadRange; r++)
                {
                    for (int z = -r; z <= r; z++)
                    {
                        for (int x = -r; x <= r; x++)
                        {
                            for (int y = Math.Max(fOffY, -r * Tile.SizeY / Tile.SizeXZ); y < Math.Min(cOffY, r * Tile.SizeY / Tile.SizeXZ); y++)
                            {
                                if (Math.Sqrt(x * x + y * y + z * z) <= LoadRange)
                                {
                                    Tile t;
                                    if (!Find(tp.X + x, tp.Y + y, tp.Z + z, out t))
                                        l.Add(new Vector3I(tp.X + x, tp.Y + y, tp.Z + z));
                                }
                            }
                        }
                    }
                    if (l.Count > MaxTilesInProgress)
                        break;
                }

                var cen = new Vector3D(Tile.SizeXZ / 2.0f, Tile.SizeY / 2.0f, Tile.SizeXZ / 2.0f);
                var size = new Vector3D(Tile.SizeXZ, Tile.SizeY, Tile.SizeXZ);
                Func<Vector3I,double> valueFunc = a => (a * size + cen - newPosition).SqrMagnitude;
                l.Sort((a, b) => Math.Sign(valueFunc(a) - valueFunc(b)));
                for (int i = 0; i < l.Count && TilesInProgress < MaxTilesInProgress; i++)
                {
                    var vec = l[i];
                    int x = vec.X;
                    int y = vec.Y;
                    int z = vec.Z;
                    var tile = new Tile(this, x, y, z);

                    Tile tile2;
                    if (!Find(x, y, z, out tile2))
                    {
                        SetTile(x, y, z, tile);
                        //ForceTile(tile);
                        _pendingTiles.Enqueue(tile);
                    }
                }
                _lastPlayerPosition = newPosition;
            }
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
            if (y < (Tile.Floor / Tile.SizeY) || y >= (Tile.Ceiling / Tile.SizeY))
                return false;
            if (!Find(x, y, z, out tile))
            {
                tile = new Tile(this, x, y, z);
                SetTile(x, y, z, tile);
            }
            ForceTile(tile);
            return true;
        }

        Tile lastQueried;
        private Tile GetTileCoords(ref int x, ref int y, ref int z)
        {
            int px = (int)Math.Floor(x / (double)Tile.SizeXZ);
            int py = (int)Math.Floor(y / (double)Tile.SizeY);
            int pz = (int)Math.Floor(z / (double)Tile.SizeXZ);

            var tile = lastQueried;
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
            }

            if (Find(px, py, pz, out tile))
            {
                x -= tile.OffX;
                y -= tile.OffY;
                z -= tile.OffZ;
                Debug.Assert(x >= 0 && x < Tile.SizeXZ);
                Debug.Assert(y >= 0 && y < Tile.SizeY);
                Debug.Assert(z >= 0 && z < Tile.SizeXZ);
                Interlocked.Exchange(ref lastQueried, tile);
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
                while (y > Tile.Floor && !GetBlock(x, y, z).PhysicsMaterial.IsSolid)
                    y--;
            }
            return y;
        }

        internal double GetDensityAt(int px, int py, int pz)
        {
            var nx = (px + 0.5) / 128;
            var ny = (py + 0.5) / 256;
            var nz = (pz + 0.5) / 128;

            var hx = nx / 2;
            var hz = nz / 2;

            var rx = nx / 3;
            var rz = nz / 3;

            var ph = PerlinHeight.Noise(hx, hz, 2);
            var heightChange = ExpectedHeight(ph);

            var rh = PerlinRoughness.Noise(rx, rz, 2);
            var roughness = ExpectedHeight(rh, 0.45, 3);

            var baseHeight = Tile.Average + Tile.Range * 0.15 * heightChange;

            var bottom = baseHeight - Tile.Range;
            var top = baseHeight + Tile.Range;

            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (py - bottom) / (top - bottom)));

            var noise = heightChange > 0.5 ?
                PerlinDensity.Noise(nx, ny, nz, 5, 1.7) :
                PerlinDensity.Noise(nx, ny, nz, 4);

            return 0.1f * roughness * noise + baseDensity;
        }

        private double ExpectedHeight(double initial)
        {//1.3*x^5 - 0.55*x^3 + 0.15*x
            double powered3 = Math.Pow(initial, 3);
            double powered5 = Math.Pow(initial, 5);
            double powered = 1.3 * powered5 - 0.55 * powered3 + 0.15 * initial;
            return powered;
        }

        private double ExpectedHeight(double initial, double factor, int power)
        {//1.3*x^5 - 0.55*x^3 + 0.15*x
            double powered = Math.Pow(initial, power);
            return initial + factor * (powered - initial);
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
                y = Tile.WaterLevel;

                if (GetDensityAt(x, y, z) < 0)
                {
                    range += 5;
                    // try again
                    continue;
                }

                while (y < Tile.Ceiling)
                {
                    if (GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GetDensityAt(x, y++, z) >= 0)
                    {
                        continue;
                    }
                    if (GetDensityAt(x, y++, z) < 0)
                        break;
                }

                if (y < Tile.Ceiling)
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

        public override void Initialize()
        {
            base.Initialize();

            PerlinDensity.Initialize();
            PerlinHeight.Initialize();
            PerlinRoughness.Initialize();

            FindSpawnPosition();
        }

        public override void Update(GameTime gameTime)
        {
            while (TilesInProgress < 10 && _pendingTiles.Count > 0)
            {
                var tile = _pendingTiles.Dequeue();

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
                var vgame = ((VoxelGame)Game);

                foreach (var pass in vgame.terrainDrawEffect.Techniques[0].Passes)
                {
                    pass.Apply();
                    GraphicsDevice.Textures[0] = vgame.terrainTexture;
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
            _pendingTiles.Enqueue(tile);
        }
    }
}
