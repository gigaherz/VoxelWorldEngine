using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine.Objects
{
    public class RenderQueue : RegistrableObject<RenderQueue>
    {
        public static GenericRegistry<RenderQueue> Registry { get; private set; }
        public static void Initialize()
        {
            Registry = RegistryManager.GetRegistry<RenderQueue>();
            Registry.Map();
        }

        public static readonly RenderQueue
            None = new RenderQueue("none"),
            Opaque = new RenderQueue("opaque"),
            Transparent = new RenderQueue("transparent") { RequiresSorting = true,
                BlendState = BlendState.NonPremultiplied,
                //RasterizerState = RasterizerState.CullNone
            };

        public bool RequiresSorting { get; set; }
        public BlendState BlendState { get; set; } = BlendState.Opaque;
        public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;
        public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;

        public RenderQueue(string name)
            : this(VoxelGame.DefaultDomain, name)
        {
        }

        public RenderQueue(string domain, string name)
            : base(domain, name)
        {
        }

        public override string ToString()
        {
            return $"{{Key={Key}}}";
        }
    }
}