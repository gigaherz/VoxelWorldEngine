using Microsoft.Xna.Framework;
using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine.Objects
{
    public class RenderingMaterial : RegistrableObject<RenderingMaterial>
    {
        public static bool Touched { get; private set; }
        public static void Touch()
        {
            Touched = true;
        }

        public static readonly RenderingMaterial
            Air = new RenderingMaterial("air")
            {
                TileNumber = 0,
                Translucency = 0,
                Weight = 0,
                Hardness = 0,
                WaterCapacity = 0,
                RenderQueue = RenderQueue.None
            },
            Grass = new RenderingMaterial("grass")
            {
                TileNumber = 0,
                Translucency = 0,
                Weight = 1,
                Hardness = 5,
                WaterCapacity = 5,
                RenderQueue = RenderQueue.Opaque
            },
            Stone = new RenderingMaterial("stone")
            {
                TileNumber = 1,
                Translucency = 0,
                Weight = 5,
                Hardness = 25,
                WaterCapacity = 1,
                RenderQueue = RenderQueue.Opaque
            },
            Granite = new RenderingMaterial("granite")
            {
                TileNumber = 2,
                Translucency = 0,
                Weight = 10,
                Hardness = 50,
                WaterCapacity = 0,
                RenderQueue = RenderQueue.Opaque
            },
            RiverWater = new RenderingMaterial("riverWater")
            {
                TileNumber = 3,
                Translucency = 0.6f,
                Weight = 2,
                Hardness = 0,
                WaterCapacity = 15,
                RenderQueue = RenderQueue.Transparent
            },
            SeaWater = new RenderingMaterial("seaWater")
            {
                TileNumber = 3,
                Translucency = 0.6f,
                Weight = 2,
                Hardness = 0,
                WaterCapacity = 15,
                RenderQueue = RenderQueue.Transparent
            },
            Sand = new RenderingMaterial("sand")
            {
                TileNumber = 5,
                Translucency = 0,
                Weight = 1,
                Hardness = 5,
                WaterCapacity = 5,
                RenderQueue = RenderQueue.Opaque
            },
            Gravel = new RenderingMaterial("gravel")
            {
                TileNumber = 6,
                Translucency = 0,
                Weight = 1,
                Hardness = 5,
                WaterCapacity = 5,
                RenderQueue = RenderQueue.Opaque
            },
            Dirt = new RenderingMaterial("dirt")
            {
                TileNumber = 7,
                Translucency = 0,
                Weight = 1,
                Hardness = 5,
                WaterCapacity = 5,
                RenderQueue = RenderQueue.Opaque
            },
            Unbreakite = new RenderingMaterial("unbreakite")
            {
                TileNumber = 8,
                Translucency = 0,
                Weight = int.MaxValue,
                Hardness = int.MaxValue,
                WaterCapacity = 0,
                RenderQueue = RenderQueue.Opaque
            };
        
        public RenderQueue RenderQueue { get; set; } = RenderQueue.None;

        // TODO: specify texture file

        public int TileNumber { get; set; }

        public float Translucency { get; set; }

        public int Weight { get; set; }
        public int Hardness { get; set; }
        public int WaterCapacity { get; set; }

        public RenderingMaterial(string name)
            : this(VoxelGame.DefaultDomain, name)
        {
        }

        public RenderingMaterial(string domain, string name)
            : base(domain, name)
        {
        }

        internal void GetTexCoords(out Vector2 tex1, out Vector2 tex2, out Vector2 tex3, out Vector2 tex4)
        {
            int nw = 1024 / 64;
            int x = TileNumber % nw;
            int y = TileNumber / nw;
            float wh = 1.0f / nw;
            float wh2 = 1.0f / (nw + 1);
            float fx = x * wh;
            float fy = y * wh;

            tex1 = new Vector2(fx, fy + wh2);
            tex2 = new Vector2(fx + wh2, fy + wh2);
            tex3 = new Vector2(fx + wh2, fy);
            tex4 = new Vector2(fx, fy);
        }
    }
}
