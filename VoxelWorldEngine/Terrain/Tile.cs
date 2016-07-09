using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using VoxelWorldEngine.Noise;
using VoxelWorldEngine.Objects;
using VoxelWorldEngine.Util;

namespace VoxelWorldEngine.Terrain
{
    public class Tile : GameComponent
    {
        public const float VoxelSizeX = 1.0f;
        public const float VoxelSizeY = 0.5f;
        public const float VoxelSizeZ = 1.0f;
        public const int SizeXZ = 32;
        public const int SizeY = 32;
        public const int WaterLevel = 66;
        public const int DepthLevel = 32;
        public const int TopLevel = 128;
        public const int DirtLayers = 3;

        public const int BeachTop = 3;
        public const int BeachBottom = 5;

        private readonly Grid _parent;

        private readonly int _indexX;
        private readonly int _indexY;
        private readonly int _indexZ;
        private readonly int _offX;
        private readonly int _offY;
        private readonly int _offZ;

        private bool _isSparse = true;
        private Block[,,] _gridBlock;
        private int[,] _heightmap;
        private readonly Dictionary<Vector3I, object> _gridExtra = new Dictionary<Vector3I, object>();

        private bool _dirty;
        private bool _saved;
        private int _generationPhase = -1;

        public Simplex PerlinDensity { get; }
        public Simplex PerlinHeight { get; }
        public Simplex PerlinRoughness { get; }

        public int OffX => _offX;
        public int OffY => _offY;
        public int OffZ => _offZ;
        public Grid Parent => _parent;
        public TileGraphics Graphics => _graphics;
        public int IndexX => _indexX;
        public int IndexY => _indexY;
        public int IndexZ => _indexZ;
        public Vector3D Centroid => new Vector3D(_offX + SizeXZ / 2, _offY + SizeY / 2, _offZ + SizeXZ / 2);
        public bool IsSparse => _isSparse;

        private readonly TileGraphics _graphics;

        public Tile(Grid parent, int idxX, int idxY, int idxZ)
            : base(parent.Game)
        {
            _parent = parent;
            _indexX = idxX;
            _indexY = idxY;
            _indexZ = idxZ;
            _offX = idxX * SizeXZ;
            _offY = idxY * SizeY;
            _offZ = idxZ * SizeXZ;

            PerlinDensity = new Simplex(parent.Seed);
            PerlinHeight = new Simplex(parent.Seed * 3);
            PerlinRoughness = new Simplex(parent.Seed * 5);

            _graphics = new TileGraphics(this);
        }

        bool initialized = false;
        public override void Initialize()
        {
            base.Initialize();

            RunProcess(GenTerrain, 0);
        }

        private readonly ConcurrentQueue<Action>[] _pendingActions = {
            new ConcurrentQueue<Action>(),
            new ConcurrentQueue<Action>()
        };

        private void InvokeAfter(int phase, Action action)
        {
            if (!initialized)
                _parent.ForceTile(this);

            if (_generationPhase >= 0)
                action();
            else
                _pendingActions[phase].Enqueue(action);
        }

        private void RunProcess(Action process, int phase)
        {
            PriorityScheduler.StartNew(process, Centroid, 0).ContinueWith(task =>
            {
                if (!_isSparse)
                {
                    _dirty = true;

                    Tile tile;
                    if (_parent.Find(_indexX - 1, _indexY, _indexZ, out tile)) tile.MarkDirty();
                    if (_parent.Find(_indexX + 1, _indexY, _indexZ, out tile)) tile.MarkDirty();
                    if (_parent.Find(_indexX, _indexY - 1, _indexZ, out tile)) tile.MarkDirty();
                    if (_parent.Find(_indexX, _indexY + 1, _indexZ, out tile)) tile.MarkDirty();
                    if (_parent.Find(_indexX, _indexY, _indexZ - 1, out tile)) tile.MarkDirty();
                    if (_parent.Find(_indexX, _indexY, _indexZ + 1, out tile)) tile.MarkDirty();
                }
            });
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        int dependencies1 = 0;
        void GenTerrain()
        {
            PerlinDensity.Initialize();
            PerlinHeight.Initialize();
            PerlinRoughness.Initialize();

            var average = (TopLevel + DepthLevel) / 2;
            var range = (TopLevel - DepthLevel) / 2;

            for (int z = 0; z < SizeXZ; z++)
            {
                int pz = _indexZ * SizeXZ + z;
                var nz = (pz + 0.5) / 128;
                var hz = (pz + 0.5) / 256;
                var rz = (pz + 0.5) / 96;

                for (int x = 0; x < SizeXZ; x++)
                {
                    int px = _indexX * SizeXZ + x;
                    var nx = (px + 0.5) / 128;
                    var hx = (px + 0.5) / 256;
                    var rx = (px + 0.5) / 96;

                    var roughness = 1.8 + 0.4 * PerlinRoughness.Noise(rx, rz, 2);

                    var ph = PerlinHeight.Noise(hx, hz, 2);
                    var heightChange = (1 + 0.75 * ph);

                    var baseHeight = average * heightChange;

                    var bottom = baseHeight - range;
                    var top = baseHeight + range;

                    var topSolid = -1;
                    for (int y = 0; y < SizeY; y++)
                    {
                        int py = _indexY * SizeY + y;
                        var ny = (py + 0.5) / 256;

                        var block = Block.Unbreakite;

                        if (_offY + y > 0)
                        {
                            var baseDensity = 0.5 - Math.Max(0, Math.Min(1, (py - bottom) / (top - bottom)));

                            var density = baseDensity + 0.26 * PerlinDensity.Noise(nx, ny, nz, 4, roughness);

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
            
            int relativeWaterLevel = WaterLevel - _offY;

            // Add seawater
            for (int z = 0; z < SizeXZ; z++)
            {
                for (int x = 0; x < SizeXZ; x++)
                {
                    int y = Math.Min(SizeY-1, relativeWaterLevel);
                    while (y >= 0 && GetBlock(x,y,z) == Block.Air)
                    {
                        SetBlock(x, y, z, Block.SeaWater);
                        y--;
                    }
                }
            }

            if (_isSparse)
            {
                // We don't need to generate surfaces so don't queue the task at all
                Interlocked.Exchange(ref _generationPhase, 1);
            }
            else
            {
                Interlocked.Exchange(ref _generationPhase, 0);

                Tile dependantDown;
                Tile dependantUp;

                if (_parent.Find(_indexX, _indexY + 1, _indexZ, out dependantUp))
                {
                    Interlocked.Increment(ref dependencies1);
                    dependantUp.InvokeAfter(0, Phase1DependencyCallback);
                }
                if (_parent.Find(_indexX, _indexY - 1, _indexZ, out dependantDown))
                {
                    Interlocked.Increment(ref dependencies1);
                    dependantDown.InvokeAfter(0, Phase1DependencyCallback);
                }

                if (dependantDown == null && dependantUp == null)
                {
                    PostProcessSurface();
                }
                // else will be called by the last 
            }
        }

        private void Phase1DependencyCallback()
        {
            if (Interlocked.Decrement(ref dependencies1) == 0)
            {
                RunProcess(PostProcessSurface, 1);
            }
        }

        private void PostProcessSurface()
        {
            int relativeWaterLevel = WaterLevel - _offY;
            for (int z = 0; z < SizeXZ; z++)
            {
                var oz = _offZ + z;

                for (int x = 0; x < SizeXZ; x++)
                {
                    var ox = _offX + x;

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
                        var oyy = _offY + yy;

                        var above = _parent.GetBlock(ox, oyy + 1, oz);

                        Block replaceWith1 = null;
                        Block replaceWith2 = null;
                        if (above == Block.Lava)
                        {
                            replaceWith1 = replaceWith2 = Block.Granite;
                        }
                        else if (yy >= relativeWaterLevel - BeachBottom && yy <= relativeWaterLevel + BeachTop)
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
                            for (int y = 0; y <= DirtLayers; y++, yy--)
                            {
                                var oy = _offY + yy;

                                if (!_parent.GetBlock(ox, oy, oz).PhysicsMaterial.IsSolid)
                                    break;

                                var below = _parent.GetBlock(ox, oy - 1, oz);
                                if (!below.PhysicsMaterial.IsSolid)
                                    break;

                                _parent.SetBlock(ox, oy, oz, y == 0 ? replaceWith1 : replaceWith2);
                            }
                        }

                        while (yy >= 0 && _parent.GetBlock(ox, _offY + yy, oz).PhysicsMaterial != PhysicsMaterial.Air)
                        {
                            yy--;
                        }

                        while (yy >= 0 && _parent.GetBlock(ox, _offY + yy, oz).PhysicsMaterial == PhysicsMaterial.Air)
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
            if (_isSparse)
                return -1;
            return _heightmap[x, z];
        }

        public Block GetBlock(int x, int y, int z)
        {
            if (_isSparse)
                return Block.Air;
            return y >= SizeY || y < 0
                ? Block.Air
                : (_gridBlock[z, x, y] ?? Block.Air);
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (_isSparse)
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
            _isSparse = false;
        }

        public override void Update(GameTime gameTime)
        {
            for (int i = 0; i < _pendingActions.Length && i <= _generationPhase; i++)
            {
                Action action;
                while (_pendingActions[i].TryDequeue(out action))
                {
                    action();
                }
            }

            if (_dirty && _generationPhase >= 1)
            {
                if (_graphics.Rebuild())
                {
                    _dirty = false;
                }
            }

            _graphics.Update(gameTime);

            base.Update(gameTime);
        }

        public void Ready()
        {
            _parent.TilesInProgress--;
        }
    }
}