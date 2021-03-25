using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace VoxelWorldEngine.Util.Performance
{
    public class BasicProfiler : IProfiler
    {
        private readonly ThreadLocal<ThreadInfo> CurrentThreadInfo;
        private readonly ConcurrentDictionary<string, Node> Nodes = new ConcurrentDictionary<string, Node>();

        private readonly Node RootNode = new Node("ROOT");

        public BasicProfiler()
        {
            CurrentThreadInfo = new ThreadLocal<ThreadInfo>(() => new ThreadInfo(RootNode), true);
        }

        public IProfilerFrame Begin(string nodeName)
        {
            var node = Nodes.GetOrAdd(nodeName, name => new Node(name));
            var ti = CurrentThreadInfo.Value;
            //ti.Hold();
            var frame = ti.PushAndStart((Node)node);
            //ti.Unhold();
            return frame;
        }

        public IProfilerFrame BeginThread()
        {
            var ti = CurrentThreadInfo.Value;
            ti.Root.Start();
            return ti.Root;
        }

        public void Close()
        {
            using (var stream = new FileStream($"profile-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}.log", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream))
                {
                    long totalTimeRoot = RootNode.Frames.Sum(f => f.TotalTime.ElapsedTicks) / 10;

                    var nodes = Nodes.Values.ToList();

                    writer.WriteLine("Nodes: ");
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        bool isLast = (i + 1) == nodes.Count;
                        Node node = nodes[i];
                        long totalTimeUS = node.Frames.Sum(f => f.TotalTime.ElapsedTicks) / 10;
                        long selfTimeUS = node.Frames.Sum(f => f.SelfTime.ElapsedTicks) / 10;
                        int callCount = node.Frames.Sum(f => f.CallCount);
                        int percentOfRoot = (int)(selfTimeUS * 100 / totalTimeRoot);
                        if (isLast)
                            writer.Write("  └ ");
                        else
                            writer.Write("  ├ ");
                        writer.Write($"{node.Name}: {percentOfRoot}%; Time: {totalTimeUS} us: Self: {selfTimeUS} us; Calls: {callCount}");
                        if (callCount < 2)
                        {
                            writer.WriteLine();
                        }
                        else
                        {
                            long totalTimeUSPC = totalTimeUS / callCount;
                            long selfTimeUSPC = selfTimeUS  / callCount;
                            writer.WriteLine($"; Per Call: {totalTimeUSPC} us; Self: {selfTimeUSPC} us");
                        }
                    }

                    foreach (var value in CurrentThreadInfo.Values)
                    {
                        writer.WriteLine($"Thread {value.ThreadId}: {value.ThreadName}");
                        writer.Write("  ");
                        DumpFrame(writer, value.Root, 100, 100, true);
                        DumpStack("  ", writer, value.Root, value.Root);
                    }
                }
            }
        }

        private static (int,int) DumpStack(String prefix, StreamWriter writer, Frame parent, Frame root)
        {
            int percentOfRootAcc = 0;
            int percentOfParentAcc = 0;
            List<Frame> values = parent.ChildFrames.Values.ToList();
            for (int j = 0; j < values.Count; j++)
            {
                bool isLast = (j + 1) == values.Count;
                Frame frame = values[j];

                writer.Write(prefix);
                if (isLast)
                    writer.Write("└ ");
                else
                    writer.Write("├ ");

                int percentOfRoot = (int)(frame.TotalTime.ElapsedTicks * 100 / root.TotalTime.ElapsedTicks);
                int percentOfParent = (int)(frame.TotalTime.ElapsedTicks * 100 / parent.TotalTime.ElapsedTicks);
                DumpFrame(writer, frame, percentOfRoot, percentOfParent, ReferenceEquals(parent, root));

                if (frame.ChildFrames.Count > 0)
                {
                    var prefix2 = prefix + (isLast ? "    " : "│   ");
                    var (percentOfRootCh, percentOfParentCh) = DumpStack(prefix2, writer, frame, root);
                    if (frame.ChildFrames.Count > 1)
                    {
                        writer.Write(prefix2);
                        writer.WriteLine($"Accounted: {percentOfRootCh}% ({percentOfParentCh}%)");
                    }
                }

                percentOfParentAcc += percentOfParent;
                percentOfRootAcc += percentOfRoot;
            }

            return (percentOfRootAcc, percentOfParentAcc);
        }

        private static void DumpFrame(StreamWriter writer, Frame frame, long percentOfRoot, long percentOfParent, bool isChildOfRoot)
        {
            long totalTimeUS = frame.TotalTime.ElapsedTicks / 10;
            long selfTimeUS = frame.SelfTime.ElapsedTicks / 10;
            writer.Write($"{frame.Node.Name}: {percentOfRoot}%");
            if (!isChildOfRoot)
                writer.Write($" ({percentOfParent}%)");
            writer.Write($"; Time: {totalTimeUS} us; Self: {selfTimeUS} us; Calls: {frame.CallCount}");
            if (frame.CallCount < 2)
            {
                writer.WriteLine();
            }
            else
            {
                long totalTimeUSPC = totalTimeUS / frame.CallCount;
                long selfTimeUSPC = selfTimeUS / frame.CallCount;
                writer.WriteLine($"; Per Call: {totalTimeUSPC} us; Self: {selfTimeUSPC} us");
            }
        }

        public class ThreadInfo
        {
            public int ThreadId { get; } = Thread.CurrentThread.ManagedThreadId;
            public string ThreadName { get; } = Thread.CurrentThread.Name;
            public Stack<Frame> FrameStack { get; } = new Stack<Frame>();
            public Frame Root { get; }

            public ThreadInfo(Node root)
            {
                Root = new Frame(root, this);
                Root.Start();
                FrameStack.Push(Root);
            }

            /// <summary>
            /// Pauses counting, but doesn't increment the call count.
            /// </summary>
            internal void Hold()
            {
                foreach (var frame in FrameStack) frame.Hold();
            }

            internal void Unhold()
            {
                foreach (var frame in FrameStack) frame.Unhold();
            }

            internal Frame PushAndStart(Node node)
            {
                var top = FrameStack.Peek();

                if (!top.ChildFrames.TryGetValue(node, out var frame))
                {
                    frame = new Frame(node, this);
                    top.ChildFrames.Add(node, frame);
                }

                top.Pause();
                FrameStack.Push(frame);
                frame.Start();
                return frame;
            }

            internal void StopAndPop(Frame frame)
            {
                var removed = FrameStack.Pop();
                if (removed != frame)
                {
                    throw new Exception($"Profiler stack mismatch! {removed.Node} != {frame.Node}!");
                }
                removed.Stop();
                if (FrameStack.Count > 0)
                {
                    var top = FrameStack.Peek();
                    top.Resume();
                }
            }
        }

        public class Node
        {
            public string Name { get; }

            public ConcurrentBag<Frame> Frames { get; } = new ConcurrentBag<Frame>();

            internal Node(string name)
            {
                Name = name;
            }

            public override string ToString()
            {
                return $"{{Node: {Name}}}";
            }
        }

        public class Frame : IProfilerFrame
        {
            public Node Node { get; }

            public Dictionary<Node, Frame> ChildFrames { get; } = new Dictionary<Node, Frame>();

            public ThreadInfo ThreadInfo { get; }

            public int CallCount { get; private set; }
            public Stopwatch SelfTime { get; } = new Stopwatch();
            public Stopwatch TotalTime { get; } = new Stopwatch();

            public Frame(Node node, ThreadInfo ti)
            {
                Node = node;
                ThreadInfo = ti;
                Node.Frames.Add(this);
            }

            public void Dispose()
            {
                ThreadInfo.StopAndPop(this);
            }

            private bool _started = false;
            private bool _paused = false;

            internal void Start()
            {
                CallCount++;
                SelfTime.Start();
                TotalTime.Start();
                _started = true;
            }

            internal void Pause()
            {
                SelfTime.Stop();
                _paused = true;
            }

            internal void Resume()
            {
                SelfTime.Start();
                _paused = false;
            }

            internal void Stop()
            {
                SelfTime.Stop();
                TotalTime.Stop();
                _started = false;
            }

            internal void Hold()
            {
                if (_started && !_paused)
                    SelfTime.Stop();
            }

            internal void Unhold()
            {
                if (_started && !_paused)
                    SelfTime.Start();
            }
        }
    }
}
