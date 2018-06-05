using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Terrain.Graphics;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        // Tile properties
        private static readonly int RealSizeH = 32;
        private static readonly int RealSizeV = 32;
        private static readonly int GridSizeH = 32;
        private static readonly int GridSizeV = 32;
        private static readonly float VoxelSizeH = RealSizeH / (float)GridSizeH;
        private static readonly float VoxelSizeV = RealSizeV / (float)GridSizeV;
        public static readonly Vector3I GridSize = new Vector3I(GridSizeH, GridSizeV, GridSizeH);
        public static readonly Vector3I RealSize = new Vector3I(RealSizeH, RealSizeV, RealSizeH);
        public static readonly Vector3 VoxelSize = new Vector3(VoxelSizeH, VoxelSizeV, VoxelSizeH);

        private ushort[,,] _gridBlock;
        private int[,] _heightmap;
        private readonly CubeTree<object> _gridExtra = new CubeTree<object>();

        private int dependencies1 = 0;
        private int _generationPhase = -1;
        private bool _isSparse = true;

        public bool IsSparse => _isSparse;
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

        public void RequirePhase(int i)
        {
            if (GenerationPhase >= i)
                return;

            RequiredPhase = Math.Max(RequiredPhase, i);

            if (!Initialized)
                return;

            switch (GenerationPhase)
            {
                case -1: PreparePhase0(); break;
                case 0: PreparePhase1(); break;
            }
        }

        void PreparePhase0()
        {
            if (_generationPhase >= 0)
                return;

            RunProcess(GenTerrain, 0).ContinueWith(MarkNeighboursDirty);
        }

        void GenTerrain()
        {
            //Debug.WriteLine($"Begin tile {Index}...");

            var roughnesss = new double[GridSizeH, GridSizeH];
            var bottoms = new double[GridSizeH, GridSizeH];
            var topss = new double[GridSizeH, GridSizeH];

            for (int z = 0; z < GridSizeH; z++)
            {
                for (int x = 0; x < GridSizeH; x++)
                {
                    var pxyz = (Index * GridSize).Offset(x, 0, z);
                    Context.GetTopologyAt(pxyz.XZ, out roughnesss[z, x], out bottoms[z, x], out topss[z, x]);
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
                        var pxyz = (Index * GridSize).Offset(x,y,z);

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

                        SetBlock(new Vector3I(x, y, z), block);
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
                            SetBlock(xyz, Block.SeaWater);
                        }
                    }
                }
            }

            Interlocked.Exchange(ref _generationPhase, 0);

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
                    Tile dependantDown;
                    Tile dependantUp;

                    if (Parent.Require(Index.Offset(0, 1, 0), 0, out dependantUp))
                    {
                        Interlocked.Increment(ref dependencies1);
                        dependantUp.InvokeAfter(0, Phase1DependencyCallback);
                    }
                    if (Parent.Require(Index.Offset(0, -1, 0), 0, out dependantDown))
                    {
                        Interlocked.Increment(ref dependencies1);
                        dependantDown.InvokeAfter(0, Phase1DependencyCallback);
                    }

                    if (dependantDown == null && dependantUp == null)
                    {
                        RunProcess(PostProcessSurface, 1).ContinueWith(MarkNeighboursDirty);
                    }
                    // else will be called by the last 
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

        private void PostProcessSurface()
        {
            int relativeWaterLevel = Context.WaterLevel - Offset.Y;
            for (int z = 0; z < GridSizeH; z++)
            {
                for (int x = 0; x < GridSizeH; x++)
                {
                    int top = GetSolidTop(new Vector3I(x,0,z));
                    if (top == GridSizeV - 1)
                    {
#if false
                        bool groundFound = false;

                        for (int y = 0; y < DirtLayers; y++)
                        {
                            var load = _parent.GetBlock(ox, _offY + top + y, oz);
                            if (!load.PhysicsMaterial.IsSolid)
                            {
                                groundFound = true;
                                top += y;
                            }
                        }

                        if (!groundFound)
                            continue;
#endif
                    }

                    int yy = top;

                    while (yy >= 0)
                    {
                        var above = GetBlockRelative(x, yy + 1, z);

                        Block replaceWith1 = null;
                        Block replaceWith2 = null;
                        if (above == Block.Lava)
                        {
                            replaceWith1 = replaceWith2 = Block.Granite;
                        }
                        else if (yy >= relativeWaterLevel - Context.BeachBottom && yy <= relativeWaterLevel + Context.BeachTop)
                        {
                            replaceWith1 = replaceWith2 = Block.Sand;
                        }
                        else if (above.PhysicsMaterial == PhysicsMaterial.Air)
                        {
                            replaceWith1 = top == yy ? Block.Grass : Block.Dirt;
                            replaceWith2 = Block.Dirt;
                        }
                        else if (above == Block.SeaWater || above == Block.RiverWater)
                        {
                            replaceWith1 = replaceWith2 = Block.Gravel;
                        }

                        if (replaceWith1 != null)
                        {
                            for (int y = 0; y <= Context.DirtLayers; y++, yy--)
                            {
                                if (!GetBlockRelative(x, yy, z).PhysicsMaterial.IsSolid)
                                    break;

                                var below = GetBlockRelative(x, yy - 1, z);
                                if (!below.PhysicsMaterial.IsSolid)
                                    break;

                                SetBlockRelative(x, yy, z, y == 0 ? replaceWith1 : replaceWith2);
                            }
                        }

                        while (yy >= 0 && GetBlockRelative(x, yy, z).PhysicsMaterial != PhysicsMaterial.Air)
                        {
                            yy--;
                        }

                        while (yy >= 0 && GetBlockRelative(x, yy, z).PhysicsMaterial == PhysicsMaterial.Air)
                        {
                            yy--;
                        }
                    }
                }
            }

            Interlocked.Exchange(ref _generationPhase, 1);
        }

        public int GetSolidTop(Vector3I xyz)
        {
            if (_isSparse)
                return -1;
            return _heightmap[xyz.X, xyz.Z];
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

                InitializeStorage();
            }

            if (block.Key.InternalId.HasValue)
                _gridBlock[z,x,y] = block.Key.InternalId.Value;
        }

        private void InitializeStorage()
        {
            _gridBlock = new ushort[GridSizeH, GridSizeH, GridSizeV];
            _heightmap = new int[GridSizeH, GridSizeH];
            _isSparse = false;
        }

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

            if (IsDirty && _generationPhase >= 1)
            {
                if (Graphics.Rebuild())
                {
                    IsDirty = false;
                }
            }

            Graphics.Update(gameTime);

            base.Update(gameTime);
        }
    }
}