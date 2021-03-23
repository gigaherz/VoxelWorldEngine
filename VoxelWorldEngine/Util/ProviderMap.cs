using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VoxelWorldEngine.Util;
using VoxelWorldEngine.Util.Providers;

namespace VoxelWorldEngine.Terrain
{
    internal class ProviderMap
    {
        private readonly ConcurrentDictionary<object, object> values = new ConcurrentDictionary<object, object>();

        public T Get<T>(ProviderType<T> key)
                    where T : IValueProvider
        {
            return (T)values[key];
        }

        public bool Get<T>(ProviderType<T> key, out T value)
                    where T : IValueProvider
        {
            if (values.TryGetValue(key, out var v))
            {
                value = (T)v;
                return true;
            }

            value = default(T);
            return false;
        }

        public T GetOrAdd<T>(ProviderType<T> key, Func<ProviderType<T>, T> compute)
                    where T : IValueProvider
        {
            return (T)values.GetOrAdd(key, o => compute(key));
        }

        public T AddOrUpdate<T>(ProviderType<T> key, Func<ProviderType<T>, T> compute)
                    where T : IValueProvider
        {
            return (T)values.AddOrUpdate(key, o => compute(key), (o,o2) => compute(key));
        }
    }
}