using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Terrain
{
    public class GridScanner
    {
        private Grid grid;
        private Tile tile;
        private TilePos idx;
        private int x;
        private int y;
        private int z;

        public int X => x + idx.X * Tile.GridSize.X;
        public int Y => y + idx.Y * Tile.GridSize.Y;
        public int Z => z + idx.Z * Tile.GridSize.Z;

        public GridScanner(Grid grid, BlockPos startPosition)
        {
            this.grid = grid;
            var (tilePos,offset) = startPosition.Split();
            idx = tilePos;
            x = offset.X;
            y = offset.Y;
            z = offset.Z;
            UpdateTile();
        }

        private void UpdateTile()
        {
            var tile = grid.GetOrCreateTile(idx);
            this.tile = tile;
        }

        public Block GetBlock()
        {
            return tile != null ? tile.GetBlock(x, y, z) : Block.Air;
        }

        public void XP()
        {
            x++;
            if (x >= Tile.GridSize.X)
            {
                x -= Tile.GridSize.X;
                idx = idx.Offset(1, 0, 0);
                UpdateTile();
            }
        }

        public void XN()
        {
            x--;
            if (x < 0)
            {
                x += Tile.GridSize.X;
                idx = idx.Offset(-1, 0, 0);
                UpdateTile();
            }
        }

        public void YP()
        {
            y++;
            if (y >= Tile.GridSize.Y)
            {
                y -= Tile.GridSize.Y;
                idx = idx.Offset(0, 1, 0);
                UpdateTile();
            }
        }

        public void YN()
        {
            y--;
            if (y < 0)
            {
                y += Tile.GridSize.Y;
                idx = idx.Offset(0, -1, 0);
                UpdateTile();
            }
        }

        public void ZP()
        {
            z++;
            if (z >= Tile.GridSize.Z)
            {
                z -= Tile.GridSize.Z;
                idx = idx.Offset(0, 0, 1);
                UpdateTile();
            }
        }

        public void ZN()
        {
            z--;
            if (z < 0)
            {
                z += Tile.GridSize.Z;
                idx = idx.Offset(0, 0, -1);
                UpdateTile();
            }
        }
    }
}
