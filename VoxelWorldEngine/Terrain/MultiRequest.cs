using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Terrain
{
    internal class MultiRequest
    {
        private ConcurrentQueue<Tile> tiles = new ConcurrentQueue<Tile>();
        private HashSet<TilePos> indices;
        private GenerationStage stage;
        private string message;
        private Action<bool, Tile[]> action;
        private volatile int remaining;
        private volatile bool allImmediate;

        public MultiRequest(HashSet<TilePos> tiles, GenerationStage stage, string v, Action<bool, Tile[]> p)
        {
            this.indices = tiles;
            this.stage = stage;
            this.message = v;
            this.action = p;
        }

        internal void Start(Grid parent)
        {
            remaining = indices.Count;
            foreach (var index in indices)
            {
                parent.Request(index, stage, $"{message} {index}", (b, t) =>
                {
                    if (!b) allImmediate = false;
                    tiles.Enqueue(t);
                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        action(allImmediate, tiles.ToArray());
                    }
                });
            }
        }
    }
}