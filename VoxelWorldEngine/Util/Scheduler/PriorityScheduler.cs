using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VoxelWorldEngine.Maths;
using VoxelWorldEngine.Util.Performance;

namespace VoxelWorldEngine.Util.Scheduler
{
    public partial class PriorityScheduler : IDisposable
    {
        public static PriorityScheduler Instance = new PriorityScheduler();

        private readonly List<PriorityTaskBase>[] _tasks;
        private readonly AutoResetEvent _awaitTasks = new AutoResetEvent(false);

        private Thread[] _threads;
        public int MaximumConcurrencyLevel { get; set; } = 2 * Math.Max(1, Environment.ProcessorCount - 1);
        public int QueuedTaskCount => _tasks.Sum(t => t.Count);

        EntityPosition _lastPlayerPosition;

        int _oldTickCount = Environment.TickCount;

        private PriorityScheduler()
        {
            _tasks = new List<PriorityTaskBase>[Enum.GetValues(typeof(PriorityClass)).Cast<PriorityClass>().Max(v => (int)v) + 1];
            for (int i = 0; i < _tasks.Length; i++)
                _tasks[i] = new List<PriorityTaskBase>();

            _threads = new Thread[MaximumConcurrencyLevel];
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(ThreadProc)
                {
                    Name = $"Priority Scheduler Thread #{i}",
                    Priority = ThreadPriority.Lowest,
                    IsBackground = true
                };
                _threads[i].Start();
            }
        }

        public void SetPlayerPosition(EntityPosition newPosition)
        {
            using (Profiler.CurrentProfiler.Begin("Updating Task Priorities"))
            {
                var difference = newPosition.RelativeTo(_lastPlayerPosition);
                var distance = difference.Length();
                
                int tickCount = Environment.TickCount;

                if (distance > 1 && tickCount - _oldTickCount >= 1000)
                {
                    Debug.WriteLine($"Updating Task Priorities... {newPosition}");
                    lock (_tasks)
                    {
                        for (int i = 0; i < _tasks.Length; i++)
                        {
                            foreach (var task in _tasks[i])
                                task.UpdatePriority(_lastPlayerPosition);
                        }
                    }
                    _lastPlayerPosition = newPosition;
                    _oldTickCount = tickCount;
                }
            }
        }

        private IEnumerable<PriorityTaskBase> GetNextTask()
        {
            while (true)
            {
                PriorityTaskBase task = null;
                using (Profiler.CurrentProfiler.Begin("Awaiting Tasks"))
                {
                    try
                    {
                        _awaitTasks.WaitOne();
                        lock (_tasks)
                        {
                            for (int i = 0; i < _tasks.Length; i++)
                            {
                                var list = _tasks[i];
                                if (list.Count > 0)
                                {
                                    int minIndex = -1;
                                    int minPriority = -1;
                                    for (int j = 0; j < list.Count; j++)
                                    {
                                        var t = list[j];
                                        int p = t.Priority;
                                        if (p < minPriority || task == null)
                                        {
                                            minIndex = j;
                                            task = t;
                                            minPriority = p;
                                        }
                                    }
                                     if (task != null)
                                    {
                                        if (minIndex != list.Count - 1)
                                        {
                                            list[minIndex] = list[list.Count - 1];
                                        }
                                        list.RemoveAt(list.Count - 1);
                                        break;
                                    }
                                }
                            }
                            if (_tasks.Sum(t => t.Count) > 0)
                            {
                                _awaitTasks.Set();
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        yield break;
                    }
                    catch (AbandonedMutexException)
                    {
                        yield break;
                    }
                }
                if (task != null)
                    yield return task;
            }
        }

        protected void QueueTask(PriorityTaskBase task)
        {
            try
            {
                task.UpdatePriority(_lastPlayerPosition);

                lock (_tasks)
                {
                    _tasks[(int)task.PriorityClass].Add(task);
                    _awaitTasks.Set();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Oops! " + e);
            }
        }

        private void ThreadProc()
        {
            using (Profiler.CurrentProfiler.BeginThread())
            {
                foreach (var t in GetNextTask())
                    TryExecuteTask(t);
            }
        }

        private void TryExecuteTask(PriorityTaskBase task)
        {
            try
            {
                using (Profiler.CurrentProfiler.Begin("Running Task"))
                {
                    task.Run();
                }
            }
            catch (Exception e)
            {
                // ignored
                Debug.Write(e);
            }
        }

        public static PriorityTask Schedule(Action action, PriorityClass priorityClass, int priority)
        {
            var t = new PriorityTask(action, priorityClass, priority);
            Instance.QueueTask(t);
            return t;
        }

        public static PriorityTask<T> Schedule<T>(Func<T> action, PriorityClass priorityClass, int priority)
        {
            var t = new PriorityTask<T>(action, priorityClass, priority);
            Instance.QueueTask(t);
            return t;
        }

        public static PositionedTask Schedule(Action action, PriorityClass priorityClass, EntityPosition position)
        {
            var t = new PositionedTask(action, priorityClass, position);
            Instance.QueueTask(t);
            return t;
        }

        public static PositionedTask<T> Schedule<T>(Func<T> action, PriorityClass priorityClass, EntityPosition position)
        {
            var t = new PositionedTask<T>(action, priorityClass, position);
            Instance.QueueTask(t);
            return t;
        }

        protected virtual void Dispose(bool whatever)
        {
            _awaitTasks.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
