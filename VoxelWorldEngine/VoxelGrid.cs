using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine
{
    public class VoxelGrid : DrawableGameComponent
    {
        private readonly Dictionary<long, Dictionary<int, VoxelTile>> _tiles = new Dictionary<long, Dictionary<int, VoxelTile>>();
        private readonly List<VoxelTile> _unorderedTiles = new List<VoxelTile>();

        public int Seed { get; } = (int)DateTime.Now.Ticks;

        public VoxelGrid(Game game)
            : base(game)
        {
            int range = 12;
            for (int r = 0; r <= range; r++)
            {
                for (int z = -r; z <= r; z++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        for (int y = 0; y < (256/VoxelTile.TileSizeY); y++)
                        {
                            if (Math.Sqrt(x * x + z * z) <= range)
                                AddTile(x, y, z);
                        }
                    }
                }
            }
        }

        private void AddTile(int x, int y, int z)
        {
            VoxelTile tile;
            if (Find(x,y,z, out tile))
                return;

            tile = new VoxelTile(this, x, y, z);

            SetTile(x, y, z, tile);
            _unorderedTiles.Add(tile);
        }

        private void SetTile(int x, int y, int z, VoxelTile tile)
        {
            long xz = ((long)x<<32) ^ z;

            Dictionary<int, VoxelTile> yy;
            if (!_tiles.TryGetValue(xz, out yy))
            {
                yy = new Dictionary<int, VoxelTile>();
                _tiles.Add(xz, yy);
            }
            
            yy[y] = tile;
        }

        public bool Find(int x, int y, int z, out VoxelTile tile)
        {
            long xz = ((long)x<<32) ^ z;

            Dictionary<int, VoxelTile> yy;
            if (!_tiles.TryGetValue(xz, out yy))
            {
                tile = null;
                return false;
            }
            
            return yy.TryGetValue(y, out tile);
        }

        private VoxelTile GetTileCoords(ref int x, ref int y, ref int z)
        {
            int px = (int)Math.Floor(x / (double)VoxelTile.TileSizeX);
            int py = (int)Math.Floor(y / (double)VoxelTile.TileSizeY);
            int pz = (int)Math.Floor(z / (double)VoxelTile.TileSizeZ);

            VoxelTile tile;
            if (Find(px, py, pz, out tile))
            {
                x = x - px * VoxelTile.TileSizeX;
                y = y - py * VoxelTile.TileSizeY;
                z = z - pz * VoxelTile.TileSizeZ;
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

                while(top == (VoxelTile.TileSizeY-1))
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
            if (tile != null)
                return tile.GetBlock(x,y,z);
            return Block.Air;
        }

        public uint GetOcclusion(int x, int y, int z, bool load = true)
        {
            var tile = GetTileCoords(ref x, ref y, ref z);
            if (tile != null)
                return tile.GetOcclusion(x, y, z);
            return 0;
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

        public override void Initialize()
        {
            foreach (var tile in _unorderedTiles)
            {
                tile.Initialize();
            }

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
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
                Game.GraphicsDevice.BlendState = queue.BlendState;
                Game.GraphicsDevice.RasterizerState = queue.RasterizerState;

                foreach (var tile in _unorderedTiles)
                {
                    tile.Draw(gameTime, queue);
                }
            }

            base.Draw(gameTime);
        }
    }
}
