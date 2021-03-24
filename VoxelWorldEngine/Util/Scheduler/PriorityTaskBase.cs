using System;
using System.Threading.Tasks;
using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util.Scheduler
{
    public abstract class PriorityTaskBase
    {
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

        public PriorityTask(Action action, PriorityClass priorityClass, int priority)
            : base(priorityClass, priority)
        {
            _action = action;
        }

        public override void Run()
        {
            _action();
        }

        public override string ToString()
        {
            return $"{{Priority: {Priority}}}";
        }
    }

    public class PriorityTask<T> : PriorityTaskBase
    {
        private Func<T> _action;

        public PriorityTask(Func<T> action, PriorityClass priorityClass, int priority)
            : base(priorityClass, priority)
        {
            _action = action;
        }
        public override void Run()
        {
            T result = _action();
        }

    }
}
