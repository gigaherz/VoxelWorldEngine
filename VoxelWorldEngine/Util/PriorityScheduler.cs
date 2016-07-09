using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VoxelWorldEngine.Util
{
    public class PriorityScheduler : IDisposable
    {
        public static PriorityScheduler Instance = new PriorityScheduler();

        private readonly List<PhasedTask> _tasks = new List<PhasedTask>();
        private readonly Semaphore _awaitTasks = new Semaphore(0, 1);
        private readonly Mutex _lockTasks = new Mutex();

        private Thread[] _threads;
        public int MaximumConcurrencyLevel { get; set; } = Math.Max(1, Environment.ProcessorCount - 1);

        Vector3D _lastPlayerPosition;

        public void SetPlayerPosition(Vector3D newPosition)
        {
            var difference = newPosition - _lastPlayerPosition;
            var distance = difference.SqrMagnitude;

            if (distance > 5)
            {
                if (_lockTasks.WaitOne())
                {
                    Debug.WriteLine("Sorting...");
                    _tasks.Sort((a, b) => -Math.Sign(a.Score(_lastPlayerPosition) - b.Score(_lastPlayerPosition)));
                    _lockTasks.ReleaseMutex();
                }
                _lastPlayerPosition = newPosition;
            }
        }

        private IEnumerable<PhasedTask> GetNextTask()
        {
            while (true)
            {
                PhasedTask task = null;
                try
                {
                    if (_lockTasks.WaitOne())
                    {
                        bool lockAcquired = _awaitTasks.WaitOne(0);
                        if (!lockAcquired)
                        {
                            _lockTasks.ReleaseMutex();
                            lockAcquired = _awaitTasks.WaitOne();
                            _lockTasks.WaitOne();
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
                        _lockTasks.ReleaseMutex();
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
                yield return task;
            }
        }

        protected void QueueTask(PhasedTask task)
        {
            var pos = _lastPlayerPosition;

            if (_lockTasks.WaitOne())
            {
                var score = task.Score(pos);
                bool inserted = false;

                if (_tasks.Count > 0)
                {
                    if (score > _tasks[0].Score(pos))
                    {
                        inserted = true;
                        _tasks.Insert(0, task);
                    }
                    else if (score > _tasks[_tasks.Count - 1].Score(pos))
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

                            var centerScore = _tasks[center].Score(pos);
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
                _lockTasks.ReleaseMutex();
            }

            if (_threads == null)
            {
                _threads = new Thread[MaximumConcurrencyLevel];
                for (int i = 0; i < _threads.Length; i++)
                {
                    (_threads[i] = new Thread(() =>
                    {
                        foreach (var t in GetNextTask())
                            TryExecuteTask(t);
                    })
                    {
                        Name = string.Format("PriorityScheduler: ", i),
                        Priority = ThreadPriority.Lowest,
                        IsBackground = true
                    }).Start();
                }
            }
        }

        int cnt = 0;
        private void TryExecuteTask(PhasedTask task)
        {
            try
            {
                task.Run();
                if (task.Continuation != null)
                    QueueTask(task.Continuation);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static PhasedTask StartNew(Action action, Vector3D position, int phase)
        {
            var t = new PhasedTask(action, position, phase);
            Instance.QueueTask(t);
            return t;
        }

        public class PhasedTask
        {
            public int Phase { get; set; }
            public Vector3D Position { get; set; }

            public Action Action { get; }

            public PhasedTask Continuation { get; set; }

            public PhasedTask(Action action, Vector3D position, int phase)
            {
                Action = action;
                Position = position;
                Phase = phase;
            }

            public void Run()
            {
                Action();
            }

            int lastScore = -1;
            public int Score(Vector3D vector3D)
            {
                lastScore = (int)Math.Round(((vector3D - Position) * new Vector3D(1,0.25,1)).Magnitude) + Phase * 10;
                return lastScore;
            }

            public PhasedTask ContinueWith(Action<PhasedTask> action)
            {
                Continuation = new PhasedTask(() =>
                {
                    action(this);
                }, Position, Phase);
                return Continuation;
            }

            public override string ToString()
            {
                return $"{{Last computed score:{lastScore}}}";
            }
        }

        public void Dispose()
        {
            _awaitTasks.Dispose();
        }
    }
}
