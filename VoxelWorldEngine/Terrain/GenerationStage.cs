using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelWorldEngine.Terrain
{
    public enum GenerationStage : int
    {
        Unstarted = -1,
        Density,
        Terrain,
        Carving,
        Surface,
        Structures,
        Features,
        Entities,
        Completed
    }
}
