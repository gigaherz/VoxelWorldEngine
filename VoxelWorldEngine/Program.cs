using System;
using System.Threading;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Thread.CurrentThread.Name = "Main Thread";
            using (VoxelGame game = new VoxelGame())
            {
                game.Run();
            }
        }
    }
}

