using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Maths
{
    public struct BlockPos
    {
        public Vector3I Vec { get; }
        public int X => Vec.X;
        public int Y => Vec.Y;
        public int Z => Vec.Z;

        public BlockPos(Vector3I pos)
        {
            Vec = pos;
        }

        public BlockPos(int x, int y, int z)
        {
            Vec = new Vector3I(x,y,z);
        }

        internal (TilePos, Vector3I) Split()
        {
            var oo = Vec & (Tile.GridSize - 1);
            var pp = Vec - oo;

            return (new TilePos(pp / Tile.GridSize), oo);
        }

        public TilePos ToTilePos()
        {
            var (tilePos, _) = Split();
            return tilePos;
        }

        internal BlockPos Offset(Vector3I offset)
        {
            return new BlockPos(Vec + offset);
        }

        internal BlockPos Offset(int x, int y, int z)
        {
            return new BlockPos(Vec.Offset(x, y, z));
        }

        public static explicit operator Vector3I(BlockPos pos)
        {
            return pos.Vec;
        }

        public static bool operator ==(BlockPos a, BlockPos b)
        {
            return a.Vec == b.Vec;
        }

        public static bool operator !=(BlockPos a, BlockPos b)
        {
            return a.Vec != b.Vec;
        }

        public static BlockPos operator +(BlockPos a, BlockPos b)
        {
            return new BlockPos(a.Vec + b.Vec);
        }

        public static BlockPos operator -(BlockPos a, BlockPos b)
        {
            return new BlockPos(a.Vec - b.Vec);
        }

        public bool Equals(BlockPos other)
        {
            return Vec == other.Vec;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockPos))
                return false;
            return Equals((BlockPos)obj);
        }

        public override int GetHashCode()
        {
            return Vec.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{Block:{Vec}}}";
        }
    }

    public struct TilePos
    {
        public Vector3I Vec { get; }
        public int X => Vec.X;
        public int Y => Vec.Y;
        public int Z => Vec.Z;

        public TilePos(Vector3I pos)
        {
            Vec = pos;
        }

        public TilePos(int x, int y, int z)
        {
            Vec = new Vector3I(x, y, z);
        }

        public static explicit operator Vector3I(TilePos pos)
        {
            return pos.Vec;
        }

        internal BlockPos ToBlockPos()
        {
            return new BlockPos(Vec * Tile.GridSize);
        }

        internal BlockPos ToBlockPos(Vector3I offset)
        {
            return new BlockPos(Vec * Tile.GridSize + offset);
        }

        internal TilePos Offset(Vector3I offset)
        {
            return new TilePos(Vec + offset);
        }

        internal TilePos Offset(int x, int y, int z)
        {
            return new TilePos(Vec.Offset(x,y,z));
        }

        public static bool operator ==(TilePos a, TilePos b)
        {
            return a.Vec == b.Vec;
        }

        public static bool operator !=(TilePos a, TilePos b)
        {
            return a.Vec != b.Vec;
        }

        public static TilePos operator +(TilePos a, TilePos b)
        {
            return new TilePos(a.Vec + b.Vec);
        }

        public static TilePos operator -(TilePos a, TilePos b)
        {
            return new TilePos(a.Vec - b.Vec);
        }

        public bool Equals(TilePos other)
        {
            return Vec == other.Vec;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TilePos))
                return false;
            return Equals((TilePos)obj);
        }

        public override int GetHashCode()
        {
            return Vec.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{Tile:{Vec}}}";
        }
    }
}
