using Microsoft.Xna.Framework.Graphics;
using VoxelWorldEngine.Registry;

namespace VoxelWorldEngine.Objects
{
    public class RenderQueue : RegistrableObject<RenderQueue>
    {
        public static readonly RenderQueue
            None = new RenderQueue("none"),
            Opaque = new RenderQueue("opaque"),
            Transparent = new RenderQueue("transparent") { RequiresSorting = true,
                BlendState = BlendState.NonPremultiplied,
                RasterizerState = RasterizerState.CullNone
            };

        public bool RequiresSorting { get; set; }
        public BlendState BlendState { get; set; } = BlendState.Opaque;
        public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;

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