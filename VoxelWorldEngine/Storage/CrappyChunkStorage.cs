using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using SharpRiff;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Terrain;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Storage
{
    public class CrappyChunkStorage
    {
        public static readonly int RegionSize = 16;

        public DirectoryInfo SaveFolder { get; set; }

        public bool BusyWriting { get; private set; }

        public void WriteTiles(List<Tile> tiles)
        {
            BusyWriting = true;
            foreach (var grouping in tiles.GroupBy(t => RegionIndex(t)))
            {
                var name = NameOf(grouping.Key);

                var regionFile = LoadRegionFile(name, grouping.Key);

                lock (regionFile)
                {
                    foreach (var tile in grouping)
                    {
                        regionFile.SetTile(tile);
                    }

                    WriteRegionFile(name, regionFile);
                }
            }
            BusyWriting = false;
        }

        private Vector3I RegionIndex(Tile tile)
        {
            return (tile.Index / (float)RegionSize).FloorToInt();
        }

        private void WriteRegionFile(string name, RegionData regionFile)
        {
            var formatter = new BinaryFormatter();
            var path = Path.Combine(SaveFolder.FullName, name);
            const string fileId = "VRGN";
            using (var mems = new MemoryStream())
            {
                using (var sw = new RiffFile(mems, fileId))
                {
                    // Region file meta
                    using (var meta = sw.CreateChunk("RMTA"))
                    {
                        // Data Version
                        meta.Write((byte)1);
                    }

                    // Tiles
                    using (var list = sw.CreateList("TILS"))
                    {
                        foreach (var tile in regionFile.tiles)
                        {
                            if (tile == null)
                                continue;

                            using (var tileList = list.CreateList("TILE"))
                            {
                                // Tile Meta
                                using (var tileMeta = tileList.CreateChunk("TMTA"))
                                {
                                    tileMeta.Write(tile._indexX);
                                    tileMeta.Write(tile._indexY);
                                    tileMeta.Write(tile._indexZ);
                                    tileMeta.Write(tile._generationState);
                                }

                                // Heightmap
                                using (var heightmap = tileList.CreateChunk("HMAP"))
                                {
                                    formatter.Serialize(heightmap, tile._heightmap);
                                }

                                // Block Data
                                using (var blockData = tileList.CreateChunk("BLKS"))
                                {
                                    formatter.Serialize(blockData, tile._gridBlock);
                                }

                                // Block Extra Data
                                if (tile._gridExtra != null)
                                {
                                    using (var extraData = tileList.CreateList("TXTA"))
                                    {
                                        foreach (var byteArray in tile._gridExtra)
                                        {
                                            using (var blockExtra = extraData.CreateChunk("BXTA"))
                                            {
                                                formatter.Serialize(blockExtra, byteArray);
                                            }
                                        }
                                    }
                                }

                                // Entities
                                if (tile._gridEntities != null)
                                {
                                    using (var entityData = tileList.CreateList("ENTS"))
                                    {
                                        foreach (var byteArray in tile._gridEntities)
                                        {
                                            using (var blockExtra = entityData.CreateChunk("ENTY"))
                                            {
                                                formatter.Serialize(blockExtra, byteArray);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                using (var fs = new GZipStream(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None), CompressionMode.Compress))
                {
                    var b = mems.GetBuffer();
                    fs.Write(b, 0, b.Length);
                    fs.Flush();
                }
            }
        }

        private readonly List<RegionData> dataCache = new List<RegionData>();
        private RegionData LoadRegionFile(string name, Vector3I region)
        {
            RegionData data = null;

            lock (dataCache)
            {
                foreach (var rgn in dataCache)
                {
                    if (rgn.Region == region)
                    {
                        data = rgn;
                        break;
                    }
                }

                if (data != null)
                {
                    dataCache.Remove(data);
                    dataCache.Add(data);

                    while (dataCache.Count > 5)
                        dataCache.RemoveAt(0);

                    return data;
                }

                data = new RegionData() { Region = region };
                dataCache.Add(data);

                while (dataCache.Count > 5)
                    dataCache.RemoveAt(0);
            }

            lock (data)
            {
                var formatter = new BinaryFormatter();
                var path = Path.Combine(SaveFolder.FullName, name);
                if (File.Exists(path))
                {
                    byte[] mdata;
                    using (var gzs = new GZipStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), CompressionMode.Decompress))
                    {
                        using (var mems = new MemoryStream())
                        {
                            gzs.CopyTo(mems);
                            mems.Close();
                            mdata = mems.ToArray();
                        }
                    }

                    using (var sw = new RiffFile(new MemoryStream(mdata)))
                    {
                        foreach (var chunk in sw.Chunks)
                        {
                            switch (chunk.ChunkId)
                            {
                            case "RMTA": // Region file meta
                                {
                                    var version = chunk.ReadByte();
                                    if (version != 1)
                                        throw new NotImplementedException();
                                }
                                break;
                            case "LIST":
                                {
                                    var list = chunk.ToList();
                                    switch (list.ListId)
                                    {
                                    case "TILS":
                                        foreach (var chunk2 in list.Chunks)
                                        {
                                            switch (chunk2.ChunkId)
                                            {
                                            case "LIST":
                                                {
                                                    var list2 = chunk2.ToList();
                                                    switch (list2.ListId)
                                                    {
                                                    case "TILE":
                                                        {
                                                            var tileData = new RegionData.TileData();
                                                            foreach (var chunk3 in list2.Chunks)
                                                            {
                                                                switch (chunk3.ChunkId)
                                                                {
                                                                case "TMTA":
                                                                    tileData._indexX = chunk3.ReadInt32();
                                                                    tileData._indexY = chunk3.ReadInt32();
                                                                    tileData._indexZ = chunk3.ReadInt32();
                                                                    tileData._generationState = chunk3.ReadInt32();
                                                                    data.SetTile(tileData);
                                                                    break;
                                                                case "HMAP":
                                                                    {
                                                                        var a = (int[])formatter.Deserialize(chunk);
                                                                        Array.Copy(a, tileData._heightmap, a.Length);
                                                                    }
                                                                    break;
                                                                case "BLKS":
                                                                    {
                                                                        var a = (ushort[])formatter.Deserialize(chunk);
                                                                        Array.Copy(a, tileData._gridBlock, a.Length);
                                                                    }
                                                                    break;
                                                                case "LIST":
                                                                    {
                                                                        var list3 = chunk3.ToList();
                                                                        switch (list3.ListId)
                                                                        {
                                                                        case "TXTA":
                                                                            break;
                                                                        case "ENTS":
                                                                            break;
                                                                        }
                                                                    }
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                        break;
                                                    }
                                                }
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }
                                break;
                            }

                            //            // Block Extra Data
                            //            using (var extraData = tileList.CreateList("TXTA"))
                            //            {
                            //                foreach (var byteArray in tile._gridExtra)
                            //                {
                            //                    using (var blockExtra = extraData.CreateChunk("BXTA"))
                            //                    {
                            //                        formatter.Serialize(blockExtra, byteArray);
                            //                    }
                            //                }
                            //            }

                            //            // Entities
                            //            using (var entityData = tileList.CreateList("ENTS"))
                            //            {
                            //                foreach (var byteArray in tile._gridEntities)
                            //                {
                            //                    using (var blockExtra = entityData.CreateChunk("ENTY"))
                            //                    {
                            //                        formatter.Serialize(blockExtra, byteArray);
                            //                    }
                            //                }
                            //            }
                        }
                    }
                }

                return data;
            }
        }

        private string NameOf(Vector3I pos)
        {
            return $"region{pos.X:+0;-#}{pos.Y:+0;-#}{pos.Z:+0;-#}.vgz";
        }

        public class RegionData
        {
            public class TileData
            {
                public int _indexX;
                public int _indexY;
                public int _indexZ;
                public int _generationState;
                public readonly ushort[] _gridBlock = new ushort[Tile.GridSize.X * Tile.GridSize.Y * Tile.GridSize.Z];
                public readonly int[] _heightmap = new int[Tile.GridSize.X * Tile.GridSize.Z];
                public byte[][] _gridExtra;
                public byte[][] _gridEntities;
            }

            public readonly TileData[] tiles = new TileData[RegionSize * RegionSize * RegionSize];
            public Vector3I Region { get; set; }

            public void SetTile(TileData tile)
            {
                var offset = new Vector3I(tile._indexX, tile._indexY, tile._indexZ) - Region * RegionSize;
                tiles[(offset.Z * RegionSize + offset.Y) * RegionSize + offset.X] = tile;
            }

            public void SetTile(Tile tile)
            {
                var offset = tile.Index - Region * RegionSize;
                tiles[(offset.Z * RegionSize + offset.Y) * RegionSize + offset.X] = tile.Serialize();
            }

            public bool ReadTile(Tile tile)
            {
                var offset = tile.Index - Region * RegionSize;
                var data = tiles[(offset.Z * RegionSize + offset.Y) * RegionSize + offset.X];
                if (data != null)
                {
                    tile.Deserialize(data);
                    return true;
                }
                return false;
            }
        }

        public void TryLoadTile(Tile tile, Action<bool> action)
        {
            // TODO: Async... someday.

            var index = RegionIndex(tile);
            var name = NameOf(index);
            var rgn = LoadRegionFile(name, index);

            bool b;
            lock (rgn)
            {
                b = rgn.ReadTile(tile);
            }
            action(b);
        }
    }
}
