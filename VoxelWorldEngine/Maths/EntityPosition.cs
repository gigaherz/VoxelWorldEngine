using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Maths
{
    public struct EntityPosition
    {
        public TilePos BasePosition;
        public Vector3 RelativePosition;

        private EntityPosition(TilePos basePos, Vector3 relativePos)
        {
            BasePosition = basePos;
            RelativePosition = relativePos;
        }

        public BlockPos GridPosition => BasePosition.ToBlockPos().Offset((Vector3I)(RelativePosition / Tile.VoxelSize).Floor());

        public Vector3 RelativeTo(EntityPosition other)
        {
            return GetBaseDifference(other) + RelativePosition - other.RelativePosition;
        }

        private Vector3I GetBaseDifference(EntityPosition other)
        {
            return (BasePosition - other.BasePosition).Vec * Tile.RealSize;
        }

        public static EntityPosition Create(TilePos basePosition, EntityPosition other)
        {
            var offset = basePosition - other.BasePosition;
            var relativePosition = other.RelativePosition - offset.Vec * Tile.RealSize;

            return new EntityPosition(basePosition, relativePosition);
        }

        public static EntityPosition Create(TilePos basePosition, Vector3 relativePosition)
        {
            var offset = (Vector3I)(relativePosition / Tile.RealSize).Floor();

            basePosition = basePosition.Offset(offset);
            relativePosition -= offset * Tile.RealSize;

            return new EntityPosition(basePosition, relativePosition);
        }

        public override string ToString()
        {
            return $"{BasePosition}+{RelativePosition}";
        }

        public static EntityPosition operator +(EntityPosition a, Vector3 b)
        {
            return Create(a.BasePosition, a.RelativePosition + b);
        }

        public static EntityPosition operator -(EntityPosition a, Vector3 b)
        {
            return Create(a.BasePosition, a.RelativePosition - b);
        }

        public static EntityPosition FromGrid(BlockPos xyz, Vector3 offset = new Vector3())
        {
            var (basePos, relativePos) = xyz.Split();
            return Create(basePos, relativePos * Tile.VoxelSize + offset);
        }
    }
}
