using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        // Voxel properties
        public const float VoxelSizeX = 1.0f;
        public const float VoxelSizeY = 0.5f;
        public const float VoxelSizeZ = 1.0f;

        // Tile properties
        public const int SizeXZ = 16;
        public const int SizeY = 32;

        private Block[,,] _gridBlock;
        private int[,] _heightmap;
        private readonly Dictionary<Vector3I, object> _gridExtra = new Dictionary<Vector3I, object>();

        public bool IsDirty { get; private set; }

        public bool IsSaved { get; }

        private int _generationPhase = -1;
        public int GenerationPhase => _generationPhase;

        public int OffX { get; }
        public int OffY { get; }
        public int OffZ { get; }

        public Grid Parent { get; }
        public GenerationContext Context { get; }

        public TileGraphics Graphics { get; }

        public int IndexX { get; }
        public int IndexY { get; }
        public int IndexZ { get; }

        public Vector3D Centroid => new Vector3D(OffX + SizeXZ / 2, OffY + SizeY / 2, OffZ + SizeXZ / 2);

        public bool IsSparse { get; private set; } = true;
        public bool Initialized { get; private set; }

        public Tile(Grid parent, int idxX, int idxY, int idxZ)
            : base(parent.Game)
        {
            Parent = parent;
            Context = parent.GenerationContext;
            IndexX = idxX;
            IndexY = idxY;
            IndexZ = idxZ;
            OffX = idxX * SizeXZ;
            OffY = idxY * SizeY;
            OffZ = idxZ * SizeXZ;

            Graphics = new TileGraphics(this);
        }

        public override void Initialize()
        {
            base.Initialize();

            Initialized = true;

            RunProcess(GenTerrain, 0).ContinueWith(MarkNeighboursDirty);
        }

        private void MarkNeighboursDirty(PriorityScheduler.PositionedTask task)
        {
            if (IsSparse) return;

            IsDirty = true;

            //Tile tile;
            //if (Parent.Find(IndexX - 1, IndexY, IndexZ, out tile)) tile.MarkDirty();
            //if (Parent.Find(IndexX + 1, IndexY, IndexZ, out tile)) tile.MarkDirty();
            //if (Parent.Find(IndexX, IndexY - 1, IndexZ, out tile)) tile.MarkDirty();
            //if (Parent.Find(IndexX, IndexY + 1, IndexZ, out tile)) tile.MarkDirty();
            //if (Parent.Find(IndexX, IndexY, IndexZ - 1, out tile)) tile.MarkDirty();
            //if (Parent.Find(IndexX, IndexY, IndexZ + 1, out tile)) tile.MarkDirty();
        }

        private readonly ConcurrentQueue<Action>[] _pendingActions = {
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>()
        };

        public void InvokeAfter(int phase, Action action)
        {
            if (!Initialized)
            {
                _pendingActions[0].Enqueue(() => { Parent.QueueTile(this); });
            }

            if (_generationPhase >= phase && Thread.CurrentThread == VoxelGame.GameThread)
                action();
            else
                _pendingActions[phase+1].Enqueue(action);
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
            IsDirty = true;
        }

        int dependencies1 = 0;
        void GenTerrain()
        {
            for (int z = 0; z < SizeXZ; z++)
            {
                int pz = IndexZ * SizeXZ + z;

                for (int x = 0; x < SizeXZ; x++)
                {
                    int px = IndexX * SizeXZ + x;

                    var topSolid = -1;
                    for (int y = 0; y < SizeY; y++)
                    {
                        int py = IndexY * SizeY + y;

                        var block = Block.Unbreakite;

                        if (OffY + y > Context.Floor)
                        {
                            var density = Context.GetDensityAt(px, py, pz);

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

                        SetBlock(x, y, z, block);
                    }

                    if (topSolid >= 0)
                        _heightmap[x, z] = topSolid;
                }
            }
            
            int relativeWaterLevel = Context.WaterLevel - OffY;

            // Add seawater
            for (int z = 0; z < SizeXZ; z++)
            {
                for (int x = 0; x < SizeXZ; x++)
                {
                    for (int y = 0; y < relativeWaterLevel; y++)
                    {
                        if (GetBlock(x, y, z) == Block.Air)
                        {
                            SetBlock(x, y, z, Block.SeaWater);
                        }
                    }
                }
            }

            if (IsSparse)
            {
                // We don't need to generate surfaces so don't queue the task at all
                Interlocked.Exchange(ref _generationPhase, 1);
            }
            else
            {
                Interlocked.Exchange(ref _generationPhase, 0);

                InvokeAfter(-1, () => {
                    Tile dependantDown;
                    Tile dependantUp;

                    if (Parent.Require(IndexX, IndexY + 1, IndexZ, out dependantUp))
                    {
                        Interlocked.Increment(ref dependencies1);
                        dependantUp.InvokeAfter(0, Phase1DependencyCallback);
                    }
                    if (Parent.Require(IndexX, IndexY - 1, IndexZ, out dependantDown))
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
            int relativeWaterLevel = Context.WaterLevel - OffY;
            for (int z = 0; z < SizeXZ; z++)
            {
                var oz = OffZ + z;

                for (int x = 0; x < SizeXZ; x++)
                {
                    var ox = OffX + x;

                    int top = GetSolidTop(x, z);
                    if (top == SizeY - 1)
                    {
#if false
                        bool groundFound = false;

                        for (int y = 0; y < DirtLayers; y++)
                        {
                            var b = _parent.GetBlock(ox, _offY + top + y, oz);
                            if (!b.PhysicsMaterial.IsSolid)
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
                        var oyy = OffY + yy;

                        var above = Parent.GetBlock(ox, oyy + 1, oz);

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
                                var oy = OffY + yy;

                                if (!Parent.GetBlock(ox, oy, oz).PhysicsMaterial.IsSolid)
                                    break;

                                var below = Parent.GetBlock(ox, oy - 1, oz);
                                if (!below.PhysicsMaterial.IsSolid)
                                    break;

                                Parent.SetBlock(ox, oy, oz, y == 0 ? replaceWith1 : replaceWith2);
                            }
                        }

                        while (yy >= 0 && Parent.GetBlock(ox, OffY + yy, oz).PhysicsMaterial != PhysicsMaterial.Air)
                        {
                            yy--;
                        }

                        while (yy >= 0 && Parent.GetBlock(ox, OffY + yy, oz).PhysicsMaterial == PhysicsMaterial.Air)
                        {
                            yy--;
                        }
                    }
                }
            }

            Interlocked.Exchange(ref _generationPhase, 1);
        }

        public int GetSolidTop(int x, int z)
        {
            if (IsSparse)
                return -1;
            return _heightmap[x, z];
        }

        public Block GetBlock(int x, int y, int z)
        {
            if (IsSparse)
                return Block.Air;
            return y >= SizeY || y < 0
                ? Block.Air
                : (_gridBlock[z, x, y] ?? Block.Air);
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (IsSparse)
            {
                if (block == Block.Air)
                    return;

                InitializeStorage();
            }

            if (y < SizeY && y >= 0)
                _gridBlock[z, x, y] = block;
        }

        private void InitializeStorage()
        {
            _gridBlock = new Block[SizeXZ, SizeXZ, SizeY];
            _heightmap = new int[SizeXZ, SizeXZ];
            IsSparse = false;
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