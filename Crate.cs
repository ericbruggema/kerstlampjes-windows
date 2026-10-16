using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kerstlampjes
{
    /// <summary>
    /// De vuurwerkkist: een kistje met pijlen in een hoek van het scherm. Klik erop en het vuurwerk gaat af
    /// (de gekozen show); zo hoeft het vuurwerk niet continu te lopen.
    /// </summary>
    public partial class RimDecor
    {
        float CrateX { get { return w * 0.94f; } }

        bool FireBusy() { return queue.Count > 0 || emitters.Count > 0 || rockets.Count > 2; }

        bool HitCrate(float x, float y)
        {
            float gy = GroundY(CrateX);
            return Math.Abs(x - CrateX) < 32f * d && y > gy - 62f * d && y < gy + 6f * d;
        }

        /// <summary>Klik op de kist: de grote show start alleen als de vorige klaar is.</summary>
        void PressCrate()
        {
            if (s.FireShow == FireShows.Grand && FireBusy()) return;
            float gy = GroundY(CrateX);
            for (int i = 0; i < 8; i++) AddSpark(CrateX + 14f * d, gy - 30f * d, Color.FromArgb(0xFF, 0xB0, 0x40), 0.6f, 1.2f);
            LaunchFirework();
        }

        void DrawCrate(Graphics g)
        {
            float cx = CrateX, gy = GroundY(cx);
            bool idle = !FireBusy();
            var st = g.Save();
            g.TranslateTransform(cx, gy);
            g.ScaleTransform(d, d);

            if (idle)
            {   // zachte gloed die pulseert: "klik mij"
                float p = 0.5f + 0.5f * (float)Math.Sin(time * 3f);
                Circle(g, 0, -26, 40, Color.FromArgb(A(18 + 34 * p), 255, 200, 90));
                Circle(g, 0, -26, 28, Color.FromArgb(A(26 + 40 * p), 255, 200, 90));
            }

            // pijlen die uit de kist steken
            var pal = Palette();
            for (int j = 0; j < 3; j++)
            {
                float bx = -10 + j * 10, tx = bx + (j - 1) * 3.5f;
                pen.Width = 1.6f; pen.Color = Color.FromArgb(0x8B, 0x6B, 0x3B); pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawLine(pen, bx, -22, tx, -38);
                br.Color = Color.FromArgb(pal[j % pal.Length]);
                g.FillPolygon(br, new[] { new PointF(tx - 3.2f, -38), new PointF(tx + 3.2f, -38), new PointF(tx, -49) });
            }

            // de kist
            Box(g, -19, -26, 38, 26, 3, Color.FromArgb(0x8B, 0x5A, 0x2B));
            br.Color = Color.FromArgb(0x6B, 0x43, 0x1E);
            g.FillRectangle(br, -19, -18, 38, 1.6f); g.FillRectangle(br, -19, -9, 38, 1.6f);
            br.Color = Color.FromArgb(0xC6, 0x28, 0x28);
            g.FillRectangle(br, -19, -20, 38, 9);
            // ster op de band
            br.Color = Color.White;
            var star = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? 4.2f : 1.8f, a = (float)(-Math.PI / 2 + i * Math.PI / 5);
                star[i] = new PointF(r * (float)Math.Cos(a), -15.5f + r * (float)Math.Sin(a));
            }
            g.FillPolygon(br, star);

            // lont met een vonkje
            pen.Width = 1.2f; pen.Color = Color.FromArgb(0x33, 0x2A, 0x20);
            g.DrawBezier(pen, 15, -26, 17, -31, 21, -31, 22, -36);
            float fl = 0.6f + 0.4f * (float)Math.Sin(time * 22f);
            Circle(g, 22, -37, 3.6f * fl + 1.5f, Color.FromArgb(A(120 * fl), 255, 170, 60));
            Circle(g, 22, -37, 1.6f, Color.FromArgb(0xFF, 0xF0, 0xB0));

            g.Restore(st);
        }
    }
}
