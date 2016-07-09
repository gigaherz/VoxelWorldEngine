using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Grid : DrawableGameComponent
    {
        private readonly Dictionary<long, Dictionary<int, Tile>> _tiles = new Dictionary<long, Dictionary<int, Tile>>();
        private readonly List<Tile> _unorderedTiles = new List<Tile>();
        private readonly Queue<Tile> _pendingTiles = new Queue<Tile>();

        public List<Tile> UnorderedTiles => _unorderedTiles;

        public int Seed { get; } = (int)DateTime.UtcNow.Ticks;
        public int TilesInProgress { get; set; }

        public Grid(Game game)
            : base(game)
        {
            var l = new List<Vector3I>();
            int range = 32;
            for (int z = -range; z <= range; z++)
            {
                for (int x = -range; x <= range; x++)
                {
                    for (int y = 0; y < 256/Tile.SizeY; y++)
                    {
                        if (Math.Sqrt(x * x + z * z) <= range)
                            l.Add(new Vector3I(x,y,z));
                    }
                }
            }

            l.Sort((a,b) => Math.Sign(a.SqrMagnitude - b.SqrMagnitude));
            foreach (var vec in l)
            {
                int x = vec.X;
                int y = vec.Y;
                int z = vec.Z;
                var tile = new Tile(this, x, y, z);

                SetTile(x, y, z, tile);
                _unorderedTiles.Add(tile);
                _pendingTiles.Enqueue(tile);
            }
        }

        private void SetTile(int x, int y, int z, Tile tile)
        {
            long xz = ((long)x<<32) ^ z;

            Dictionary<int, Tile> yy;
            if (!_tiles.TryGetValue(xz, out yy))
            {
                yy = new Dictionary<int, Tile>();
                _tiles.Add(xz, yy);
            }
            
            yy[y] = tile;
        }

        public bool Find(int x, int y, int z, out Tile tile)
        {
            long xz = ((long)x<<32) ^ z;

            Dictionary<int, Tile> yy;
            if (!_tiles.TryGetValue(xz, out yy))
            {
                tile = null;
                return false;
            }
            
            return yy.TryGetValue(y, out tile);
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
                while (y > 0 && !GetBlock(x, y, z).PhysicsMaterial.IsSolid)
                    y--;
            }
            return y;
        }

        public void ForceTile(Tile tile)
        {
            tile.Initialize();

            TilesInProgress++;
        }

        public override void Update(GameTime gameTime)
        {
            while (TilesInProgress < 10 && _pendingTiles.Count > 0)
            {
                var tile = _pendingTiles.Dequeue();

                ForceTile(tile);
            }

            foreach (var tile in _unorderedTiles)
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
                    foreach (var tile in _unorderedTiles)
                    {
                        tile.Graphics.Draw(gameTime, queue);
                    }
                }

            }

            base.Draw(gameTime);
        }

        public void UpdatePlayerPosition(Vector3 playerPosition)
        {
        }
    }
}
