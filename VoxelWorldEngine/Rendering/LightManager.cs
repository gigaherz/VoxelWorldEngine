using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace VoxelWorldEngine.Rendering
{
    class LightManager
    {
        readonly List< DirectionalLight> directionalLights = new List<DirectionalLight>();

        public List<DirectionalLight> DirectionalLights => directionalLights;

        public LightManager(ContentManager content)
        {
        }

        public void AddLight(DirectionalLight light)
        {
            directionalLights.Add(light);
        }

        public void RemoveLight(DirectionalLight light)
        {
            directionalLights.Remove(light);
        }
    }
}