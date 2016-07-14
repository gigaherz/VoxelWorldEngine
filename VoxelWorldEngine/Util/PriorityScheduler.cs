﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VoxelWorldEngine.Util
{
    public class PriorityScheduler : IDisposable
    {
        public static PriorityScheduler Instance = new PriorityScheduler();

        private readonly List<PositionedTask> _tasks = new List<PositionedTask>();
        private readonly Semaphore _awaitTasks = new Semaphore(0, 1);
        private readonly ReaderWriterLockSlim _lockTasks = new ReaderWriterLockSlim();

        private Thread[] _threads;
        public int MaximumConcurrencyLevel { get; set; } = Math.Max(1, Environment.ProcessorCount - 1);
        public int QueuedTaskCount => _tasks.Count;

        Vector3D _lastPlayerPosition;

        int before = Environment.TickCount;

        public void SetPlayerPosition(Vector3D newPosition)
        {
            var difference = newPosition - _lastPlayerPosition;
            var distance = difference.SqrMagnitude;

            if (distance > 5 && (Environment.TickCount - before) >= 1000)
            {
                _lockTasks.EnterWriteLock();
                {
                    Debug.WriteLine("Sorting Tasks...");
                    _tasks.Sort((a, b) => -Math.Sign(a.Score(_lastPlayerPosition) - b.Score(_lastPlayerPosition)));
                }
                _lockTasks.ExitWriteLock();
                _lastPlayerPosition = newPosition;
                before = Environment.TickCount;
            }
        }

        private IEnumerable<PositionedTask> GetNextTask()
        {
            while (true)
            {
                PositionedTask task = null;
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

        protected void QueueTask(PositionedTask task)
        {
            var pos = _lastPlayerPosition;

            _lockTasks.EnterWriteLock();
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
            }
            _lockTasks.ExitWriteLock();

            if (_threads == null)
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
        }

        private void ThreadProc()
        {
            foreach (var t in GetNextTask())
                TryExecuteTask(t);
        }

        int cnt = 0;
        private void TryExecuteTask(PositionedTask task)
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

        public static PositionedTask StartNew(Action action, Vector3D position)
        {
            var t = new PositionedTask(action, position);
            Instance.QueueTask(t);
            return t;
        }

        public class PositionedTask
        {
            public Vector3D Position { get; set; }

            public Action Action { get; }

            public PositionedTask Continuation { get; set; }

            public PositionedTask(Action action, Vector3D position)
            {
                Action = action;
                Position = position;
            }

            public void Run()
            {
                Action();
            }

            int lastScore = -1;
            public int Score(Vector3D vector3D)
            {
                lastScore = (int)Math.Round(((vector3D - Position) * new Vector3D(1,0.25,1)).Magnitude);
                return lastScore;
            }

            public PositionedTask ContinueWith(Action<PositionedTask> action)
            {
                Continuation = new PositionedTask(() =>
                {
                    action(this);
                }, Position);
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
