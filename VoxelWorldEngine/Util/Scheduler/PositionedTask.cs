using System;
using System.Threading.Tasks;
using VoxelWorldEngine.Maths;

namespace VoxelWorldEngine.Util.Scheduler
{
    public class PositionedTask : PriorityTask
    {
        public EntityPosition Position { get; set; }

        public PositionedTask(Action action, PriorityClass priorityClass, EntityPosition position)
            : base(action, priorityClass, 0)
        {
            Position = position;
        }

        public override void UpdatePriority(EntityPosition other)
        {
            Priority = (int)Math.Round(other.RelativeTo(Position).Length());
        }
    }

    public class PositionedTask<T> : PriorityTask<T>
    {
        public EntityPosition Position { get; set; }

        public PositionedTask(Func<T> action, PriorityClass priorityClass, EntityPosition position)
            : base(action, priorityClass, 0)
        {
            Position = position;
        }

        public override void UpdatePriority(EntityPosition other)
        {
            Priority = (int)Math.Round(other.RelativeTo(Position).LengthSquared());
        }
    }
}
