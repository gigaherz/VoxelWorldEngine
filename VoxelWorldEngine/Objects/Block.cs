using System.ComponentModel;
using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine.Objects
{
    public class Block : RegistrableObject<Block>
    {
        // Empty/gas blocks
        public static readonly Block
            Air = new Block("air") { PhysicsMaterial = PhysicsMaterial.Air },

            // Liquids
            RiverWater = new Block("riverWater")
            {
                PhysicsMaterial = PhysicsMaterial.Liquid,
                RenderingMaterial = RenderingMaterial.RiverWater,
                Description = "Running river water, 'gives' water sideways, but only towards empty blocks or blocks with a lower level of water."
            },
            WaterFountain = new Block("waterFountain")
            {
                PhysicsMaterial = PhysicsMaterial.Liquid,
                Description = "Generates a constant value of river water every update, which it can spread sideways and even upwards."
            },
            SeaWater = new Block("seaWater")
            {
                PhysicsMaterial = PhysicsMaterial.Liquid,
                RenderingMaterial = RenderingMaterial.SeaWater,
                Description = "Solid block of sea water. Spreads down and sideways."
            },
            Lava = new Block("lava")
            {
                PhysicsMaterial = PhysicsMaterial.Liquid,
                Description = "Molten rock. similar to magma, but solidifies into stone."
            },
            Magma = new Block("magma")
            {
                PhysicsMaterial = PhysicsMaterial.Liquid,
                Description = "Molten rock. similar to lava, but solidifies into granite"
            },

            // Soft solids (fall down)
            Sand = new Block("sand")
            {
                PhysicsMaterial = PhysicsMaterial.Falling,
                RenderingMaterial = RenderingMaterial.Sand,
                Description = "Appears on beaches of sea and lakes"
            },
            Dirt = new Block("dirt")
            {
                PhysicsMaterial = PhysicsMaterial.Falling,
                RenderingMaterial = RenderingMaterial.Dirt,
                Description = "Surface ground material"
            },
            Gravel = new Block("gravel")
            {
                PhysicsMaterial = PhysicsMaterial.Falling,
                RenderingMaterial = RenderingMaterial.Gravel,
                Description = "Appears under running river water"
            },

            // Hard solids (don't fall down)
            Trunk = new Block("trunk")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                Description = "Tree trunk. Log."
            },
            Sandstone = new Block("sandstone")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                Description = "Compacted sand-like particles of quartz or other materials"
            },
            Stone = new Block("stone")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                RenderingMaterial = RenderingMaterial.Stone,
                Description = "Consider renaming to some kind of stone material"
            },
            Granite = new Block("granite")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                RenderingMaterial = RenderingMaterial.Granite,
                Description = "I googled 'hardest stone' and they said granite."
            },
            Grass = new Block("grass")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                RenderingMaterial = RenderingMaterial.Grass
            },

            // Crafted materials
            Wood = new Block("wood")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                Description = "Processed tree trunk, ready to use in crafting/construction"
            },
            Cobblestone = new Block("cobblestone")
            {
                PhysicsMaterial = PhysicsMaterial.Solid,
                Description = "Stone processed into cobbestone"
            },

            // Unbreakable block
            Unbreakite = new Block("unbreakite")
            {
                PhysicsMaterial = PhysicsMaterial.Unbreakable,
                RenderingMaterial = RenderingMaterial.Unbreakite
            };

        public PhysicsMaterial PhysicsMaterial { get; set; }
        public RenderingMaterial RenderingMaterial { get; set; } = RenderingMaterial.Air;
        public string Description { get; set; } = "";

        public Block(string name)
            : this(VoxelGame.DefaultDomain, name)
        {
        }

        public Block(string domain, string name)
            : base(domain, name)
        {
        }

        public override string ToString()
        {
            return $"{{Key={Key}}}";
        }
    }
}