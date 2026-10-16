using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Kerstlampjes
{
    /// <summary>Versiering rond de rand van het scherm; elk onderdeel is los aan of uit te zetten.</summary>
    public static class Decor
    {
        public const int Train = 1, Snow = 2, Stars = 4, Fireworks = 8, Pile = 16, Icicles = 32,
            Garland = 64, Sleigh = 128, Walkers = 256, Marquee = 512, All = 1023;

        public static readonly int[] Flags = { Train, Snow, Stars, Fireworks, Pile, Icicles, Garland, Sleigh, Walkers, Marquee };
        public static readonly string[] Names =
        {
            "Treintje", "Sneeuwvlokken en sterretjes", "Vallende sterren", "Vuurwerk", "Sneeuw die blijft liggen",
            "Ijspegels", "Slinger", "Slee met rendieren", "Pinguïns en sneeuwpop", "Lichtkrant"
        };
        public static readonly string[] TrainViews = { "Van boven", "Van opzij" };
    }

    /// <summary>Gesloten afgeronde rechthoek; At zet X/Y en de looprichting TX/TY op afstand s (met de klok mee).</summary>
    public class RimPath
    {
        public readonly float X0, Y0, X1, Y1, R, Length;
        readonly float sw, sh, arc;
        readonly float[] seg;
        public float X, Y, TX = 1, TY;

        public RimPath(float x0, float y0, float x1, float y1, float radius)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
            R = Math.Max(0f, Math.Min(radius, Math.Max(0f, Math.Min(x1 - x0, y1 - y0) / 2f)));
            sw = Math.Max(0f, (x1 - x0) - 2 * R);
            sh = Math.Max(0f, (y1 - y0) - 2 * R);
            arc = R * (float)Math.PI / 2f;
            seg = new[] { sw, arc, sh, arc, sw, arc, sh, arc };
            Length = 2 * sw + 2 * sh + 4 * arc;
        }

        public void At(float sIn)
        {
            float s = sIn % Length;
            if (s < 0) s += Length;
            int k = 0;
            while (k < 7 && s >= seg[k]) { s -= seg[k]; k++; }
            switch (k)
            {
                case 0: X = X0 + R + s; Y = Y0; TX = 1; TY = 0; return;
                case 2: X = X1; Y = Y0 + R + s; TX = 0; TY = 1; return;
                case 4: X = X1 - R - s; Y = Y1; TX = -1; TY = 0; return;
                case 6: X = X0; Y = Y1 - R - s; TX = 0; TY = -1; return;
            }
            float cx = (k == 1 || k == 3) ? X1 - R : X0 + R;
            float cy = (k == 1 || k == 7) ? Y0 + R : Y1 - R;
            float th0 = -(float)Math.PI / 2f + ((k - 1) / 2) * (float)Math.PI / 2f;
            float th = th0 + (R > 0f ? s / R : 0f);
            X = cx + R * (float)Math.Cos(th); Y = cy + R * (float)Math.Sin(th);
            TX = -(float)Math.Sin(th); TY = (float)Math.Cos(th);
        }
    }

    public class RimDecor : IDisposable
    {
        class Spark { public float X, Y, VX, VY, Age, Life, Size, Grav; public Color Col; }
        class Rocket { public float X, Y, VY, TY; }
        class Star { public float X, Y, VX, VY, Age, Life; }
        class Puff { public float X, Y, VX, VY, Age, Life, R; }
        class Penguin { public float X, Dir, Pause, HopY, HopV, Phase; }

        Settings s = new Settings();
        float w, h, d = 1f;
        readonly Random rnd = new Random();
        readonly Stopwatch clock = Stopwatch.StartNew();
        float time;
        long lastMs = -1;

        float size = 14, insetPx, cornerPx, reach;

        /// <summary>Alleen voor Android-gedrag: een klik ergens anders dan op een object laat vuurwerk afgaan.</summary>
        public bool FireworksOnAnyClick;

        public bool Active { get { return s.DecorMask != 0; } }
        bool On(int f) { return (s.DecorMask & f) != 0; }

        readonly SolidBrush br = new SolidBrush(Color.White);
        readonly Pen pen = new Pen(Color.White);
        readonly GraphicsPath path = new GraphicsPath();

        // sneeuwvlokken en sterretjes
        const int NFlakes = 60, NSpark = 14;
        readonly float[] fx = new float[NFlakes], fy = new float[NFlakes], fv = new float[NFlakes], fs = new float[NFlakes], fp = new float[NFlakes];
        readonly float[] sx = new float[NSpark], sy = new float[NSpark], sp = new float[NSpark], ss = new float[NSpark];

        // sneeuw die blijft liggen
        float[] pile = new float[0];
        float pileAcc, clearT;

        // ijspegels
        float[] ix = new float[0], iw = new float[0], imax = new float[0], ilen = new float[0], iy = new float[0], ivy = new float[0], inext = new float[0];
        bool[] ifall = new bool[0];

        // vaste achtergrond (slinger en spoor), eenmalig getekend
        Bitmap staticBack;
        /// <summary>Vaste onderlaag (slinger, spoor en draad); wordt elk beeld in een keer gekopieerd.</summary>
        public Bitmap StaticLayer { get { return staticBack; } }
        float[] balls = new float[0];

        // trein
        float trainS;
        RimPath trackTop;
        readonly List<Puff> puffs = new List<Puff>();
        float puffT;
        readonly float[] pose = new float[3];

        // vuurwerk, vallende sterren, slee, pinguïns
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Rocket> rockets = new List<Rocket>();
        float fireT = 2f;
        readonly List<Star> stars = new List<Star>();
        float starT = 3f;
        bool sleighActive; float sleighX, sleighDir = 1, sleighT = 8f, trailT;
        readonly List<Penguin> penguins = new List<Penguin>();
        float snowmanX, snowmanWiggle;

        // lichtkrant
        Font marqueeFont;
        string marqueeText = "";
        float[] marqueeW = new float[0];
        float marqueeTotal;

        int[] Palette() { return Palettes.Colors[Math.Max(0, Math.Min(Palettes.Colors.Length - 1, s.Palette))]; }
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        // ------------------------------------------------------------------ opbouw

        public void Layout(Settings settings, float width, float height, float dpiScale)
        {
            s = settings.Clone();
            d = dpiScale;
            if (width <= 0 || height <= 0) return;
            bool resized = width != w || height != h;
            w = width; h = height;
            size = s.Size * d;
            insetPx = s.Inset * d + size * 0.15f;
            cornerPx = s.Corner * d;
            reach = s.ShowLights ? size * 1.9f : 0f;

            if (resized || pile.Length == 0)
            {
                pile = new float[Math.Max(2, (int)(w / (4f * d)) + 1)];
                BuildIcicles();
                for (int i = 0; i < NFlakes; i++) ResetFlake(i, true);
                for (int i = 0; i < NSpark; i++) NewSparkle(i);
                penguins.Clear();
                penguins.Add(new Penguin { X = w * 0.25f, Dir = 1, Pause = 0, Phase = 0 });
                penguins.Add(new Penguin { X = w * 0.55f, Dir = -1, Pause = 1, Phase = 2 });
                snowmanX = w * 0.82f;
            }
            if (On(Decor.Train)) BuildTrack();
            BuildStatic();
            BuildMarquee();
        }

        float BandW() { return Math.Max(60f * d, Math.Min(110f * d, w * 0.16f)); }

        void ResetFlake(int i, bool anywhere)
        {
            float b = BandW();
            fx[i] = rnd.Next(2) == 0 ? R(0f, b) : R(w - b, w);
            fy[i] = anywhere ? R(-20f * d, h) : -10f * d;
            fv[i] = R(28f, 70f) * d;
            fs[i] = R(1.2f, 3.2f) * d;
            fp[i] = R(0f, 6.28f);
        }

        void NewSparkle(int i)
        {
            float b = BandW();
            if (rnd.Next(3) == 0) { sx[i] = R(0f, w); sy[i] = R(0f, b); }
            else { sx[i] = rnd.Next(2) == 0 ? R(0f, b) : R(w - b, w); sy[i] = R(0f, h); }
            sp[i] = R(0f, 6.28f);
            ss[i] = R(4f, 9f) * d;
        }

        void BuildIcicles()
        {
            var xs = new List<float>();
            float x = 18f * d;
            while (x < w - 18f * d) { xs.Add(x); x += R(18f, 38f) * d; }
            int n = xs.Count;
            ix = xs.ToArray();
            iw = new float[n]; imax = new float[n]; ilen = new float[n]; iy = new float[n]; ivy = new float[n]; inext = new float[n];
            ifall = new bool[n];
            for (int i = 0; i < n; i++) { iw[i] = R(6f, 11f) * d; imax[i] = R(12f, 42f) * d; ilen[i] = imax[i]; inext[i] = R(30f, 120f); }
        }

        float TrackOffset() { return insetPx + reach + 9f * d; }

        void BuildTrack()
        {
            float off = TrackOffset();
            trackTop = new RimPath(off, off, w - off, h - off, Math.Max(10f * d, cornerPx - (off - insetPx)));
        }

        static void Quad(GraphicsPath gp, float x0, float y0, float cx, float cy, float x1, float y1)
        {
            gp.AddBezier(x0, y0, x0 + 2f / 3f * (cx - x0), y0 + 2f / 3f * (cy - y0),
                x1 + 2f / 3f * (cx - x1), y1 + 2f / 3f * (cy - y1), x1, y1);
        }

        void BuildStatic()
        {
            if (staticBack != null) { staticBack.Dispose(); staticBack = null; }
            staticBack = new Bitmap((int)w, (int)h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(staticBack))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (On(Decor.Garland)) PaintGarland(g);
                if (On(Decor.Train)) PaintTrack(g);
            }
        }

        void PaintGarland(Graphics g)
        {
            var rp = new RimPath(insetPx, insetPx, w - insetPx, h - insetPx, cornerPx);
            float swag = 64f * d;
            int n = Math.Max(4, (int)Math.Round(rp.Length / swag));
            float step = rp.Length / n, sag = 9f * d;
            var pal = Palette();
            var main = new GraphicsPath();
            var needles = new GraphicsPath();
            var bl = new List<float>();
            for (int i = 0; i < n; i++)
            {
                rp.At(i * step); float ax = rp.X, ay = rp.Y;
                rp.At((i + 1) * step); float bx = rp.X, by = rp.Y;
                rp.At(i * step + step / 2f);
                float cx = rp.X + (-rp.TY) * sag * 2f, cy = rp.Y + rp.TX * sag * 2f;
                Quad(main, ax, ay, cx, cy, bx, by);
                int m = Math.Max(6, (int)(step / (2.6f * d)));
                for (int k = 0; k < m; k++)
                {
                    float t = k / (float)m, u = 1 - t;
                    float px = u * u * ax + 2 * u * t * cx + t * t * bx, py = u * u * ay + 2 * u * t * cy + t * t * by;
                    float dx = 2 * u * (cx - ax) + 2 * t * (bx - cx), dy = 2 * u * (cy - ay) + 2 * t * (by - cy);
                    float a = (float)Math.Atan2(dy, dx);
                    foreach (int sgn in new[] { -1, 1 })
                    {
                        float aa = a + sgn * R(0.7f, 1.3f), len = R(4f, 8f) * d;
                        needles.StartFigure();
                        needles.AddLine(px, py, px + (float)Math.Cos(aa) * len, py + (float)Math.Sin(aa) * len);
                    }
                }
                float mx = 0.25f * ax + 0.5f * cx + 0.25f * bx, my = 0.25f * ay + 0.5f * cy + 0.25f * by;
                bl.Add(mx); bl.Add(my + 5f * d); bl.Add(5.2f * d); bl.Add(pal[i % pal.Length]);
            }
            balls = bl.ToArray();
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            pen.Width = 7f * d; pen.Color = Color.FromArgb(0x1B, 0x5E, 0x2E); g.DrawPath(pen, main);
            pen.Width = 1.7f * d; pen.Color = Color.FromArgb(0x3F, 0xA6, 0x5E); g.DrawPath(pen, needles);
            pen.Width = 2.2f * d; pen.Color = Color.FromArgb(0x2E, 0x8B, 0x4A); g.DrawPath(pen, main);
            main.Dispose(); needles.Dispose();
            if (s.GarlandBalls)
            {
                for (int i = 0; i < balls.Length; i += 4)
                {
                    float bx = balls[i], by = balls[i + 1], brr = balls[i + 2];
                    pen.Width = 1f * d; pen.Color = Color.FromArgb(0xB8, 0x86, 0x0B);
                    g.DrawLine(pen, bx, by - brr - 3f * d, bx, by - brr);
                    br.Color = Color.FromArgb((int)balls[i + 3]); g.FillEllipse(br, bx - brr, by - brr, 2 * brr, 2 * brr);
                    br.Color = Color.FromArgb(0x88, 255, 255, 255);
                    float hr = brr * 0.3f; g.FillEllipse(br, bx - brr * 0.35f - hr, by - brr * 0.35f - hr, 2 * hr, 2 * hr);
                }
            }
        }

        void PaintTrack(Graphics g)
        {
            if (s.TrainSide)
            {
                float y = TrackY();
                pen.Width = 1.8f * d; pen.Color = Color.FromArgb(0xA0, 0xAA, 0xB0);
                g.DrawLine(pen, 0, y, w, y);
                pen.Width = 3f * d; pen.Color = Color.FromArgb(0x6B, 0x4A, 0x2B);
                for (float x = 0; x < w; x += 9f * d) g.DrawLine(pen, x, y + 2f * d, x, y + 4f * d);
                return;
            }
            var rp = trackTop;
            if (rp == null) return;
            int n = (int)(rp.Length / (8f * d));
            pen.Width = 2f * d; pen.Color = Color.FromArgb(0xAA, 0x6B, 0x4A, 0x2B);
            for (int i = 0; i < n; i++)
            {
                rp.At(i * rp.Length / n);
                float nx = -rp.TY, ny = rp.TX;
                g.DrawLine(pen, rp.X - nx * 5f * d, rp.Y - ny * 5f * d, rp.X + nx * 5f * d, rp.Y + ny * 5f * d);
            }
            pen.Width = 1.4f * d; pen.Color = Color.FromArgb(0xCC, 0xB0, 0xBA, 0xC0);
            foreach (float o in new[] { -3.5f * d, 3.5f * d })
            {
                float rr = Math.Max(0f, rp.R + o);
                RoundRectPath(path, rp.X0 - o, rp.Y0 - o, rp.X1 + o, rp.Y1 + o, rr);
                g.DrawPath(pen, path);
            }
        }

        void BuildMarquee()
        {
            string text = string.IsNullOrWhiteSpace(s.Marquee) ? Tr.t("Fijne feestdagen!") : s.Marquee;
            if (marqueeFont != null) marqueeFont.Dispose();
            marqueeFont = new Font("Segoe UI", 22f * d, FontStyle.Bold, GraphicsUnit.Pixel);
            marqueeText = text;
            marqueeW = new float[text.Length];
            marqueeTotal = 0;
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                var fmt = StringFormat.GenericTypographic;
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                for (int i = 0; i < text.Length; i++)
                {
                    marqueeW[i] = g.MeasureString(text.Substring(i, 1), marqueeFont, PointF.Empty, fmt).Width;
                    marqueeTotal += marqueeW[i];
                }
            }
        }

        static void RoundRectPath(GraphicsPath gp, float l, float t, float r, float b, float rad)
        {
            gp.Reset();
            float dm = Math.Min(rad * 2f, Math.Min(r - l, b - t));
            if (dm < 1f) { gp.AddRectangle(new RectangleF(l, t, r - l, b - t)); return; }
            gp.AddArc(l, t, dm, dm, 180, 90);
            gp.AddArc(r - dm, t, dm, dm, 270, 90);
            gp.AddArc(r - dm, b - dm, dm, dm, 0, 90);
            gp.AddArc(l, b - dm, dm, dm, 90, 90);
            gp.CloseFigure();
        }

        // ------------------------------------------------------------------ bediening

        /// <summary>Verwerkt een klik (lokale coördinaten); true als er iets op reageerde.</summary>
        public bool Click(float x, float y)
        {
            if (w <= 0) return false;
            for (int i = stars.Count - 1; i >= 0; i--)
            {
                var st = stars[i];
                if (Math.Abs(st.X - x) < 54f * d && Math.Abs(st.Y - y) < 54f * d) { Burst(st.X, st.Y); stars.RemoveAt(i); return true; }
            }
            if (On(Decor.Icicles))
                for (int i = 0; i < ix.Length; i++)
                    if (!ifall[i] && ilen[i] > 4f * d && Math.Abs(ix[i] - x) < 20f * d && y < ilen[i] + 36f * d)
                    { ifall[i] = true; ivy[i] = 0; Sounds.Ding(s.Sound); return true; }
            if (On(Decor.Train) && HitTrain(x, y))
            {
                Sounds.Toot(s.Sound);
                for (int k = 0; k < 6; k++) { Chimney(pose); SpawnPuff(pose[0], pose[1], 1.4f); }
                return true;
            }
            if (On(Decor.Walkers))
            {
                foreach (var pg in penguins)
                    if (Math.Abs(pg.X - x) < 22f * d && Math.Abs(GroundY(pg.X) - y) < 34f * d) { pg.HopV = 380f * d; pg.HopY = 0.01f; return true; }
                if (Math.Abs(snowmanX - x) < 26f * d && Math.Abs(GroundY(snowmanX) - 22f * d - y) < 40f * d)
                {
                    snowmanWiggle = 1.2f;
                    for (int k = 0; k < 14; k++) AddSpark(snowmanX, GroundY(snowmanX) - 40f * d, Color.White, 1f, 2f);
                    return true;
                }
            }
            if (FireworksOnAnyClick && On(Decor.Fireworks)) { Burst(x, y); return true; }
            return false;
        }

        /// <summary>Schudden (bij Windows: de muis heen en weer): sneeuw die blijft liggen vliegt weg.</summary>
        public void Shake()
        {
            if (!On(Decor.Pile) || w <= 0) return;
            clearT = 0.9f;
            for (int i = 0; i < pile.Length; i += 2)
                if (pile[i] > 2f * d)
                    sparks.Add(new Spark { X = i * 4f * d, Y = h - pile[i], VX = R(-50f, 50f) * d, VY = -R(90f, 260f) * d, Life = R(0.7f, 1.3f), Col = Color.FromArgb(0xF2, 0xF7, 0xFF), Size = R(1.5f, 3f) * d, Grav = 260f * d });
        }

        /// <summary>De muis schuift door de sneeuw: die wordt weggeveegd rond x.</summary>
        public void Plow(float x, float y)
        {
            if (!On(Decor.Pile) || pile.Length == 0) return;
            if (y < h - 34f * d - 26f * d) return;
            int c0 = Math.Max(0, (int)((x - 30f * d) / (4f * d))), c1 = Math.Min(pile.Length - 1, (int)((x + 30f * d) / (4f * d)));
            for (int i = c0; i <= c1; i++)
            {
                if (pile[i] > 1.5f * d && rnd.Next(4) == 0) AddSpark(i * 4f * d, h - pile[i], Color.FromArgb(0xF2, 0xF7, 0xFF), 0.8f, 1.5f);
                pile[i] *= 0.6f;
            }
        }

        public void LaunchFirework()
        {
            rockets.Add(new Rocket { X = R(w * 0.12f, w * 0.88f), Y = h, VY = -R(420f, 620f) * d, TY = R(h * 0.15f, h * 0.5f) });
        }

        public void DropIcicles()
        {
            for (int i = 0; i < ix.Length; i++) if (!ifall[i] && ilen[i] > 4f * d) { ifall[i] = true; ivy[i] = -R(0f, 200f) * d; }
        }

        bool HitTrain(float x, float y)
        {
            int n = 1 + s.TrainWagons;
            for (int k = 0; k < n; k++)
            {
                CarPose(k, pose);
                if (s.TrainSide) { if (Math.Abs(pose[0] - x) < CarLen() / 2f && Math.Abs(pose[1] - y) < 16f * d) return true; }
                else if (Math.Abs(pose[0] - x) < 20f * d && Math.Abs(pose[1] - y) < 20f * d) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ simulatie

        float CarLen() { return s.TrainSide ? 44f * d : 26f * d; }
        float CarGap() { return s.TrainSide ? 5f * d : 4f * d; }
        float TrainSpeed() { return (24f + 16f * s.Speed) * d; }
        float SideCycle() { return w + (1 + s.TrainWagons) * (CarLen() + CarGap()) + CarLen() * 2f; }
        float TrackY() { return h - Math.Max(10f * d, insetPx + 2f * d); }
        float SideH { get { return 22f * d; } }

        void CarPose(int k, float[] o)
        {
            float sk = trainS - k * (CarLen() + CarGap());
            if (s.TrainSide)
            {
                float cycle = SideCycle(), x = sk % cycle;
                if (x < 0) x += cycle;
                o[0] = x - CarLen(); o[1] = TrackY() - 13f * d; o[2] = 0f;
            }
            else
            {
                var rp = trackTop; if (rp == null) return;
                rp.At(sk + CarLen() / 2f); float fx2 = rp.X, fy2 = rp.Y;
                rp.At(sk - CarLen() / 2f); float rx = rp.X, ry = rp.Y;
                o[0] = (fx2 + rx) / 2f; o[1] = (fy2 + ry) / 2f; o[2] = (float)Math.Atan2(fy2 - ry, fx2 - rx);
            }
        }

        void Chimney(float[] o)
        {
            CarPose(0, o);
            float a = o[2], lx = CarLen() * (s.TrainSide ? 0.30f : 0.22f), ly = s.TrainSide ? -SideH * 0.5f : 0f;
            float cx = o[0], cy = o[1];
            o[0] = cx + (float)Math.Cos(a) * lx - (float)Math.Sin(a) * ly;
            o[1] = cy + (float)Math.Sin(a) * lx + (float)Math.Cos(a) * ly;
        }

        void SpawnPuff(float x, float y, float scale)
        {
            if (puffs.Count > 48) return;
            bool side = s.TrainSide;
            puffs.Add(new Puff
            {
                X = x, Y = y,
                VX = side ? -R(4f, 14f) * d : R(-9f, 9f) * d,
                VY = side ? -R(18f, 30f) * d : R(-9f, 9f) * d,
                Life = R(0.9f, 1.5f), R = (side ? 3f : 2.4f) * d * scale
            });
        }

        void AddSpark(float x, float y, Color col, float life, float spread)
        {
            if (sparks.Count > 520) return;
            sparks.Add(new Spark { X = x, Y = y, VX = R(-30f, 30f) * d * spread, VY = -R(10f, 60f) * d * spread, Life = life * R(0.6f, 1f), Col = col, Size = R(1.2f, 2.6f) * d, Grav = 120f * d });
        }

        static Color Hsv(float h, float sat, float v)
        {
            int hi = (int)(h / 60f) % 6; float f = h / 60f - (float)Math.Floor(h / 60f);
            float p = v * (1 - sat), q = v * (1 - f * sat), t = v * (1 - (1 - f) * sat);
            float r, g, b;
            switch (hi) { case 0: r = v; g = t; b = p; break; case 1: r = q; g = v; b = p; break; case 2: r = p; g = v; b = t; break; case 3: r = p; g = q; b = v; break; case 4: r = t; g = p; b = v; break; default: r = v; g = p; b = q; break; }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        void Burst(float x, float y)
        {
            Sounds.Boom(s.Sound);
            float hue = R(0f, 360f);
            int n = 46;
            bool ring = rnd.Next(2) == 0;
            for (int i = 0; i < n; i++)
            {
                if (sparks.Count > 520) break;
                float a = (float)(2 * Math.PI * i / n) + R(-0.05f, 0.05f);
                float spd = (ring ? 130f : R(50f, 170f)) * d;
                sparks.Add(new Spark { X = x, Y = y, VX = (float)Math.Cos(a) * spd, VY = (float)Math.Sin(a) * spd, Life = R(1.0f, 1.8f), Col = Hsv((hue + R(-25f, 25f) + 360f) % 360f, R(0.45f, 0.9f), 1f), Size = R(1.6f, 3.0f) * d, Grav = 85f * d });
            }
        }

        public void Update()
        {
            long now = clock.ElapsedMilliseconds;
            float dt = lastMs < 0 ? 0.033f : Math.Max(0.001f, Math.Min(0.1f, (now - lastMs) / 1000f));
            lastMs = now; time += dt;
            if (w <= 0) return;

            if (On(Decor.Snow))
                for (int i = 0; i < NFlakes; i++)
                {
                    fy[i] += fv[i] * dt;
                    fx[i] += (float)Math.Sin(time * 1.3f + fp[i]) * 12f * d * dt;
                    if (fy[i] > h) ResetFlake(i, false);
                }

            if (On(Decor.Pile))
            {
                pileAcc += 14f * d * dt;
                float maxH = 26f * d;
                while (pileAcc > 0.25f * d)
                {
                    pileAcc -= 0.25f * d;
                    int c = rnd.Next(pile.Length);
                    if (pile[c] < maxH) pile[c] += 0.25f * d;
                }
                float lim = 2.2f * d;
                for (int i = 0; i < pile.Length; i++)
                {
                    int l = i > 0 ? i - 1 : i, rr = i < pile.Length - 1 ? i + 1 : i;
                    if (pile[i] - pile[l] > lim) { float m = 0.3f * (pile[i] - pile[l] - lim); pile[i] -= m; pile[l] += m; }
                    if (pile[i] - pile[rr] > lim) { float m = 0.3f * (pile[i] - pile[rr] - lim); pile[i] -= m; pile[rr] += m; }
                }
                if (clearT > 0f)
                {
                    clearT -= dt;
                    float f = Math.Max(0f, 1f - 3f * dt);
                    for (int i = 0; i < pile.Length; i++) pile[i] *= f;
                }
            }
            else Array.Clear(pile, 0, pile.Length);

            if (On(Decor.Icicles))
                for (int i = 0; i < ix.Length; i++)
                {
                    if (ifall[i])
                    {
                        ivy[i] += 1100f * d * dt; iy[i] += ivy[i] * dt;
                        if (iy[i] + ilen[i] > h) { ifall[i] = false; ilen[i] = 0; iy[i] = 0; inext[i] = R(40f, 140f); }
                    }
                    else if (ilen[i] < imax[i]) ilen[i] = Math.Min(imax[i], ilen[i] + imax[i] * dt / 9f);
                    else { inext[i] -= dt; if (inext[i] <= 0f) { ifall[i] = true; ivy[i] = 0; } }
                }

            if (On(Decor.Train))
            {
                trainS += TrainSpeed() * dt;
                float len = s.TrainSide ? SideCycle() : (trackTop != null ? trackTop.Length : 1f);
                if (trainS > len * 4) trainS -= len * 4;
                puffT -= dt;
                if (puffT <= 0f) { puffT = 0.14f; Chimney(pose); SpawnPuff(pose[0], pose[1], 1f); }
            }
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var pf = puffs[i];
                pf.Age += dt; pf.X += pf.VX * dt; pf.Y += pf.VY * dt;
                if (pf.Age > pf.Life) puffs.RemoveAt(i);
            }

            if (On(Decor.Fireworks)) { fireT -= dt; if (fireT <= 0f) { fireT = R(2.5f, 6f); LaunchFirework(); } }
            for (int i = rockets.Count - 1; i >= 0; i--)
            {
                var rk = rockets[i];
                rk.Y += rk.VY * dt;
                if (rk.Y <= rk.TY) { Burst(rk.X, rk.Y); rockets.RemoveAt(i); }
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var sk = sparks[i];
                sk.Age += dt; sk.VY += sk.Grav * dt; sk.X += sk.VX * dt; sk.Y += sk.VY * dt;
                if (sk.Age > sk.Life || sk.Y > h + 20f * d) sparks.RemoveAt(i);
            }

            if (On(Decor.Stars))
            {
                starT -= dt;
                if (starT <= 0f)
                {
                    starT = R(5f, 11f);
                    float dir = rnd.Next(2) == 0 ? 1f : -1f, ang = R(0.45f, 0.8f), v = R(380f, 520f) * d;
                    stars.Add(new Star { X = dir > 0 ? R(0f, w * 0.5f) : R(w * 0.5f, w), Y = R(0f, h * 0.25f), VX = (float)Math.Cos(ang) * v * dir, VY = (float)Math.Sin(ang) * v, Life = R(1.3f, 1.8f) });
                }
            }
            for (int i = stars.Count - 1; i >= 0; i--)
            {
                var st = stars[i];
                st.Age += dt; st.X += st.VX * dt; st.Y += st.VY * dt;
                if (st.Age > st.Life) stars.RemoveAt(i);
            }

            if (On(Decor.Sleigh))
            {
                if (!sleighActive)
                {
                    sleighT -= dt;
                    if (sleighT <= 0f) { sleighActive = true; sleighDir = rnd.Next(2) == 0 ? 1f : -1f; sleighX = sleighDir > 0 ? -170f * d : w + 170f * d; }
                }
                else
                {
                    sleighX += sleighDir * 110f * d * dt;
                    trailT -= dt;
                    if (trailT <= 0f) { trailT = 0.04f; AddSpark(sleighX - sleighDir * 20f * d, SleighY() + 4f * d, Color.FromArgb(0xFF, 0xD5, 0x4F), 1.2f, 0.6f); }
                    if ((sleighDir > 0 && sleighX > w + 190f * d) || (sleighDir < 0 && sleighX < -190f * d)) { sleighActive = false; sleighT = R(25f, 45f); }
                }
            }

            if (On(Decor.Walkers))
            {
                foreach (var pg in penguins)
                {
                    if (pg.Pause > 0f) pg.Pause -= dt;
                    else
                    {
                        pg.X += pg.Dir * 22f * d * dt;
                        if (pg.X < 30f * d) pg.Dir = 1; if (pg.X > w - 30f * d) pg.Dir = -1;
                        if (rnd.NextDouble() < 0.35 * dt) pg.Pause = R(1f, 3f);
                    }
                    if (pg.HopY > 0f) { pg.HopY += pg.HopV * dt; pg.HopV -= 1700f * d * dt; if (pg.HopY <= 0f) { pg.HopY = 0; pg.HopV = 0; } }
                }
                if (snowmanWiggle > 0f) snowmanWiggle -= dt;
            }
        }

        float SleighY() { return insetPx + reach + 62f * d + 16f * d * (float)Math.Sin(sleighX / (70f * d)); }

        float PileAt(float x)
        {
            if (pile.Length == 0 || !On(Decor.Pile)) return 0f;
            float f = Math.Max(0f, Math.Min(pile.Length - 1.001f, x / (4f * d)));
            int i = (int)f;
            return pile[i] + (pile[i + 1] - pile[i]) * (f - i);
        }

        float GroundY(float x) { return h - 3f * d - PileAt(x); }

        // ------------------------------------------------------------------ tekenen (achter de lampjes)

        public void DrawBack(Graphics g)
        {
            if (w <= 0) return;
            if (On(Decor.Icicles)) DrawIcicles(g);
            if (On(Decor.Pile)) DrawPile(g);
        }

        void DrawIcicles(Graphics g)
        {
            for (int i = 0; i < ix.Length; i++)
            {
                float len = ilen[i];
                if (len < 1f) continue;
                float top = iy[i], sway = ifall[i] ? 0f : (float)Math.Sin(time * 0.7f + i) * 0.6f * d;
                br.Color = Color.FromArgb(0xD8, 0xCF, 0xE8, 0xFF);
                g.FillPolygon(br, new[] { new PointF(ix[i] - iw[i] / 2f, top), new PointF(ix[i] + iw[i] / 2f, top), new PointF(ix[i] + sway, top + len) });
                pen.Width = 1f * d; pen.Color = Color.FromArgb(0xAA, 255, 255, 255);
                g.DrawLine(pen, ix[i] - iw[i] * 0.2f, top + 1f * d, ix[i] + sway * 0.8f, top + len * 0.85f);
            }
        }

        void DrawPile(Graphics g)
        {
            bool any = false;
            foreach (float v in pile) if (v > 0.5f) { any = true; break; }
            if (!any) return;
            for (int pass = 0; pass < 2; pass++)
            {
                float dy = pass == 0 ? 2.5f * d : 0f;
                var pts = new PointF[pile.Length + 4];
                pts[0] = new PointF(0, h + 2f * d);
                for (int i = 0; i < pile.Length; i++) pts[i + 1] = new PointF(i * 4f * d, h - pile[i] + dy);
                pts[pile.Length + 1] = new PointF(w, h - pile[pile.Length - 1] + dy);
                pts[pile.Length + 2] = new PointF(w, h + 2f * d);
                pts[pile.Length + 3] = new PointF(0, h + 2f * d);
                br.Color = pass == 0 ? Color.FromArgb(0xB3, 0xC8, 0xE6) : Color.FromArgb(0xF4, 0xF8, 0xFF);
                g.FillPolygon(br, pts);
            }
        }

        // ------------------------------------------------------------------ tekenen (voor de lampjes)

        public void DrawFront(Graphics g)
        {
            if (w <= 0) return;
            if (On(Decor.Walkers)) DrawWalkers(g);
            if (On(Decor.Train)) DrawTrain(g);
            if (On(Decor.Marquee)) DrawMarquee(g);
            if (On(Decor.Snow)) DrawSnow(g);
            if (On(Decor.Sleigh) && sleighActive) DrawSleigh(g);
            DrawStars(g);
            DrawFireworks(g);
            DrawPuffs(g);
        }

        static int A(float f) { return Math.Max(0, Math.Min(255, (int)f)); }

        void Circle(Graphics g, float x, float y, float r, Color c) { br.Color = c; g.FillEllipse(br, x - r, y - r, 2 * r, 2 * r); }

        void Sparkle(Graphics g, float x, float y, float r, Color c)
        {
            float k = 0.18f * r;
            br.Color = c;
            g.FillPolygon(br, new[]
            {
                new PointF(x, y - r), new PointF(x + k, y - k), new PointF(x + r, y), new PointF(x + k, y + k),
                new PointF(x, y + r), new PointF(x - k, y + k), new PointF(x - r, y), new PointF(x - k, y - k)
            });
        }

        void DrawSnow(Graphics g)
        {
            br.Color = Color.FromArgb(0xE6, 255, 255, 255);
            for (int i = 0; i < NFlakes; i++) g.FillEllipse(br, fx[i] - fs[i], fy[i] - fs[i], 2 * fs[i], 2 * fs[i]);
            for (int i = 0; i < NSpark; i++)
            {
                float a = (float)Math.Sin(time * 1.7f + sp[i]);
                if (a <= 0f) continue;
                Sparkle(g, sx[i], sy[i], ss[i] * (0.4f + 0.6f * a), Color.FromArgb(A(255 * a), 255, 244, 200));
            }
        }

        void DrawPuffs(Graphics g)
        {
            foreach (var pf in puffs)
            {
                float f = pf.Age / pf.Life, r = pf.R + f * 7f * d;
                Circle(g, pf.X, pf.Y, r, Color.FromArgb(A(170 * (1f - f)), 235, 235, 240));
            }
        }

        void DrawFireworks(Graphics g)
        {
            foreach (var rk in rockets)
            {
                Circle(g, rk.X, rk.Y, 2.2f * d, Color.FromArgb(0xFF, 0xE0, 0x8A));
                Circle(g, rk.X, rk.Y + 7f * d, 1.6f * d, Color.FromArgb(0x66, 0xFF, 0xE0, 0x8A));
                Circle(g, rk.X, rk.Y + 14f * d, 1.2f * d, Color.FromArgb(0x33, 0xFF, 0xE0, 0x8A));
            }
            foreach (var sk in sparks)
            {
                float f = Math.Max(0f, Math.Min(1f, 1f - sk.Age / sk.Life));
                int a = A(255 * f);
                Circle(g, sk.X, sk.Y, sk.Size * (0.5f + 0.5f * f), Color.FromArgb(a, sk.Col));
                Circle(g, sk.X - sk.VX * 0.03f, sk.Y - sk.VY * 0.03f, Math.Max(0.3f, sk.Size * 0.7f * f), Color.FromArgb(a / 3, sk.Col));
            }
        }

        void DrawStars(Graphics g)
        {
            foreach (var st in stars)
            {
                float fade = Math.Max(0f, Math.Min(1f, 1f - st.Age / st.Life));
                float len = (float)Math.Sqrt(st.VX * st.VX + st.VY * st.VY), ux = st.VX / len, uy = st.VY / len;
                for (int j = 0; j < 14; j++)
                {
                    float t = j * 4f * d;
                    Circle(g, st.X - ux * t, st.Y - uy * t, Math.Max(0.6f, 3f - j * 0.17f) * d, Color.FromArgb(A(230 * (1f - j / 14f) * fade), 255, 250, 225));
                }
                Sparkle(g, st.X, st.Y, 9f * d, Color.FromArgb(A(255 * fade), 255, 255, 255));
            }
        }

        void DrawMarquee(Graphics g)
        {
            if (marqueeFont == null || marqueeText.Length == 0) return;
            float y = insetPx + reach + 26f * d - 22f * d * 0.85f;
            var pal = Palette();
            float cycle = w + marqueeTotal + 40f * d;
            float x = w - ((time * 60f * d) % cycle);
            int shift = (int)(time * 2f);
            var fmt = StringFormat.GenericTypographic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            for (int i = 0; i < marqueeText.Length; i++)
            {
                if (x > -30f * d && x < w)
                {
                    string ch = marqueeText.Substring(i, 1);
                    br.Color = Color.FromArgb(0x99, 0, 0, 0);
                    float o = 1.5f * d;
                    g.DrawString(ch, marqueeFont, br, x - o, y, fmt); g.DrawString(ch, marqueeFont, br, x + o, y, fmt);
                    g.DrawString(ch, marqueeFont, br, x, y - o, fmt); g.DrawString(ch, marqueeFont, br, x, y + o, fmt);
                    br.Color = Color.FromArgb(pal[(i + shift) % pal.Length]);
                    g.DrawString(ch, marqueeFont, br, x, y, fmt);
                }
                x += marqueeW[i];
            }
        }

        // ---- trein

        void Box(Graphics g, float l, float t, float r, float b, float rad, Color col)
        {
            br.Color = col;
            if (rad < 1.2f * d) { g.FillRectangle(br, l, t, r - l, b - t); return; }
            RoundRectPath(path, l, t, r, b, rad);
            g.FillPath(br, path);
        }

        float Twinkle(int k, int j) { return 0.35f + 0.65f * (0.5f + 0.5f * (float)Math.Sin(time * 3.2f + k * 1.7f + j * 2.3f)); }

        void Lamp(Graphics g, float x, float y, int color, float a)
        {
            var c = Color.FromArgb(color);
            Circle(g, x, y, 4.2f * d, Color.FromArgb(A(70 * a), c));
            Circle(g, x, y, 1.9f * d, Color.FromArgb(A(255 * a), c));
        }

        void DrawTrain(Graphics g)
        {
            int n = 1 + s.TrainWagons;
            var pal = Palette();
            for (int k = n - 1; k >= 0; k--)
            {
                CarPose(k, pose);
                var st = g.Save();
                g.TranslateTransform(pose[0], pose[1]);
                g.RotateTransform(pose[2] * 180f / (float)Math.PI);
                if (s.TrainSide) { if (k == 0) SideLoco(g); else SideWagon(g, k, pal); }
                else { if (k == 0) TopLoco(g); else TopWagon(g, k, pal); }
                g.Restore(st);
            }
        }

        void TopLoco(Graphics g)
        {
            float l = CarLen(), hw = 7f * d;
            Box(g, -l / 2, -hw, l / 2, hw, 3f * d, Color.FromArgb(0xC6, 0x28, 0x28));
            Box(g, 0, -hw * 0.78f, l / 2, hw * 0.78f, 3f * d, Color.FromArgb(0x8E, 0x1B, 0x1B));
            Box(g, -l / 2, -hw, -l * 0.12f, hw, 2f * d, Color.FromArgb(0x5E, 0x10, 0x10));
            Box(g, l * 0.02f, -hw, l * 0.07f, hw, 0, Color.FromArgb(0xE0, 0xB0, 0x40));
            Circle(g, l * 0.22f, 0, 2.7f * d, Color.FromArgb(0x22, 0x22, 0x22));
            Circle(g, l / 2, 0, 1.9f * d, Color.FromArgb(0xFF, 0xE0, 0x8A));
            Circle(g, l / 2 + 2f * d, 0, 6f * d, Color.FromArgb(0x55, 0xFF, 0xE0, 0x8A));
            if (s.TrainLights) { Lamp(g, -l * 0.3f, -hw * 0.9f, unchecked((int)0xFFFFD54F), Twinkle(0, 0)); Lamp(g, -l * 0.3f, hw * 0.9f, unchecked((int)0xFF40A9FF), Twinkle(0, 1)); }
        }

        void TopWagon(Graphics g, int k, int[] pal)
        {
            float l = CarLen(), hw = 6.5f * d;
            Box(g, -l / 2 - CarGap(), -1f * d, -l / 2, 1f * d, 0, Color.FromArgb(0x33, 0x33, 0x33));
            Box(g, -l / 2, -hw, l / 2, hw, 2.5f * d, Color.FromArgb(0x37, 0x47, 0x4F));
            Box(g, -l / 2 + 1.5f * d, -hw + 1.5f * d, l / 2 - 1.5f * d, hw - 1.5f * d, 2f * d, Color.FromArgb(0x26, 0x32, 0x38));
            for (int j = 0; j < 3; j++)
            {
                var col = Color.FromArgb(pal[(k + j) % pal.Length]);
                float gx = -l * 0.34f + j * l * 0.24f;
                Box(g, gx, -hw * 0.55f, gx + 6f * d, hw * 0.55f, 1f * d, col);
                Box(g, gx + 2.6f * d, -hw * 0.55f, gx + 3.4f * d, hw * 0.55f, 0, Color.FromArgb(0xAA, 255, 255, 255));
            }
            if (s.TrainLights)
            {
                Lamp(g, -l * 0.42f, -hw, pal[k % pal.Length], Twinkle(k, 0)); Lamp(g, l * 0.42f, -hw, pal[(k + 1) % pal.Length], Twinkle(k, 1));
                Lamp(g, -l * 0.42f, hw, pal[(k + 2) % pal.Length], Twinkle(k, 2)); Lamp(g, l * 0.42f, hw, pal[(k + 3) % pal.Length], Twinkle(k, 3));
            }
        }

        void Wheel(Graphics g, float x, float y, float rad)
        {
            Circle(g, x, y, rad, Color.FromArgb(0x37, 0x39, 0x3D));
            pen.Width = 1f * d; pen.Color = Color.FromArgb(0xB6, 0xBC, 0xC2);
            g.DrawEllipse(pen, x - rad + 0.5f * d, y - rad + 0.5f * d, 2 * rad - d, 2 * rad - d);
            float a = trainS / rad;
            g.DrawLine(pen, x - (float)Math.Cos(a) * rad * 0.8f, y - (float)Math.Sin(a) * rad * 0.8f, x + (float)Math.Cos(a) * rad * 0.8f, y + (float)Math.Sin(a) * rad * 0.8f);
        }

        void SideLoco(Graphics g)
        {
            float l = CarLen(), hh = SideH;
            Box(g, -l / 2, hh * 0.2f, l / 2, hh * 0.38f, 1f * d, Color.FromArgb(0x2B, 0x2B, 0x2B));
            Box(g, -l * 0.08f, -hh * 0.25f, l * 0.5f, hh * 0.25f, 4f * d, Color.FromArgb(0xC6, 0x28, 0x28));
            Box(g, -l * 0.5f, -hh * 0.5f, -l * 0.1f, hh * 0.25f, 2f * d, Color.FromArgb(0x8E, 0x1B, 0x1B));
            Box(g, -l * 0.54f, -hh * 0.58f, -l * 0.06f, -hh * 0.48f, 1.5f * d, Color.FromArgb(0x3B, 0x0B, 0x0B));
            Box(g, -l * 0.42f, -hh * 0.38f, -l * 0.2f, -hh * 0.1f, 1.5f * d, Color.FromArgb(0xFF, 0xE0, 0x8A));
            br.Color = Color.FromArgb(0x22, 0x22, 0x22);
            g.FillPolygon(br, new[] { new PointF(l * 0.26f, -hh * 0.25f), new PointF(l * 0.34f, -hh * 0.25f), new PointF(l * 0.37f, -hh * 0.55f), new PointF(l * 0.23f, -hh * 0.55f) });
            Circle(g, l * 0.1f, -hh * 0.28f, 3f * d, Color.FromArgb(0xE0, 0xB0, 0x40));
            br.Color = Color.FromArgb(0x2B, 0x2B, 0x2B);
            g.FillPolygon(br, new[] { new PointF(l * 0.5f, hh * 0.1f), new PointF(l * 0.6f, hh * 0.38f), new PointF(l * 0.5f, hh * 0.38f) });
            Circle(g, l * 0.5f, -hh * 0.05f, 2f * d, Color.FromArgb(0xFF, 0xE0, 0x8A));
            Circle(g, l * 0.5f + 3f * d, -hh * 0.05f, 6f * d, Color.FromArgb(0x55, 0xFF, 0xE0, 0x8A));
            Wheel(g, -l * 0.3f, hh * 0.4f, 4.4f * d); Wheel(g, -l * 0.02f, hh * 0.4f, 4.4f * d); Wheel(g, l * 0.3f, hh * 0.4f, 4.4f * d);
            if (s.TrainLights) Lamp(g, -l * 0.3f, -hh * 0.62f, unchecked((int)0xFFFFD54F), Twinkle(0, 0));
        }

        void SideWagon(Graphics g, int k, int[] pal)
        {
            float l = CarLen(), hh = SideH;
            Box(g, -l / 2 - CarGap(), hh * 0.12f, -l / 2, hh * 0.18f, 0, Color.FromArgb(0x33, 0x33, 0x33));
            Box(g, -l / 2, hh * 0.2f, l / 2, hh * 0.36f, 1f * d, Color.FromArgb(0x2B, 0x2B, 0x2B));
            Box(g, -l / 2, -hh * 0.3f, l / 2, hh * 0.25f, 2.5f * d, Color.FromArgb(0x37, 0x47, 0x4F));
            pen.Width = 0.8f * d; pen.Color = Color.FromArgb(0x26, 0x32, 0x38);
            for (float x = -l / 2 + 7f * d; x < l / 2; x += 7f * d) g.DrawLine(pen, x, -hh * 0.3f, x, hh * 0.25f);
            for (int j = 0; j < 3; j++)
            {
                var col = Color.FromArgb(pal[(k + j) % pal.Length]);
                float gx = -l * 0.36f + j * l * 0.25f;
                Box(g, gx, -hh * 0.55f, gx + 7.5f * d, -hh * 0.3f, 1f * d, col);
                Box(g, gx + 3.3f * d, -hh * 0.55f, gx + 4.2f * d, -hh * 0.3f, 0, Color.FromArgb(0xAA, 255, 255, 255));
            }
            Wheel(g, -l * 0.3f, hh * 0.4f, 3.9f * d); Wheel(g, l * 0.3f, hh * 0.4f, 3.9f * d);
            if (s.TrainLights)
            {
                int j = 0;
                for (float lx = -l * 0.45f; lx <= l * 0.46f; lx += l * 0.18f, j++) Lamp(g, lx, -hh * 0.32f, pal[(k + j) % pal.Length], Twinkle(k, j));
            }
        }

        // ---- slee

        void DrawSleigh(Graphics g)
        {
            var st = g.Save();
            g.TranslateTransform(sleighX, SleighY());
            g.ScaleTransform(d * sleighDir, d);
            pen.Width = 0.8f; pen.Color = Color.FromArgb(0x3B, 0x2A, 0x1A);
            g.DrawLine(pen, 8, -2, 108, -4);
            for (int i = 0; i < 3; i++) Reindeer(g, 40f + i * 34f, i);
            pen.Width = 1.8f; pen.Color = Color.FromArgb(0xE0, 0xB0, 0x40);
            path.Reset(); path.AddLine(-24, 10, 14, 10); Quad(path, 14, 10, 24, 10, 22, 2);
            g.DrawPath(pen, path);
            path.Reset();
            path.StartFigure();
            path.AddLine(-22, 8, -22, -8);
            Quad(path, -22, -8, -10, -2, 8, -4);
            path.AddLine(8, -4, 18, -6);
            Quad(path, 18, -6, 18, 6, 8, 8);
            path.CloseFigure();
            br.Color = Color.FromArgb(0xC6, 0x28, 0x28); g.FillPath(br, path);
            Circle(g, -15, -9, 7, Color.FromArgb(0x6B, 0x4A, 0x2B));
            Circle(g, -3, -10, 5.5f, Color.FromArgb(0xC6, 0x28, 0x28));
            Circle(g, -3, -17, 3.5f, Color.FromArgb(0xFF, 0xD2, 0xA8));
            br.Color = Color.FromArgb(0xC6, 0x28, 0x28);
            g.FillPolygon(br, new[] { new PointF(-6.5f, -18), new PointF(0.5f, -18), new PointF(-5, -26) });
            Circle(g, -5, -26, 1.8f, Color.White); Circle(g, -3, -14.5f, 2.6f, Color.White);
            g.Restore(st);
        }

        void Reindeer(Graphics g, float x, int i)
        {
            float gallop = (float)Math.Sin(time * 10f + i * 1.3f), bob = gallop * 1.2f;
            var st = g.Save();
            g.TranslateTransform(x, bob);
            pen.Width = 1.6f; pen.Color = Color.FromArgb(0x6B, 0x44, 0x23); pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            for (int l = 0; l < 4; l++)
            {
                int side = l < 2 ? -1 : 1;
                float a = gallop * 0.7f * (l % 2 == 0 ? 1 : -1);
                g.DrawLine(pen, side * 8f, 5f, side * 8f + (float)Math.Sin(a) * 6f, 15f);
            }
            br.Color = Color.FromArgb(0x8B, 0x5A, 0x2B);
            g.FillEllipse(br, -12, -6, 24, 12);
            Circle(g, 15, -9, 4.6f, Color.FromArgb(0x8B, 0x5A, 0x2B));
            pen.Width = 4f; g.DrawLine(pen, 10, -3, 14, -8);
            pen.Width = 1f; pen.Color = Color.FromArgb(0x5A, 0x3A, 0x1A);
            g.DrawLine(pen, 15, -13, 14, -20); g.DrawLine(pen, 14, -20, 11, -23); g.DrawLine(pen, 14, -18, 17, -22);
            Circle(g, 19.4f, -9, 1.7f, i == 2 ? Color.FromArgb(0xFF, 0x2A, 0x2A) : Color.FromArgb(0x2B, 0x1A, 0x0E));
            if (i == 2) Circle(g, 19.4f, -9, 5f, Color.FromArgb(0x55, 0xFF, 0x2A, 0x2A));
            g.Restore(st);
        }

        // ---- pinguïns en sneeuwman

        void DrawWalkers(Graphics g)
        {
            foreach (var pg in penguins)
            {
                float gy = GroundY(pg.X) - pg.HopY;
                var st = g.Save();
                g.TranslateTransform(pg.X, gy);
                g.ScaleTransform(d * pg.Dir, d);
                float wob = (pg.Pause > 0f || pg.HopY > 0f) ? 0f : (float)Math.Sin(time * 9f + pg.Phase) * 7f;
                g.RotateTransform(wob);
                br.Color = Color.FromArgb(0xFF, 0x9F, 0x1C);
                g.FillEllipse(br, -6, -2, 5, 3.5f); g.FillEllipse(br, 1, -2, 5, 3.5f);
                br.Color = Color.FromArgb(0x1D, 0x27, 0x33);
                g.FillEllipse(br, -6.5f, -21, 13, 20); Circle(g, 0, -21, 5, Color.FromArgb(0x1D, 0x27, 0x33));
                br.Color = Color.White; g.FillEllipse(br, -4.2f, -17, 8.4f, 15);
                Circle(g, 2, -22, 1.3f, Color.White); Circle(g, -1.2f, -22, 1.3f, Color.White);
                Circle(g, 2.3f, -22, 0.6f, Color.FromArgb(0x11, 0x11, 0x11)); Circle(g, -0.9f, -22, 0.6f, Color.FromArgb(0x11, 0x11, 0x11));
                br.Color = Color.FromArgb(0xFF, 0x9F, 0x1C);
                g.FillPolygon(br, new[] { new PointF(1, -19.5f), new PointF(7, -18.5f), new PointF(1, -17.5f) });
                br.Color = Color.FromArgb(0xC6, 0x28, 0x28); g.FillRectangle(br, -5, -16.5f, 10, 2);
                g.Restore(st);
            }
            float sy2 = GroundY(snowmanX);
            var sm = g.Save();
            g.TranslateTransform(snowmanX, sy2);
            g.ScaleTransform(d, d);
            g.RotateTransform(snowmanWiggle > 0f ? (float)Math.Sin(time * 25f) * 5f * snowmanWiggle : 0f);
            var snow = Color.FromArgb(0xF8, 0xFB, 0xFF);
            Circle(g, 0, -11, 11, snow); Circle(g, 0, -27, 8, snow); Circle(g, 0, -40, 6, snow);
            pen.Width = 0.8f; pen.Color = Color.FromArgb(0xB9, 0xCC, 0xE6);
            g.DrawEllipse(pen, -11, -22, 22, 22); g.DrawEllipse(pen, -8, -35, 16, 16); g.DrawEllipse(pen, -6, -46, 12, 12);
            pen.Width = 1.2f; pen.Color = Color.FromArgb(0x6B, 0x4A, 0x2B);
            g.DrawLine(pen, -7, -29, -17, -35); g.DrawLine(pen, 7, -29, 17, -35);
            var coal = Color.FromArgb(0x22, 0x22, 0x22);
            Circle(g, -2.2f, -41.5f, 0.9f, coal); Circle(g, 2.2f, -41.5f, 0.9f, coal);
            Circle(g, 0, -24, 0.9f, coal); Circle(g, 0, -28, 0.9f, coal); Circle(g, 0, -11, 0.9f, coal);
            br.Color = Color.FromArgb(0xFF, 0x7A, 0x00);
            g.FillPolygon(br, new[] { new PointF(0, -39.5f), new PointF(7, -38.5f), new PointF(0, -37.5f) });
            br.Color = coal; g.FillRectangle(br, -5, -50, 10, 4.5f); g.FillRectangle(br, -7.5f, -46, 15, 1.5f);
            br.Color = Color.FromArgb(0xC6, 0x28, 0x28); g.FillRectangle(br, -6.5f, -35, 13, 2.5f);
            float fl = (float)Math.Sin(time * 3f) * 2f;
            g.FillPolygon(br, new[] { new PointF(3, -33), new PointF(5.5f, -33), new PointF(7 + fl, -24), new PointF(4.5f + fl, -24) });
            g.Restore(sm);
        }

        public void Dispose()
        {
            if (staticBack != null) { staticBack.Dispose(); staticBack = null; }
            if (marqueeFont != null) { marqueeFont.Dispose(); marqueeFont = null; }
            br.Dispose(); pen.Dispose(); path.Dispose();
        }
    }

    /// <summary>Korte, zelf gesynthetiseerde geluidjes (alleen als geluid is aangezet).</summary>
    public static class Sounds
    {
        const int Rate = 22050;
        static readonly Random rnd = new Random();

        static void Play(bool enabled, double seconds, Func<double, double> gen)
        {
            if (!enabled) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int n = (int)(Rate * seconds);
                    var ms = new System.IO.MemoryStream();
                    var bw = new System.IO.BinaryWriter(ms);
                    bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + n * 2); bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                    bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(Rate); bw.Write(Rate * 2); bw.Write((short)2); bw.Write((short)16);
                    bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(n * 2);
                    for (int i = 0; i < n; i++)
                    {
                        double v = Math.Max(-1.0, Math.Min(1.0, gen(i / (double)Rate)));
                        bw.Write((short)(v * 32000 * 0.5));
                    }
                    ms.Position = 0;
                    using (var sp = new System.Media.SoundPlayer(ms)) sp.PlaySync();
                }
                catch { }
            });
        }

        public static void Toot(bool on)
        {
            Play(on, 0.55, tt =>
            {
                double f = tt < 0.22 ? 392.0 : 523.25;
                double env = tt < 0.02 ? tt / 0.02 : Math.Exp(-(tt - 0.02) * 2.2);
                return (Math.Sin(2 * Math.PI * f * tt) + 0.5 * Math.Sin(2 * Math.PI * f * 2 * tt) + 0.25 * Math.Sin(2 * Math.PI * f * 3 * tt)) * env * 0.3;
            });
        }

        public static void Boom(bool on)
        {
            Play(on, 0.6, tt => ((rnd.NextDouble() * 2 - 1) * 0.6 + Math.Sin(2 * Math.PI * 70.0 * tt) * 0.5) * Math.Exp(-tt * 7.0) * 0.4);
        }

        public static void Ding(bool on)
        {
            double f = new[] { 523.25, 587.33, 659.25, 783.99, 880.0 }[rnd.Next(5)];
            Play(on, 0.7, tt => (Math.Sin(2 * Math.PI * f * tt) + 0.3 * Math.Sin(2 * Math.PI * f * 2.76 * tt)) * Math.Exp(-tt * 6.0) * 0.35);
        }
    }
}
