using System.Collections.Generic;

namespace VoxelWorldEngine.Rendering
{
    public interface IRenderableProvider
    {
        IEnumerable<IRenderable> GetRenderables();
    }
}