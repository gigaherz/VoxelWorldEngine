using System;
using System.Collections.Generic;
using VoxelWorldEngine.Terrain;

namespace VoxelWorldEngine.Util
{
    public class TaskInProgress : IDisposable
    {
        //private static readonly List<TaskInProgress> TasksList = new List<TaskInProgress>();
        private readonly string _taskName;
        private readonly object _owner;
        private bool disposed = false;

        public TaskInProgress(string taskName, object owner)
        {
            _taskName = taskName;
            _owner = owner;
            //lock (TasksList) TasksList.Add(this);
        }

        public void Dispose()
        {
            End();
        }

        public virtual bool End()
        {
            if (disposed) return false;
            disposed = true;
            //lock (TasksList) TasksList.Remove(this);
            return true;
        }

        public override string ToString()
        {
            return $"{_taskName} / {_owner}";
        }
    }
}