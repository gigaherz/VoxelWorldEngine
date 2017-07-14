using System;
using System.Linq;
using System.Threading;

namespace VoxelWorldEngine.Util
{
    public class OcTree<TValue>
    {
        private class OcNode
        {
            readonly object[] values = new object[8];

            public object this[int x, int y, int z]
            {
                get
                {
                    return values[(x << 2) | (y << 1) | z];
                }
                set
                {
                    values[(x << 2) | (y << 1) | z] = value;
                }
            }

            public bool IsEmpty => values.All(v => v == null);

            public override string ToString()
            {
                return $"{{Branch: {values[0]}, {values[1]}, {values[2]}, {values[3]}, {values[4]}, {values[5]}, {values[6]}, {values[7]}}}";
            }
        }

        private class OcSingle
        {
            public int x;
            public int y;
            public int z;
            public TValue value;

            public OcSingle(int x, int y, int z, TValue value)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.value = value;
            }

            public override string ToString()
            {
                return $"{{Single:{x},{y},{z}}}";
            }
        }

        private readonly OcNode _root = new OcNode();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public TValue this[int x, int y, int z]
        {
            get
            {
                TValue value;
                TryGetValue(x, y, z, out value);
                return value;
            }
            set
            {
                SetValue(x, y, z, value);
            }
        }

        public bool TryGetValue(int x, int y, int z, out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                x = (int)((uint)(x << 1) | ((uint)x >> 31));
                y = (int)((uint)(y << 1) | ((uint)y >> 31));
                z = (int)((uint)(z << 1) | ((uint)z >> 31));

                return TryGetValueInternal(x, y, z, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public TValue SetValue(int x, int y, int z, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (value == null && _root == null)
                {
                    return default(TValue);
                }

                x = (int)((uint)(x << 1) | ((uint)x >> 31));
                y = (int)((uint)(y << 1) | ((uint)y >> 31));
                z = (int)((uint)(z << 1) | ((uint)z >> 31));

                var isRemoving = Equals(value, default(TValue));
                var ret = SetValueInternal(_root, x, y, z, value, isRemoving);

                return ret;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private TValue SetValueInternal(OcNode node, int x, int y, int z, TValue value, bool isRemoving)
        {
            var existing = node[x & 1, y & 1, z & 1];

            if (existing == null)
            {
                if (!isRemoving)
                    node[x & 1, y & 1, z & 1] = new OcSingle(x, y, z, value);
                return default(TValue);
            }

            var child = existing as OcNode;
            if (child != null)
            {
                var ret = SetValueInternal(child, x >> 1, y >> 1, z >> 1, value, isRemoving);
                if (child.IsEmpty)
                {
                    node[x & 1, y & 1, z & 1] = null;
                }
                return ret;
            }

            var single = (OcSingle)existing;
            if (Equals(single.value, value))
                return single.value;

            if (single.x != x || single.y != y || single.z != z)
            {
                if (isRemoving)
                    return default(TValue);

                var split = SplitSingle(node, single);
                return SetValueInternal(split, x >> 1, y >> 1, z >> 1, value, false);
            }

            if (isRemoving)
            {
                node[x & 1, y & 1, z & 1] = null;
                return single.value;
            }

            var vv = single.value;
            single.value = value;
            return vv;
        }

        private bool TryGetValueInternal(int x, int y, int z, out TValue value)
        {
            value = default(TValue);

            var node = _root;
            while (true)
            {
                var v = node[x & 1, y & 1, z & 1];
                if (v == null)
                    break;
                var single = v as OcSingle;
                if (single != null)
                {
                    if (single.x == x && single.y == y && single.z == z)
                    {
                        value = single.value;
                        return true;
                    }
                    return false;
                }
                x >>= 1;
                y >>= 1;
                z >>= 1;
                node = v as OcNode;
            }

            return false;
        }

        private OcNode SplitSingle(OcNode node, OcSingle single)
        {
            var child = new OcNode();

            var sx = single.x;
            var sy = single.y;
            var sz = single.z;

            int x = sx >> 1;
            int y = sy >> 1;
            int z = sz >> 1;

            child[x & 1, y & 1, z & 1] = new OcSingle(x, y, z, single.value);
            node[sx & 1, sy & 1, sz & 1] = child;
            return child;
        }

    }

    public class CubeTree<TValue>
    {
        private const int SHIFT = 2;
        private const int SHIFT2 = SHIFT+SHIFT;
        private const int SIZE = (1 << SHIFT);
        private const int MASK = SIZE - 1;

        private class OcNode
        {
            readonly object[] values = new object[SIZE * SIZE * SIZE];

            public object this[int x, int y, int z]
            {
                get
                {
                    return values[(x << SHIFT2) | (y << SHIFT) | z];
                }
                set
                {
                    values[(x << SHIFT2) | (y << SHIFT) | z] = value;
                }
            }

            public bool IsEmpty => values.All(v => v == null);

            public override string ToString()
            {
                return $"{{Branch: {values[0]}, {values[1]}, {values[2]}, {values[3]}, {values[4]}, {values[5]}, {values[6]}, {values[7]}}}";
            }
        }

        private class OcSingle
        {
            public int x;
            public int y;
            public int z;
            public TValue value;

            public OcSingle(int x, int y, int z, TValue value)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.value = value;
            }

            public override string ToString()
            {
                return $"{{Single:{x},{y},{z}}}";
            }
        }

        private readonly OcNode _root = new OcNode();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public TValue this[int x, int y, int z]
        {
            get
            {
                TValue value;
                TryGetValue(x, y, z, out value);
                return value;
            }
            set
            {
                SetValue(x, y, z, value);
            }
        }

        public bool TryGetValue(int x, int y, int z, out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                x = (int)((uint)(x << 1) | ((uint)x >> 31));
                y = (int)((uint)(y << 1) | ((uint)y >> 31));
                z = (int)((uint)(z << 1) | ((uint)z >> 31));

                return TryGetValueInternal(x, y, z, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public TValue SetValue(int x, int y, int z, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (value == null && _root == null)
                {
                    return default(TValue);
                }

                x = (int)((uint)(x << 1) | ((uint)x >> 31));
                y = (int)((uint)(y << 1) | ((uint)y >> 31));
                z = (int)((uint)(z << 1) | ((uint)z >> 31));

                var isRemoving = Equals(value, default(TValue));
                var ret = SetValueInternal(_root, x, y, z, value, isRemoving);

                return ret;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private TValue SetValueInternal(OcNode node, int x, int y, int z, TValue value, bool isRemoving)
        {
            var existing = node[x & MASK, y & MASK, z & MASK];

            if (existing == null)
            {
                if (!isRemoving)
                    node[x & MASK, y & MASK, z & MASK] = new OcSingle(x, y, z, value);
                return default(TValue);
            }

            var child = existing as OcNode;
            if (child != null)
            {
                var ret = SetValueInternal(child, x >> SHIFT, y >> SHIFT, z >> SHIFT, value, isRemoving);
                if (child.IsEmpty)
                {
                    node[x & MASK, y & MASK, z & MASK] = null;
                }
                return ret;
            }

            var single = (OcSingle)existing;
            if (Equals(single.value, value))
                return single.value;

            if (single.x != x || single.y != y || single.z != z)
            {
                if (isRemoving)
                    return default(TValue);

                var split = SplitSingle(node, single);
                return SetValueInternal(split, x >> SHIFT, y >> SHIFT, z >> SHIFT, value, false);
            }

            if (isRemoving)
            {
                node[x & MASK, y & MASK, z & MASK] = null;
                return single.value;
            }

            var vv = single.value;
            single.value = value;
            return vv;
        }

        private bool TryGetValueInternal(int x, int y, int z, out TValue value)
        {
            value = default(TValue);

            var node = _root;
            while (true)
            {
                var v = node[x & MASK, y & MASK, z & MASK];
                if (v == null)
                    break;
                var single = v as OcSingle;
                if (single != null)
                {
                    if (single.x == x && single.y == y && single.z == z)
                    {
                        value = single.value;
                        return true;
                    }
                    return false;
                }
                x >>= SHIFT;
                y >>= SHIFT;
                z >>= SHIFT;
                node = v as OcNode;
            }

            return false;
        }

        private OcNode SplitSingle(OcNode node, OcSingle single)
        {
            var child = new OcNode();

            var sx = single.x;
            var sy = single.y;
            var sz = single.z;

            int x = sx >> SHIFT;
            int y = sy >> SHIFT;
            int z = sz >> SHIFT;

            child[x & MASK, y & MASK, z & MASK] = new OcSingle(x, y, z, single.value);
            node[sx & MASK, sy & MASK, sz & MASK] = child;
            return child;
        }

    }
}
