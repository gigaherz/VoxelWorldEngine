using System;
using System.IO;
using System.Text;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Storage
{
    class DataSystem
    {
        static readonly int SHIFT = 2;
        static readonly int SIZE = (1 << SHIFT);
        static readonly uint BlockSize = 4096;
        static readonly ulong Identifier = BitConverter.ToUInt64(Encoding.ASCII.GetBytes("VoxlStor"), 0);

        struct Location
        {
            ulong BlockNumber;
            ulong Offset;
        }

        struct Master
        {
            public ulong Identifier; // DataType for any other block
            public ulong Version; // Next for any other block
            public ulong Shift;
            public Location RootNode;
            public ulong FreeSpaceHead;
            public Location Metadata; // Usually included in the master block itself
        }

        struct BlockHeader
        {
            public ulong DataType;
            public ulong Next;
        }

        struct MetadataTable
        {
            ushort Entries;


        }

        struct Node
        {
            public ulong[] Nodes;

            public Node(int shift)
            {
                int size = 1 << shift;
                Nodes = new ulong[size*size*size];
            }
        }

        struct Leaf
        {

        }

        public static void SerializeData(string fileName, Grid grid)
        {
            // Implementation Phase 1: Write the entire grid to file, and then try to load it back!

            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                // Master block

            }
        }
    }
}
