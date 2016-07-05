using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace VoxelWorldEngine
{
    public static class MouseExtras
    {
#if WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public POINT(Point pt) : this(pt.X, pt.Y) { }

            public static implicit operator Point(POINT p)
            {
                return new Point(p.X, p.Y);
            }

            public static implicit operator POINT(Point p)
            {
                return new POINT(p.X, p.Y);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
        static extern bool ReleaseCapture_Internal();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
#endif

        public static bool HasCapture(GameWindow window)
        {
#if WINDOWS
            return GetCapture() == window.Handle;
#else
            throw new NotImplementedException();
#endif
        }

        public static void SetCapture(GameWindow window)
        {
#if WINDOWS
            SetCapture(window.Handle);
#else
            throw new NotImplementedException();
#endif
        }

        public static void ReleaseCapture()
        {
#if WINDOWS
            ReleaseCapture_Internal();
#else
            throw new NotImplementedException();
#endif
        }

        public static bool IsForeground(GameWindow window)
        {
#if WINDOWS
            return GetForegroundWindow() == window.Handle;
#else
            throw new NotImplementedException();
#endif
        }

        public static Point GetPosition(GameWindow window)
        {
#if WINDOWS
            var p = new POINT();
            if (GetCursorPos(out p))
            {
                ScreenToClient(window.Handle, ref p);
            }
            return p;
#else
            throw new NotImplementedException();
#endif
        }

        public static void SetPosition(GameWindow window, int x, int y)
        {
#if WINDOWS
            POINT p = new POINT(x,y);
            ClientToScreen(window.Handle, ref p);
            SetCursorPos(p.X, p.Y);
#else
            throw new NotImplementedException();
#endif
        }
    }
}
