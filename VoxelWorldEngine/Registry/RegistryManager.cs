using System;
using System.Collections.Generic;

namespace VoxelWorldEngine.Registry
{
    public static class RegistryManager
    {
        private static readonly Dictionary<Type, GenericRegistry> Registries = new Dictionary<Type, GenericRegistry>();
        
        public static GenericRegistry<T> GetRegistry<T>()
            where T : RegistrableObject<T>
        {
            GenericRegistry reg;
            if (!Registries.TryGetValue(typeof(T), out reg))
            {
                var r = Activator.CreateInstance<GenericRegistry<T>>();
                Registries.Add(typeof(T), r);
                return r;
            }
            return (GenericRegistry<T>)reg;
        }

        public static T Find<T>(ObjectKey key)
            where T : RegistrableObject<T>
        {
            return GetRegistry<T>()[key];
        }
    }
}