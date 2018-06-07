using System.Collections.Generic;
using System.Threading;

namespace VoxelWorldEngine.Util
{
    public class StatManager
    {
        public class Counter
        {
            private int _value = 0;

            public int Value => _value;

            public void Increment() { Interlocked.Increment(ref _value); }
            public void Add(int count) { Interlocked.Add(ref _value, count); }
            public void Reset() { _value = 0;  }

            public static Counter operator++(Counter counter)
            {
                counter.Increment();
                return counter;
            }
        }

        public static readonly StatManager Global = new StatManager();
        public static readonly StatManager PerFrame = new StatManager();

        private readonly Dictionary<string, Counter> _counters = new Dictionary<string, Counter>();

        public Counter this[string key] => GetCounter(key);

        public void Increment(string key)
        {
            GetCounter(key).Increment();
        }

        public void Add(string key, int count)
        {
            GetCounter(key).Add(count);
        }

        public Counter GetCounter(string key)
        {
            if (!_counters.TryGetValue(key, out var counter))
            {
                _counters.Add(key, counter = new Counter());
            }

            return counter;
        }

        public void Reset(string key)
        {
            if (_counters.TryGetValue(key, out var counter))
            {
                counter.Reset();
            }
        }

        public void Reset()
        {
            foreach (var counter in _counters.Values)
                counter.Reset();
        }

        private StatManager()
        {
        }
    }
}
