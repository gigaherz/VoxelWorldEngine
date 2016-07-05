using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VoxelWorldEngine.Registry
{
    public class GenericRegistry
    {
    }

    public class GenericRegistry<T> : GenericRegistry, IDictionary<ObjectKey, T>
        where T : RegistrableObject<T>
    {
        readonly SortedList<ObjectKey, T> _registry = new SortedList<ObjectKey, T>();

        public int Count => _registry.Count;
        public ICollection<ObjectKey> Keys => _registry.Keys;
        public ICollection<T> Values => _registry.Values;
        public bool IsReadOnly => true;

        public T this[ObjectKey key]
        {
            get { return _registry[key]; }
            set { throw new InvalidOperationException("Assigning to the _registry not supported"); }
        }

        public bool ContainsKey(ObjectKey key)
        {
            return _registry.ContainsKey(key);
        }

        bool IDictionary<ObjectKey, T>.TryGetValue(ObjectKey key, out T value)
        {
            return _registry.TryGetValue(key, out value);
        }

        void ICollection<KeyValuePair<ObjectKey, T>>.CopyTo(KeyValuePair<ObjectKey, T>[] array, int arrayIndex)
        {
            ((IDictionary<ObjectKey, T>)_registry).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<ObjectKey, T>>.Contains(KeyValuePair<ObjectKey, T> item)
        {
            return _registry.Contains(item);
        }

        IEnumerator<KeyValuePair<ObjectKey, T>> IEnumerable<KeyValuePair<ObjectKey, T>>.GetEnumerator()
        {
            return _registry.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _registry).GetEnumerator();
        }

        public void Register(T obj)
        {
            if (ContainsKey(obj.Key))
                throw new InvalidOperationException("The object is already registered!");
            _registry.Add(obj.Key, obj);
        }

        void IDictionary<ObjectKey, T>.Add(ObjectKey key, T value)
        {
            throw new InvalidOperationException("Assigning to the _registry not supported");
        }

        bool IDictionary<ObjectKey, T>.Remove(ObjectKey key)
        {
            throw new InvalidOperationException("Assigning to the _registry not supported");
        }

        void ICollection<KeyValuePair<ObjectKey, T>>.Add(KeyValuePair<ObjectKey, T> item)
        {
            throw new InvalidOperationException("Assigning to the _registry not supported");
        }

        void ICollection<KeyValuePair<ObjectKey, T>>.Clear()
        {
            throw new InvalidOperationException("Assigning to the _registry not supported");
        }

        bool ICollection<KeyValuePair<ObjectKey, T>>.Remove(KeyValuePair<ObjectKey, T> item)
        {
            throw new InvalidOperationException("Assigning to the _registry not supported");
        }
    }
}