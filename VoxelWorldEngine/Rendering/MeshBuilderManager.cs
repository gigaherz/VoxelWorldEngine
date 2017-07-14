using System.Collections.Concurrent;
using System.Collections.Generic;
using VoxelWorldEngine.Objects;

namespace VoxelWorldEngine.Rendering
{
    internal class MeshBuilderManager
    {
        private static readonly ConcurrentQueue<MeshBuilder> BuilderPool = new ConcurrentQueue<MeshBuilder>();

        private static MeshBuilder Get()
        {
            MeshBuilder instance;
            return BuilderPool.TryDequeue(out instance) ? instance : new MeshBuilder();
        }

        public readonly Dictionary<RenderQueue, MeshBuilder> Collectors = new Dictionary<RenderQueue, MeshBuilder>();

        public MeshBuilder Get(RenderQueue queue)
        {
            MeshBuilder collector;
            if (!Collectors.TryGetValue(queue, out collector))
            {
                collector = Get();
                collector.Queue = queue;
                Collectors.Add(queue, collector);
            }
            return collector;
        }

        public void Clear()
        {
            foreach (var collector in Collectors.Values)
            {
                collector.Clear();
                BuilderPool.Enqueue(collector);
            }
            Collectors.Clear();
        }
    }
}