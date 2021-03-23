using System;
using System.Diagnostics;
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

