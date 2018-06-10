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
        private readonly Semaphore _awaitTasks = new Semaphore(0, 1);
        private readonly ReaderWriterLockSlim _lockTasks = new ReaderWriterLockSlim();

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
                _lockTasks.EnterWriteLock();
                {
                    foreach(var task in _tasks)
                        task.UpdatePriority(_lastPlayerPosition);
                    _tasks.Sort((a, b) => -Math.Sign(a.Priority - b.Priority));
                }
                _lockTasks.ExitWriteLock();
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
                    _lockTasks.EnterReadLock();
                    {
                        bool lockAcquired = _awaitTasks.WaitOne(0);
                        if (!lockAcquired)
                        {
                            _lockTasks.ExitReadLock();
                            lockAcquired = _awaitTasks.WaitOne();
                            _lockTasks.EnterReadLock();
                        }

                        if (lockAcquired)
                        {
                            task = _tasks[_tasks.Count - 1];
                            _tasks.RemoveAt(_tasks.Count-1);
                            if (_tasks.Count > 0)
                            {
                                _awaitTasks.Release();
                            }
                        }
                    }
                    _lockTasks.ExitReadLock();
                }
                catch (ObjectDisposedException)
                {
                    yield break;
                }
                catch (AbandonedMutexException)
                {
                    yield break;
                }
                yield return task;
            }
        }

        protected void QueueTask(PriorityTask task)
        {
            var pos = _lastPlayerPosition;
            task.UpdatePriority(pos);

            _lockTasks.EnterWriteLock();
            {
                var score = task.Priority;
                bool inserted = false;

                if (_tasks.Count > 0)
                {
                    if (score > _tasks[0].Priority)
                    {
                        inserted = true;
                        _tasks.Insert(0, task);
                    }
                    else if (score > _tasks[_tasks.Count - 1].Priority)
                    {
                        int left = 0;
                        int right = _tasks.Count - 1;
                        while (right > left)
                        {
                            var center = (left + right) / 2;
                            if (center == left)
                            {
                                inserted = true;
                                _tasks.Insert(right, task);
                                break;
                            }

                            var centerScore = _tasks[center].Priority;
                            if (score == centerScore)
                            {
                                if (right == center + 1)
                                {
                                    inserted = true;
                                    _tasks.Insert(right, task);
                                    break;
                                }
                                right = center + 1;
                            }
                            else if (score > centerScore)
                            {
                                right = center;
                            }
                            else
                            {
                                left = center;
                            }
                        }
                    }
                }

                if (!inserted) _tasks.Add(task);

                if (_tasks.Count == 1)
                {
                    _awaitTasks.Release();
                }
            }
            _lockTasks.ExitWriteLock();
        }

        private void ThreadProc()
        {
            foreach (var t in GetNextTask())
                TryExecuteTask(t);
        }

        int cnt = 0;
        private void TryExecuteTask(PriorityTask task)
        {
            try
            {
                task.Run();
                if (task.Continuation != null)
                    QueueTask(task.Continuation);
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

            public PriorityTask Continuation { get; set; }

            public int Priority { get; set; }

            public PriorityTask(Action action, int priority)
            {
                Action = action;
                Priority = priority;
            }

            public void Run()
            {
                Action();
            }

            public PriorityTask ContinueWith(Action<PriorityTask> action)
            {
                Continuation = new PriorityTask(() =>
                {
                    action(this);
                }, Priority);
                return Continuation;
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
                Continuation = continuation;
                return continuation;
            }
        }

        public void Dispose()
        {
            _awaitTasks.Dispose();
        }
    }
}
