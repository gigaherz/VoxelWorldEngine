using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Storage;
using VoxelWorldEngine.Terrain.Graphics;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        // Tile properties
        private static readonly int RealSizeH = 16;
        private static readonly int RealSizeV = 16;
        private static readonly int GridSizeH = 16;
        private static readonly int GridSizeV = 16;
        private static readonly float VoxelSizeH = RealSizeH / (float)GridSizeH;
        private static readonly float VoxelSizeV = RealSizeV / (float)GridSizeV;
        public static readonly Vector3I GridSize = new Vector3I(GridSizeH, GridSizeV, GridSizeH);
        public static readonly Vector3I RealSize = new Vector3I(RealSizeH, RealSizeV, RealSizeH);
        public static readonly Vector3 VoxelSize = new Vector3(VoxelSizeH, VoxelSizeV, VoxelSizeH);

        private ushort[,,] _gridBlock;
        private int[,] _heightmap;
        private readonly CubeTree<ISerializable> _gridExtra = new CubeTree<ISerializable>();

        private int dependencies1 = 0;
        private int _generationPhase = -1;
        private bool _isSparse = true;

        private bool _isSolid = false;
        private bool[] _isSolidPlane = new bool[GridSizeV];

        private object _lock = new object();

        public bool IsSparse => _isSparse;
        public bool IsSolid => _isSolid;
        public int GenerationPhase => _generationPhase;
        public int RequiredPhase { get; private set; } = 0;

        public bool Initialized { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsSaved { get; }

        public Grid Parent { get; }
        public GenerationContext Context { get; }

        public TileGraphics Graphics { get; }

        public Vector3I Index { get; }
        public Vector3I Offset { get; }

        public EntityPosition Centroid { get; }

        private int _previousGenerationPhase = -1;
        private void OnGenerationPhaseChange()
        {
            Parent.TilePhaseChanged(this);
        }

        public Tile(Grid parent, Vector3I index)
            : base(parent.Game)
        {
            Parent = parent;
            Context = parent.GenerationContext;
            Index = index;
            Offset = index * GridSize;

            Centroid = EntityPosition.Create(Index, RealSize * 0.5f);

            Graphics = new TileGraphics(this);
        }

        private readonly ConcurrentQueue<Action>[] _pendingActions = {
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>()
        };

        public void InvokeAfter(int phase, Action action)
        {
            if (_generationPhase >= phase && Thread.CurrentThread == VoxelGame.GameThread)
                action();
            else
                _pendingActions[phase + 1].Enqueue(action);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Graphics.Dispose();
        }

        public override void Initialize()
        {
            base.Initialize();

            Initialized = true;

            RequirePhase(RequiredPhase);
        }

        private void MarkNeighboursDirty(PriorityScheduler.PositionedTask task)
        {
            if (!_isSparse)
                IsDirty = true;

            Tile tile;
            if (Parent.Find(Index.Offset(-1,0,0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(+1,0,0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0,-1,0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0,+1,0), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0,0,-1), out tile)) tile.MarkDirty();
            if (Parent.Find(Index.Offset(0,0,+1), out tile)) tile.MarkDirty();
        }


        public PriorityScheduler.PositionedTask RunProcess(Action process, int phase)
        {
            Parent.TilesInProgress++;

            return PriorityScheduler.StartNew(process, Centroid).ContinueWith(t => {
                Parent.TilesInProgress--;
            });
        }

        private void MarkDirty()
        {
            if (!_isSparse)
                IsDirty = true;
        }

        public bool RequirePhase(int i)
        {
            if (GenerationPhase >= i)
                return true;

            RequiredPhase = Math.Max(RequiredPhase, i);

            if (!Initialized)
                return false;

            switch (GenerationPhase)
            {
                case -1: PreparePhase0(); break;
                case 0: PreparePhase1(); break;
            }

            return false;
        }

        void PreparePhase0()
        {
            if (_generationPhase >= 0)
                return;

            RunProcess(GenTerrain, 0).ContinueWith(MarkNeighboursDirty);
        }


#if INSTRUMENT
        private static int _callsGen0;
        private static double _timeGen0;
#endif
        void GenTerrain()
        {
            var stopwatchGen0 = new Stopwatch();

            lock (_lock)
            {
#if INSTRUMENT
                stopwatchGen0.Start();
#endif

                var roughnesss = new double[GridSizeH, GridSizeH];
                var bottoms = new double[GridSizeH, GridSizeH];
                var topss = new double[GridSizeH, GridSizeH];

                for (int z = 0; z < GridSizeH; z++)
                {
                    for (int x = 0; x < GridSizeH; x++)
                    {
                        GetTopology(x, z, out roughnesss[z, x], out bottoms[z, x], out topss[z, x]);
                    }
                }

                for (int z = 0; z < GridSizeH; z++)
                {
                    for (int x = 0; x < GridSizeH; x++)
                    {
                        var roughness = roughnesss[z, x];
                        var bottom = bottoms[z, x];
                        var top = topss[z, x];
                        var topSolid = -1;
                        for (int y = 0; y < GridSizeV; y++)
                        {
                            var pxyz = (Index * GridSize).Offset(x, y, z);

                            var block = Block.Unbreakite;

                            // TODO: Add back optional ceiling/floor generation
                            if (true) //Offset.Y + y > Context.Floor)
                            {
                                var density = Context.GetDensityAt(pxyz, roughness, bottom, top);

                                if (density < 0.0)
                                {
                                    block = Block.Air;
                                }
                                else if (density < 0.50)
                                {
                                    block = Block.Stone;
                                }
                                else
                                {
                                    block = Block.Granite;
                                }
                            }

                            if (block.PhysicsMaterial.IsSolid)
                            {
                                topSolid = Math.Max(topSolid, y);
                            }

                            SetBlockNoLock(x, y, z, block);
                        }

                        if (topSolid >= 0)
                            _heightmap[x, z] = topSolid;
                    }
                }

                int relativeWaterLevel = Math.Min(GridSizeV, Context.WaterLevel - Offset.Y);

                // Add seawater
                for (int z = 0; z < GridSizeH; z++)
                {
                    for (int x = 0; x < GridSizeH; x++)
                    {
                        for (int y = 0; y < relativeWaterLevel; y++)
                        {
                            var xyz = new Vector3I(x, y, z);
                            if (GetBlock(xyz) == Block.Air)
                            {
                                SetBlockNoLock(x,y,z, Block.SeaWater);
                            }
                        }
                    }
                }

                Interlocked.Exchange(ref _generationPhase, 0);

#if INSTRUMENT
                stopwatchGen0.Stop();
                _callsGen0++;
                _timeGen0 += stopwatchGen0.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
#endif
            }

            PreparePhase1();

        }

        private void PreparePhase1()
        {
            if (_generationPhase >= 1)
                return;

            if (_isSparse)
            {
                // We don't need to generate surfaces so don't queue the task at all
                Interlocked.Exchange(ref _generationPhase, 1);
            }
            else if (RequiredPhase >= 1)
            {
                InvokeAfter(-1, () => {
                    int deps=0;

                    // Depend on the chunk above, in order to be able to get blocks above the chunk limit
                    if (!Parent.Require(Index.Offset(0, 1, 0), 0, out var depUp))
                    {
                        Interlocked.Increment(ref dependencies1);
                        depUp.InvokeAfter(0, Phase1DependencyCallback);
                        deps++;
                    }

                    // Depend on the chunk below, in order to be able to set blocks when processing surfaces
                    if (!Parent.Require(Index.Offset(0, -1, 0), 0, out var depDown))
                    {
                        Interlocked.Increment(ref dependencies1);
                        depDown.InvokeAfter(0, Phase1DependencyCallback);
                        deps++;
                    }

                    // If all the surrounding chunks are already generated, run immediately
                    if (deps == 0)
                    {
                        RunProcess(PostProcessSurface, 1).ContinueWith(MarkNeighboursDirty);
                    }
                });
            }
        }

        private void Phase1DependencyCallback()
        {
            if (Interlocked.Decrement(ref dependencies1) == 0)
            {
                RunProcess(PostProcessSurface, 1).ContinueWith(MarkNeighboursDirty);
            }
        }

        private readonly float rsqr2 = 1.0f / (float)Math.Sqrt(2);
        private void PostProcessSurface()
        {
            lock (_lock)
            {
                int relativeWaterLevel = Context.WaterLevel - Offset.Y;
                for (int z = 0; z < GridSizeH; z++)
                {
                    for (int x = 0; x < GridSizeH; x++)
                    {
                        for (int y = 0; y < GridSizeV; y++)
                        {
                            var block = GetBlock(x, y, z);
                            var above = (y+1) < GridSizeV ? GetBlock(x,y+1,z) : GetBlockRelative(x, y + 1, z);

                            if (block.PhysicsMaterial.IsSolid && !above.PhysicsMaterial.IsSolid)
                            {
                                // Found surface!

                                float avgSlope = GetSlopeAt(x, y, z, 10);
                                if (avgSlope > 2)
                                    continue;
                                //double avgSlope = GetGradientAt(x, y, z);
                                //if (avgSlope > 0.01)
                                //    continue;

                                int yCeiling = GetProbableCeilingPosition(x, y + 1, z, 128); // search up to one chunk's height up, for now
                                bool hasCeiling = (yCeiling - y) < 128;

                                if (GetSurfaceMaterials(above, y, relativeWaterLevel, hasCeiling, out var replaceWith))
                                {
                                    for (int i = 0; i < replaceWith.Length; i++)
                                    {
                                        SetBlockRelativeNoLock(x,y-i,z,replaceWith[i]);
                                    }
                                }
                            }
                        }
                    }
                }

                Interlocked.Exchange(ref _generationPhase, 1);
            }
        }

        private bool GetSurfaceMaterials(Block above, int y, int relativeWaterLevel, bool hasCeiling, out Block[] replaceWith)
        {
            if (above.PhysicsMaterial == PhysicsMaterial.Air)
            {
                replaceWith = new[] { hasCeiling ? Block.Dirt : Block.Grass, Block.Dirt, Block.Dirt, Block.Dirt };
                return true;
            }

            // TODO: only perform this replacement in beachy areas!
            if (y >= relativeWaterLevel - Context.BeachBottom && y <= relativeWaterLevel + Context.BeachTop)
            {
                replaceWith = new[] { Block.Sand, Block.Sand, Block.Sand, Block.Sandstone };
                return true;
            }

            if (above == Block.SeaWater || above == Block.RiverWater)
            {
                replaceWith = new[] { Block.Gravel, Block.Gravel, Block.Gravel, Block.Gravel };
                return true;
            }

            if (above == Block.Lava)
            {
                replaceWith = new[] { Block.Granite, Block.Granite, Block.Granite, Block.Granite };
                return true;
            }

            replaceWith = new Block[0];
            return false;
        }

        private void GetTopology(int x, int z, out double roughnesss, out double bottoms, out double topss)
        {
            var pxyz = Offset.Offset(x, 0, z);
            Context.GetTopologyAt(pxyz.XZ, out roughnesss, out bottoms, out topss);
        }

        public int GetProbableSurfacePosition(int x, int top, int z, int maxscan)
        {
            var pos = Offset.Offset(x, top, z);
            var startY = pos.Y;

            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);

            double density;
            do
            {
                pos += new Vector3I(0, 1, 0);
                density = Context.GetDensityAt(pos, roughness, dbottom, dtop);
            }
            while (density >= 0 && (pos.Y - startY) < maxscan);

            return (pos.Y - startY) + top;
        }

        public int GetProbableCeilingPosition(int x, int top, int z, int maxscan)
        {
            var pos = Offset.Offset(x, top, z);
            var startY = pos.Y;

            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);

            double density;
            do
            {
                pos += new Vector3I(0, 1, 0);
                density = Context.GetDensityAt(pos, roughness, dbottom, dtop);
            }
            while (density < 0 && (pos.Y - startY) < maxscan);

            if ((pos.Y - startY) >= maxscan)
                return int.MaxValue;

            return (pos.Y - startY) + top;
        }

        public double GetSlowDensityAt(int x, int y, int z)
        {
            var pos = Offset.Offset(x, y, z);
            Context.GetTopologyAt(pos.XZ, out var roughness, out var dbottom, out var dtop);
            return Context.GetDensityAt(pos, roughness, dbottom, dtop);
        }

        private double GetGradientAt(int x, int y, int z)
        {
            double top = GetSlowDensityAt(x, y, z);
            double top0 = GetSlowDensityAt(x, y, z + 1);
            double top1 = GetSlowDensityAt(x, y, z - 1);
            double top2 = GetSlowDensityAt(x - 1, y, z);
            double top3 = GetSlowDensityAt(x + 1, y, z);
            double top4 = GetSlowDensityAt(x - 1, y, z + 1);
            double top5 = GetSlowDensityAt(x + 1, y, z - 1);
            double top6 = GetSlowDensityAt(x + 1, y, z + 1);
            double top7 = GetSlowDensityAt(x - 1, y, z - 1);

            return (Math.Abs(top0 - top) + Math.Abs(top1 - top) +
                    Math.Abs(top2 - top) + Math.Abs(top3 - top) +
                    (Math.Abs(top4 - top) + Math.Abs(top5 - top) +
                     Math.Abs(top6 - top) + Math.Abs(top7 - top)) * rsqr2) * (1.0 / (4 + 4 * rsqr2));
        }

        private float GetSlopeAt(int x, int y, int z, int searchLimit)
        {
            var sl2 = searchLimit*2;
            int top0 = GetProbableSurfacePosition(x, y- searchLimit, z + 1, sl2);
            int top1 = GetProbableSurfacePosition(x, y- searchLimit, z - 1, sl2);
            int top2 = GetProbableSurfacePosition(x - 1, y- searchLimit, z, sl2);
            int top3 = GetProbableSurfacePosition(x + 1, y - searchLimit, z, sl2);
            int top4 = GetProbableSurfacePosition(x - 1, y - searchLimit, z + 1, sl2);
            int top5 = GetProbableSurfacePosition(x + 1, y - searchLimit, z - 1, sl2);
            int top6 = GetProbableSurfacePosition(x + 1, y - searchLimit, z + 1, sl2);
            int top7 = GetProbableSurfacePosition(x - 1, y - searchLimit, z - 1, sl2);

            return (Math.Abs(top0 - top1) + Math.Abs(top2 - top3) +
                    (Math.Abs(top4 - top5) + Math.Abs(top6 - top7)) * rsqr2) * (1.0f / (4 + 4 * rsqr2));
        }

        private float GetSlopeAt3(int x, int y, int z)
        {
            int top0 = GetProbableSurfacePosition(x, y, z + 1, 10);
            int top1 = GetProbableSurfacePosition(x, y, z - 1, 10);
            int top2 = GetProbableSurfacePosition(x - 1, y, z, 10);
            int top3 = GetProbableSurfacePosition(x + 1, y, z, 10);
            int top4 = GetProbableSurfacePosition(x - 1, y, z + 1, 10);
            int top5 = GetProbableSurfacePosition(x + 1, y, z - 1, 10);
            int top6 = GetProbableSurfacePosition(x + 1, y, z + 1, 10);
            int top7 = GetProbableSurfacePosition(x - 1, y, z - 1, 10);

            return (Math.Abs(top0 - top1) + Math.Abs(top2 - top3) +
                    (Math.Abs(top4 - top5) + Math.Abs(top6 - top7)) * rsqr2) * (1.0f / (4 + 4 * rsqr2));
        }

        private float GetSlopeAt2(int x, int y, int z)
        {
            int top0 = GetProbableSurfacePosition(x, y, z + 1, 10);
            int top1 = GetProbableSurfacePosition(x, y, z - 1, 10);
            int top2 = GetProbableSurfacePosition(x - 1, y, z, 10);
            int top3 = GetProbableSurfacePosition(x + 1, y, z, 10);
            int top4 = GetProbableSurfacePosition(x - 1, y, z + 1, 10);
            int top5 = GetProbableSurfacePosition(x + 1, y, z - 1, 10);
            int top6 = GetProbableSurfacePosition(x + 1, y, z + 1, 10);
            int top7 = GetProbableSurfacePosition(x - 1, y, z - 1, 10);

            return (Math.Abs(top0 - y) + Math.Abs(top1 - y) +
                    Math.Abs(top2 - y) + Math.Abs(top3 - y) +
                    (Math.Abs(top4 - y) + Math.Abs(top5 - y) +
                     Math.Abs(top6 - y) + Math.Abs(top7 - y)) * rsqr2) * (1.0f / (4 + 4 * rsqr2));
        }

        public int GetSolidTopRelative(int x, int y, int z)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeH &&
                z >= 0 && z < GridSizeH)
                return GetSolidTop(x, z);

            return Parent.GetSolidTop(new Vector3I(x, y, z) + Offset);
        }

        public int GetSolidTop(int x, int z)
        {
            if (_isSparse)
                return -1;
            return _heightmap[x, z];
        }

        public Block GetBlockRelative(int x, int y, int z, bool load = true)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                return GetBlock(x, y, z);

            return Parent.GetBlock(new Vector3I(x,y,z) + Offset, load);
        }

        public void SetBlockRelative(int x, int y, int z, Block block)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                SetBlock(new Vector3I(x, y, z), block);

            Parent.SetBlock(new Vector3I(x, y, z) + Offset, block);
        }

        public void SetBlockRelativeNoLock(int x, int y, int z, Block block)
        {
            if (x >= 0 && x < GridSizeH &&
                y >= 0 && y < GridSizeV &&
                z >= 0 && z < GridSizeH)
                SetBlockNoLock(x, y, z, block);

            Parent.SetBlock(new Vector3I(x, y, z) + Offset, block);
        }

        public Block GetBlock(Vector3I xyz)
        {
            return GetBlock(xyz.X, xyz.Y, xyz.Z);
        }

        public Block GetBlock(int x, int y, int z)
        {
            if (_isSparse)
                return Block.Air;
            return Block.Registry[_gridBlock[z,x,y]] ?? Block.Air;
        }

        public void SetBlock(Vector3I xyz, Block block)
        {
            SetBlock(xyz.X, xyz.Y, xyz.Z, block);
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (_isSparse)
            {
                if (block == Block.Air)
                    return;

                lock (_lock)
                {
                    InitializeStorage();
                }
            }

            if (block.Key.InternalId.HasValue)
            {
                lock (_lock)
                {
                    _gridBlock[z, x, y] = block.Key.InternalId.Value;
                }
            }
        }

        public void SetBlockNoLock(int x, int y, int z, Block block)
        {
            if (_isSparse)
            {
                if (block == Block.Air)
                    return;

                InitializeStorage();
            }

            if (block.Key.InternalId.HasValue)
            {
                _gridBlock[z, x, y] = block.Key.InternalId.Value;
            }
        }

        private void InitializeStorage()
        {
            _gridBlock = new ushort[GridSizeH, GridSizeH, GridSizeV];
            _heightmap = new int[GridSizeH, GridSizeH];
            _isSparse = false;
        }

        int _solidCheckIndex = 0;
        public override void Update(GameTime gameTime)
        {
            for (int i = 0; i < _pendingActions.Length && (i-1) <= _generationPhase; i++)
            {
                Action action;
                while (_pendingActions[i].TryDequeue(out action))
                {
                    action();
                }
            }

            if (_previousGenerationPhase != _generationPhase)
            {
                OnGenerationPhaseChange();
                _previousGenerationPhase = _generationPhase;
            }

            if (IsDirty && _generationPhase >= 1)
            {
                if (Graphics.Rebuild())
                {
                    IsDirty = false;
                }
            }

            Graphics.Update(gameTime);

            base.Update(gameTime);

            //if (_isSparse)
            //    _isSolid = false;
            //else
            //{
            //    int plane = _solidCheckIndex++ % GridSizeV;

            //    bool allSolid = true;
            //    for (int z = 0; allSolid & z < GridSizeH; z++)
            //    {
            //        for (int x = 0; allSolid & x < GridSizeH; x++)
            //        {
            //            allSolid &= GetBlock(x, plane, z).PhysicsMaterial.IsSolid;
            //        }
            //    }
            //    _isSolidPlane[plane] = allSolid;

            //    if (_solidCheckIndex % GridSizeV == 0)
            //    {
            //        allSolid = true;
            //        for (int y = 0; allSolid & y < GridSizeH; y++)
            //        {
            //            allSolid &= _isSolidPlane[y];
            //        }
            //        _isSolid = allSolid;
            //    }
            //}
        }

        public CrappyChunkStorage.RegionData.TileData Serialize()
        {
            lock (_lock)
            {
                var t = new CrappyChunkStorage.RegionData.TileData();

                t._indexX = Index.X;
                t._indexY = Index.Y;
                t._indexZ = Index.Z;
                t._generationState = _generationPhase;

                if (_gridBlock != null)
                {
                    for (int z = 0, i = 0; z < GridSize.Z; z++)
                    {
                        for (int y = 0; y < GridSize.Y; y++)
                        {
                            for (int x = 0; x < GridSize.X; x++, i++)
                            {
                                t._gridBlock[i] = _gridBlock[x, y, z];
                            }
                        }
                    }
                }

                if (_heightmap != null)
                {
                    for (int z = 0, i = 0; z < GridSize.Z; z++)
                    {
                        for (int x = 0; x < GridSize.X; x++, i++)
                        {
                            t._heightmap[i] = _heightmap[x, z];
                        }
                    }
                }

                t._gridExtra = new byte[0][];
                t._gridEntities = new byte[0][];

                return t;
            }
        }

        public void Deserialize(CrappyChunkStorage.RegionData.TileData data)
        {
            lock (_lock)
            {
                _generationPhase = data._generationState;

                if (_isSparse)
                {
                    if (data._gridBlock.All(b => b == Block.Air.Key.InternalId))
                        return;

                    InitializeStorage();
                }

                for (int z = 0, i = 0; z < GridSize.Z; z++)
                {
                    for (int y = 0; y < GridSize.Y; y++)
                    {
                        for (int x = 0; x < GridSize.X; x++, i++)
                        {
                            _gridBlock[x, y, z] = data._gridBlock[i];
                        }
                    }
                }

                for (int z = 0, i = 0; z < GridSize.Z; z++)
                {
                    for (int x = 0; x < GridSize.X; x++, i++)
                    {
                        _heightmap[x, z] = data._heightmap[i];
                    }
                }

                IsDirty = true;
            }
        }
    }
}