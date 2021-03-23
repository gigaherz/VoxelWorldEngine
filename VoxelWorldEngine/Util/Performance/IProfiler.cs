using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelWorldEngine.Util.Performance
{

    public interface IProfiler
    {
        IProfilerFrame Begin(string node);

        /// <summary>
        /// Only call at the root of a new thread! If the root frame is ended prematurely, the profiler will be sad!
        /// </summary>
        /// <returns>The Root frame for the thread</returns>
        IProfilerFrame BeginThread();

        void Close();
    }
}
