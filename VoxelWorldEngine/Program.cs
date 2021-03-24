using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using VoxelWorldEngine.Util.Performance;
#if OPENGL && DEBUG
using OpenTK.Graphics.OpenGL;
#endif

namespace VoxelWorldEngine
{
    static class Program
    {
#if OPENGL && DEBUG
        private static OpenGLNatives.DebugProc openGLDebugDelegate;
#endif

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Thread.CurrentThread.Name = "Main Thread";
            using (VoxelGame game = new VoxelGame())
            {
                EnableDebugOutput();

                using (Profiler.CurrentProfiler.BeginThread())
                {
                    game.Run();
                }
                Profiler.CurrentProfiler.Close();
            }
            lock (fileWriter)
            {
                _canWrite = false;
            }
            fileWriter.Close();
        }

        private static bool _canWrite = true;
        private static readonly StreamWriter fileWriter = new StreamWriter(new FileStream($"debug-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}.log", FileMode.Create, FileAccess.Write, FileShare.None));

        internal static void DebugWriteLine(string v)
        {
            if (_canWrite)
            {
                lock (fileWriter)
                {
                    if (_canWrite)
                    {
                        fileWriter.WriteLine($"[{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss.ffff")}]: {v}");
                        fileWriter.Flush();
                    }
                }
            }
        }


        public static void EnableDebugOutput()
        {
#if OPENGL && DEBUG
            OpenGLNatives.glEnable((int)EnableCap.DebugOutput);
            OpenGLNatives.glEnable((int)EnableCap.DebugOutputSynchronous);

            openGLDebugDelegate = OpenGLDebugCallback;

            OpenGLNatives.glDebugMessageCallback(openGLDebugDelegate, IntPtr.Zero);
            OpenGLNatives.glDebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityLow, 0, new uint[0], true);

            //OpenGLNatives.glDebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif
        }

#if OPENGL && DEBUG
        private static void OpenGLDebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr param)
        {
            if (severity < DebugSeverity.DebugSeverityLow)
                return;

            string messageStr = Marshal.PtrToStringAnsi(message, length);
            Debug.WriteLine($"[OpenGL]: message={messageStr}, source={source}, type={type}, id={id}, severity={severity}");
        }
#endif

        private static class OpenGLNatives
        {
#if OPENGL && DEBUG
            [DllImport("opengl32.dll")]
            public static extern void glEnable(uint cap);

            [DllImport("opengl32.dll")]
            public static extern IntPtr wglGetProcAddress(string name);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate void pglDebugMessageCallback(DebugProc callback, IntPtr userParam);
            public static pglDebugMessageCallback glDebugMessageCallback =
                (pglDebugMessageCallback) Marshal.GetDelegateForFunctionPointer(wglGetProcAddress("glDebugMessageCallback"), typeof(pglDebugMessageCallback));

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate void pglDebugMessageControl(DebugSourceControl source, DebugTypeControl type, DebugSeverityControl severity, int count, uint[] ids, bool enabled);
            public static pglDebugMessageControl glDebugMessageControl =
                (pglDebugMessageControl)Marshal.GetDelegateForFunctionPointer(wglGetProcAddress("glDebugMessageControl"), typeof(pglDebugMessageControl));

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate void pglDebugMessageInsert(DebugSourceExternal source, DebugType type, int id, DebugSeverity severity, int length, string buf);
            public static pglDebugMessageInsert glDebugMessageInsert =
                (pglDebugMessageInsert)Marshal.GetDelegateForFunctionPointer(wglGetProcAddress("glDebugMessageInsert"), typeof(pglDebugMessageInsert));

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate void DebugProc(
                DebugSource source, DebugType type, int id,
                DebugSeverity severity, int length, IntPtr message,
                IntPtr userParam);

#endif
        }
    }
}

