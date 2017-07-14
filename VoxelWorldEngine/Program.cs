using System;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (VoxelGame game = new VoxelGame())
            {
#if false//WINDOWS
                int coreCount = 0;
                foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }

                PriorityScheduler.Instance.MaximumConcurrencyLevel = coreCount;
#endif

                game.Run();
            }
        }
    }
#endif
}

