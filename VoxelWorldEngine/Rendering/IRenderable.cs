using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace VoxelWorldEngine.Rendering
{
    public interface IRenderable
    {
        void Draw(GameTime gameTime, BaseCamera camera);
    }
}
