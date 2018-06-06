using System;
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
            using (VoxelGame game = new VoxelGame())
            {
                game.Run();
            }
        }
    }
}

