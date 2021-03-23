using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelWorldEngine.Util.Performance
{
    public class DummyProfiler : IProfiler
    {
        public IProfilerFrame Begin(string node)
        {
            return DummyFrame.Instance;
        }

        public IProfilerFrame BeginThread()
        {
            return DummyFrame.Instance;
        }

        public void Close()
        {
            // Nothing to do.
        }

        public class DummyFrame : IProfilerFrame
        {
            public static readonly DummyFrame Instance = new DummyFrame();

            public void Dispose()
            {
                // Nothing to do
            }
        }
    }
}
