using System;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Maths
{
    public struct EntityPosition
    {
        public Vector3I BasePosition;
        public Vector3 RelativePosition;

        private EntityPosition(Vector3I basePos, Vector3 relativePos)
        {
            BasePosition = basePos;
            RelativePosition = relativePos;
        }

        public Vector3I GridPosition => BasePosition * Tile.GridSize + (Vector3I)(RelativePosition / Tile.VoxelSize).Floor();

        public Vector3 RelativeTo(EntityPosition other)
        {
            return GetBaseDifference(other) + RelativePosition - other.RelativePosition;
        }

        private Vector3I GetBaseDifference(EntityPosition other)
        {
            return (BasePosition - other.BasePosition) * Tile.RealSize;
        }

        public static EntityPosition Create(Vector3I basePosition, EntityPosition other)
        {
            var offset = basePosition - other.BasePosition;
            var relativePosition = other.RelativePosition - offset * Tile.RealSize;

            return new EntityPosition(basePosition, relativePosition);
        }

        public static EntityPosition Create(Vector3I basePosition, Vector3 relativePosition)
        {
            var offset = (Vector3I)(relativePosition / Tile.RealSize).Floor();

            basePosition += offset;
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

        public static EntityPosition FromGrid(Vector3I xyz, Vector3 offset = new Vector3())
        {
            var roundedPos = xyz - xyz & (Tile.GridSize - 1);

            var basePos = roundedPos / Tile.GridSize;
            var relativePos = (xyz - roundedPos) * Tile.VoxelSize;
            return Create(basePos, relativePos);
        }
    }
}
