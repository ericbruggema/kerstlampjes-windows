using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kerstlampjes
{
    static class Native
    {
        public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
            WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage,
            out IntPtr bits, IntPtr section, uint offset);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    }

    /// <summary>
    /// Een doorzichtig venster over (het werkgebied van) één scherm: klikt erdoorheen, staat bovenop,
    /// krijgt nooit focus en komt niet in de taakbalk of Alt-Tab.
    /// </summary>
    public class Overlay : Form
    {
        readonly LightsRenderer renderer = new LightsRenderer();
        IntPtr memDC, dib, oldObj;
        Bitmap bmp;
        Graphics gfx;
        int bw, bh;
        byte alpha = 255;

        public Overlay(Rectangle bounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            bw = bounds.Width; bh = bounds.Height;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW |
                              Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        public bool Animated { get { return renderer.Animated; } }

        /// <summary>Opnieuw opbouwen na een wijziging van instellingen of schermgrootte.</summary>
        public void Apply(Settings s)
        {
            if (!IsHandleCreated) CreateHandle();
            float scale = Math.Max(1f, DeviceDpi / 96f);
            alpha = (byte)(255 * s.Opacity / 100);
            renderer.Rebuild(s, bw, bh, scale);
            EnsureSurface();
            Render(0f);
        }

        void EnsureSurface()
        {
            if (bmp != null) return;
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screen);
            Native.ReleaseDC(IntPtr.Zero, screen);
            var hdr = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)),
                biWidth = bw, biHeight = -bh, biPlanes = 1, biBitCount = 32
            };
            IntPtr bits;
            dib = Native.CreateDIBSection(memDC, ref hdr, 0, out bits, IntPtr.Zero, 0);
            oldObj = Native.SelectObject(memDC, dib);
            bmp = new Bitmap(bw, bh, bw * 4, PixelFormat.Format32bppPArgb, bits);
            gfx = Graphics.FromImage(bmp);
        }

        public void Render(float t)
        {
            if (gfx == null || !IsHandleCreated) return;
            gfx.Clear(Color.Transparent);
            renderer.Draw(gfx, t);

            var dst = new Native.POINT { x = Left, y = Top };
            var size = new Native.SIZE { cx = bw, cy = bh };
            var src = new Native.POINT();
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, memDC, ref src, 0, ref blend, 2);
            Native.ReleaseDC(IntPtr.Zero, screen);
        }

        public void KeepOnTop()
        {
            if (IsHandleCreated && Visible)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE|NOMOVE|NOACTIVATE
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderer.Dispose();
                if (gfx != null) { gfx.Dispose(); gfx = null; }
                if (bmp != null) { bmp.Dispose(); bmp = null; }
            }
            if (memDC != IntPtr.Zero)
            {
                if (oldObj != IntPtr.Zero) Native.SelectObject(memDC, oldObj);
                if (dib != IntPtr.Zero) Native.DeleteObject(dib);
                Native.DeleteDC(memDC);
                memDC = IntPtr.Zero; dib = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
    }
}
