using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VoxelWorldEngine
{
    public class MouseExtras
    {
        public static readonly MouseExtras Instance;

        static MouseExtras() {
#if WINDOWS
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Instance = new MouseExtrasWindows();
            }
            else
#endif
            {
                Instance = new MouseExtras();
            }
        }

        bool fakeCapture = false;
        public virtual bool HasCapture(Game game, GameWindow window)
        {
            if (!IsForeground(game, window))
                fakeCapture = false;
            return fakeCapture;
        }

        public virtual void SetCapture(GameWindow window)
        {
            fakeCapture = true;
        }

        public virtual void ReleaseCapture()
        {
            fakeCapture = false;
        }

        public virtual bool IsForeground(Game game, GameWindow window)
        {
            return game.IsActive;
        }

        public virtual Point GetPosition(GameWindow window)
        {
            return Mouse.GetState(window).Position;
        }

        public virtual void SetPosition(GameWindow window, int x, int y)
        {
            Mouse.SetPosition(x,y);
        }
    }

    public class MouseExtrasWindows : MouseExtras
    {
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

        public override bool HasCapture(Game game, GameWindow window)
        {
            return GetCapture() == window.Handle;
        }

        public override void SetCapture(GameWindow window)
        {
            SetCapture(window.Handle);
        }

        public override void ReleaseCapture()
        {
            ReleaseCapture_Internal();
        }

        public override bool IsForeground(Game game, GameWindow window)
        {
            return GetForegroundWindow() == window.Handle;
        }

        public override Point GetPosition(GameWindow window)
        {
            var p = new POINT();
            if (GetCursorPos(out p))
            {
                ScreenToClient(window.Handle, ref p);
            }
            return p;
        }

        public override void SetPosition(GameWindow window, int x, int y)
        {
            var p = new POINT(x,y);
            ClientToScreen(window.Handle, ref p);
            SetCursorPos(p.X, p.Y);
        }
    }
}
