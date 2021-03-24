using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    class ThreadSafeTileAccess
    {
        private readonly Grid _owner;

        private readonly ReaderWriterLockSlim _tilesLock = new ReaderWriterLockSlim();

        private readonly CubeTree<Tile> _tiles = new CubeTree<Tile>();
        private readonly HashSet<Tile> _unorderedTiles = new HashSet<Tile>();

        public ThreadSafeTileAccess(Grid grid)
        {
            _owner = grid;
        }

        /// <summary>
        /// Finds a tile, if exists. MUST BE CALLED WITH THE WRITE LOCK HELD!
        /// </summary>
        private Tile SetTileUnsafe(Vector3I index, Tile tile)
        {
            var previous = _tiles.SetValue(index.X, index.Y, index.Z, tile);
            if (previous != tile)
            {
                if (previous != null)
                {
                    _unorderedTiles.Remove(previous);
                    previous.Dispose();
                }
                if (tile != null)
                {
                    _unorderedTiles.Add(tile);
                }
            }
            return previous;
        }

        /// <summary>
        /// Finds a tile, if exists. MUST BE CALLED WITH THE READ LOCK HELD!
        /// </summary>
        private bool Find(Vector3I pos, out Tile tile)
        {
#if DEBUG
            if (!_tilesLock.IsReadLockHeld && !_tilesLock.IsUpgradeableReadLockHeld)
                throw new Exception("Unsafe call to Find!");
#endif
            return _tiles.TryGetValue(pos.X, pos.Y, pos.Z, out tile);
        }

        public (bool,Tile) GetOrCreateTile(Vector3I index)
        {
            _tilesLock.EnterUpgradeableReadLock();
            try
            {
                if (Find(index, out var tile))
                {
                    return (true,tile);
                }

                tile = new Tile(_owner, index);

                tile.Initialize();

                SetTile(index, tile);

                return (false,tile);
            }
            finally
            {
                _tilesLock.ExitUpgradeableReadLock();
            }
        }

        public Tile SetTile(Vector3I index, Tile tile)
        {
            _tilesLock.EnterWriteLock();
            try
            {
                return SetTileUnsafe(index, tile);
            }
            finally
            {
                _tilesLock.ExitWriteLock();
            }
        }

        public bool GetTileIfExists(Vector3I pos, out Tile tile)
        {
            _tilesLock.EnterReadLock();
            try
            {
                return Find(pos, out tile);
            }
            finally
            {
                _tilesLock.ExitReadLock();
            }
        }

        public void AccessUnordered(Action<IEnumerable<Tile>> action)
        {
            _tilesLock.EnterReadLock();
            try
            {
                action(_unorderedTiles);
            }
            finally
            {
                _tilesLock.ExitReadLock();
            }
        }

        public void RemoveTiles(List<Vector3I> tempTiles)
        {
            _tilesLock.EnterWriteLock();
            try
            {
                foreach(var tile in tempTiles)
                {
                    SetTileUnsafe(tile, null);
                }
            }
            finally
            {
                _tilesLock.ExitWriteLock();
            }
        }
    }
}
