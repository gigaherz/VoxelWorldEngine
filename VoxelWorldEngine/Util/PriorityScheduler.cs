using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util
{
    public class PriorityScheduler : IDisposable
    {
        public static PriorityScheduler Instance = new PriorityScheduler();

        private readonly List<PriorityTask> _tasks = new List<PriorityTask>();
        private readonly AutoResetEvent _awaitTasks = new AutoResetEvent(false);
        

        private Thread[] _threads;
        public int MaximumConcurrencyLevel { get; set; } = Math.Max(1, Environment.ProcessorCount - 1);
        public int QueuedTaskCount => _tasks.Count;

        EntityPosition _lastPlayerPosition;

        int before = Environment.TickCount;

        private PriorityScheduler()
        {
            _threads = new Thread[MaximumConcurrencyLevel];
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(ThreadProc)
                {
                    Name = string.Format("PriorityScheduler: ", i),
                    Priority = ThreadPriority.Lowest,
                    IsBackground = true
                };
                _threads[i].Start();
            }
        }

        public void SetPlayerPosition(EntityPosition newPosition)
        {
            var difference = newPosition.RelativeTo(_lastPlayerPosition);
            var distance = difference.Length();

            if (distance > 5 && Environment.TickCount - before >= 1000)
            {
                lock(_tasks)
                {
                    foreach(var task in _tasks)
                        task.UpdatePriority(_lastPlayerPosition);
                }
                _lastPlayerPosition = newPosition;
                before = Environment.TickCount;
            }
        }

        private IEnumerable<PriorityTask> GetNextTask()
        {
            while (true)
            {
                PriorityTask task = null;
                try
                {
                    _awaitTasks.WaitOne();
                    lock (_tasks)
                    {
                        if (_tasks.Count > 0)
                        {
                            int minIndex = int.MaxValue;
                            int minPriority = int.MaxValue;
                            for (int i = 0; i < _tasks.Count; i++)
                            {
                                var t = _tasks[i];
                                int p = t.Priority;
                                if (p < minPriority || task == null)
                                {
                                    minIndex = i;
                                    task = t;
                                    minPriority = p;
                                }
                            }
                            _tasks[minIndex] = _tasks[_tasks.Count - 1];
                            _tasks.RemoveAt(_tasks.Count - 1);
                            if (_tasks.Count > 0)
                            {
                                _awaitTasks.Set();
                            }
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
                if (task != null)
                    yield return task;
            }
        }

        protected void QueueTask(PriorityTask task)
        {
            var pos = _lastPlayerPosition;
            task.UpdatePriority(pos);

            lock(_tasks)
            {
                _tasks.Add(task);
                _awaitTasks.Set();
            }
        }

        private void ThreadProc()
        {
            foreach (var t in GetNextTask())
                TryExecuteTask(t);
        }

        private void TryExecuteTask(PriorityTask task)
        {
            try
            {
                task.Run();
                foreach (var cont in task.Continuations)
                    QueueTask(cont);
            }
            catch (Exception e)
            {
                // ignored
                Debug.Write(e);
            }
        }

        public static PriorityTask StartNew(Action action, int priority)
        {
            var t = new PriorityTask(action, priority);
            Instance.QueueTask(t);
            return t;
        }

        public static PositionedTask StartNew(Action action, EntityPosition position)
        {
            var t = new PositionedTask(action, position);
            Instance.QueueTask(t);
            return t;
        }

        public class PriorityTask
        {
            public Action Action { get; }

            public List<PriorityTask> Continuations { get; } = new List<PriorityTask>();

            public event Action<PriorityTask> OnCompleted;

            public int Priority { get; set; }

            public PriorityTask(Action action, int priority)
            {
                Action = action;
                Priority = priority;
            }

            public void Run()
            {
                Action();
                OnCompleted?.Invoke(this);
            }

            public PriorityTask ContinueWith(Action<PriorityTask> action)
            {
                var cont = new PriorityTask(() =>
                {
                    action(this);
                }, Priority);
                Continuations.Add(cont);
                return cont;
            }

            public virtual void UpdatePriority(EntityPosition watcher)
            {
            }

            public override string ToString()
            {
                return $"{{Priority: {Priority}}}";
            }
        }

        public class PositionedTask : PriorityTask
        {
            public EntityPosition Position { get; set; }

            public PositionedTask(Action action, EntityPosition position)
                : base(action, -1)
            {
                Position = position;
            }

            public override void UpdatePriority(EntityPosition other)
            {
                Priority = (int)Math.Round(other.RelativeTo(Position).LengthSquared());
            }

            public PositionedTask ContinueWith(Action<PositionedTask> action)
            {
                var continuation = new PositionedTask(() =>
                {
                    action(this);
                }, Position);
                Continuations.Add(continuation);
                return continuation;
            }
        }

        public void Dispose()
        {
            _awaitTasks.Dispose();
        }
    }
}
