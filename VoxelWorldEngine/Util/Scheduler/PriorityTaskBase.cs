using System;
using System.Threading.Tasks;
using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util.Scheduler
{
    public abstract class PriorityTaskBase
    {
        public Task Task { get; protected set; }

        public PriorityClass PriorityClass { get; }
        public int Priority { get; set; }


        public PriorityTaskBase(PriorityClass priorityClass, int priority)
        {
            PriorityClass = priorityClass;
            Priority = priority;
        }

        public abstract void Run();

        public virtual void UpdatePriority(EntityPosition watcher)
        {
        }

        public override string ToString()
        {
            return $"{{Priority: {Priority}}}";
        }
    }

    public class PriorityTask : PriorityTaskBase
    {
        private Action _action;
        private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();

        public PriorityTask(Action action, PriorityClass priorityClass, int priority)
            : base(priorityClass, priority)
        {
            _action = action;
            Task = _completionSource.Task;
        }

        public override void Run()
        {
            _action();
            _completionSource.TrySetResult(null);
        }

        public override string ToString()
        {
            return $"{{Priority: {Priority}}}";
        }
    }

    public class PriorityTask<T> : PriorityTaskBase
    {
        private Func<T> _action;
        private readonly TaskCompletionSource<T> _completionSource = new TaskCompletionSource<T>();

        public new Task<T> Task => _completionSource.Task;

        public PriorityTask(Func<T> action, PriorityClass priorityClass, int priority)
            : base(priorityClass, priority)
        {
            _action = action;
            base.Task = _completionSource.Task;
        }
        public override void Run()
        {
            T result = _action();
            _completionSource.TrySetResult(result);
        }

    }
}
