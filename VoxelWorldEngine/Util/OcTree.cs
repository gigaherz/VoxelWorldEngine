using System;
using System.ComponentModel;
using System.Diagnostics;
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

        private OcNode _root;
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

        public TValue SetValue(int x, int y, int z, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (value == null && _root == null)
                {
                    return default(TValue);
                }

                if (_root == null)
                    _root = new OcNode();

                var ret = SetValueInternal(_root, x, y, z, value);

#if CLEAR_ROOT
            if (_root.IsEmpty)
                _root = null;
#endif
                return ret;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private TValue SetValueInternal(OcNode node, int x, int y, int z, TValue value)
        {
            var v = node[x & 1, y & 1, z & 1];
            if (value == null && v == null)
                return default(TValue);

            if (v == null)
            {
                node[x & 1, y & 1, z & 1] = new OcSingle(x, y, z, value);
                return default(TValue);
            }

            var single = v as OcSingle;
            if (single != null)
            {
                if (single.value.Equals(value))
                    return single.value;

                if (single.x == x && single.y == y && single.z == z)
                {
                    if (value == null)
                    {
                        node[x & 1, y & 1, z & 1] = null;
                        return single.value;
                    }

                    var vv = single.value;
                    single.value = value;
                    return vv;
                }

                var node3 = SplitSingle(node, single);

                SetValueInternal(node3, x>>1, y>>1, z>>1, value);
                return default(TValue);
            }

            var node2 = (OcNode)v;
            var ret = SetValueInternal(node2, x>>1, y>>1, z>>1,value);
            if (node2.IsEmpty)
            {
                node[x & 1, y & 1, z & 1] = null;
            }
            return ret;
        }

        private OcNode SplitSingle(OcNode node, OcSingle single)
        {
            var node3 = new OcNode();

            int x = single.x >> 1;
            int y = single.y >> 1;
            int z = single.z >> 1;
            
            node3[x & 1, y & 1, z & 1] = new OcSingle(x,y,z,single.value);
            
            node[single.x & 1, single.y & 1, single.z & 1] = node3;

            return node3;
        }

        public bool TryGetValue(int x, int y, int z, out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                return TryGetValueInternal(x, y, z, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private bool TryGetValueInternal(int x, int y, int z, out TValue value)
        {
            value = default(TValue);

            var node = _root;
            while (node != null)
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
    }
}
