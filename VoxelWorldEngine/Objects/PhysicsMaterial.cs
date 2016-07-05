using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine.Objects
{
    public class PhysicsMaterial : RegistrableObject<PhysicsMaterial>
    {
        public static readonly PhysicsMaterial
            Air = new PhysicsMaterial("air") { IsSolid = false },
            Liquid = new PhysicsMaterial("liquid") { IsSolid = false, Height = 7 / 8f },
            Solid = new PhysicsMaterial("solid"),
            Falling = new PhysicsMaterial("falling"),
            Carpet = new PhysicsMaterial("carpet") { Height=1/8f },
            Unbreakable = new PhysicsMaterial("unbreakable");

        public float Height { get; set; } = 1;
        public bool IsSolid { get; set; } = true;

        public PhysicsMaterial(string name)
            : this(VoxelGame.DefaultDomain, name)
        {
        }

        public PhysicsMaterial(string domain, string name)
            : base(domain, name)
        {
        }

        public override string ToString()
        {
            return $"{{Key={Key}}}";
        }
    }
}