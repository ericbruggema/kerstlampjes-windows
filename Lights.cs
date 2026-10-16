using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Kerstlampjes
{
    /// <summary>
    /// Tekent een snoer kerstlampjes langs de rand van een rechthoek (zelfde uiterlijk en effecten
    /// als LightsView in de Android-app). De lampjes hangen naar binnen.
    /// </summary>
    public class LightsRenderer : IDisposable
    {
        const int GlowLevels = 8;

        Settings s = new Settings();
        float scale = 1f;
        int w, h;

        int count;
        float[] px = new float[0], py = new float[0], ang = new float[0], phase = new float[0], freq = new float[0];
        int[] colorIdx = new int[0];

        GraphicsPath wire, glass;
        float glassCenterY, glowRadius, size;
        Bitmap[][] glow = new Bitmap[0][]; // [kleur][niveau]
        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly SolidBrush socketBrush = new SolidBrush(Color.FromArgb(0x2B, 0x3A, 0x30));
        Pen wirePen;

        readonly RimDecor fx = new RimDecor();
        /// <summary>De versiering (trein, sneeuw, vuurwerk, ...) voor klikken en andere bediening.</summary>
        public RimDecor Fx { get { return fx; } }

        public bool Animated { get { return s.Mode != Modes.Steady || fx.Active; } }

        public void Rebuild(Settings settings, int width, int height, float dpiScale)
        {
            s = settings.Clone();
            w = width; h = height; scale = dpiScale;
            fx.Layout(s, w, h, scale);
            Dispose(false);
            if (w <= 0 || h <= 0) return;

            size = s.Size * scale;
            float inset = s.Inset * scale + size * 0.15f;
            float spacing = Math.Max(s.Spacing * scale, size * 1.2f);
            float sag = s.Sag * scale;

            float x0 = inset, y0 = inset, x1 = w - inset, y1 = h - inset;
            float rw = x1 - x0, rh = y1 - y0;
            float r = Math.Max(0f, Math.Min(s.Corner * scale, Math.Min(rw, rh) / 2f));
            float len = 2 * (rw - 2 * r) + 2 * (rh - 2 * r) + 2 * (float)Math.PI * r;

            count = Math.Max(4, (int)(len / spacing));
            float step = len / count;

            px = new float[count]; py = new float[count]; ang = new float[count];
            colorIdx = new int[count]; phase = new float[count]; freq = new float[count];
            var palette = Palettes.Colors[s.Palette];
            var rnd = new Random(42);
            for (int i = 0; i < count; i++)
            {
                float x, y, tx, ty;
                PointTan(i * step, x0, y0, x1, y1, r, out x, out y, out tx, out ty);
                px[i] = x; py[i] = y;
                float nx = -ty, ny = tx;
                ang[i] = (float)(Math.Atan2(ny, nx) * 180.0 / Math.PI) - 90f;
                colorIdx[i] = i % palette.Length;
                phase[i] = (float)(rnd.NextDouble() * 2 * Math.PI);
                freq[i] = 0.35f + (float)rnd.NextDouble() * 0.9f;
            }

            // Draad: doorhangende boogjes tussen de lampjes (kwadratisch -> kubieke bézier)
            wire = new GraphicsPath();
            for (int i = 0; i < count; i++)
            {
                float mx, my, tx, ty;
                PointTan(i * step + step / 2f, x0, y0, x1, y1, r, out mx, out my, out tx, out ty);
                float cx = mx + (-ty) * sag * 2f, cy = my + tx * sag * 2f;
                int j = (i + 1) % count;
                float ax = px[i], ay = py[i], bx = px[j], by = py[j];
                wire.AddBezier(ax, ay,
                    ax + 2f / 3f * (cx - ax), ay + 2f / 3f * (cy - ay),
                    bx + 2f / 3f * (cx - bx), by + 2f / 3f * (cy - by),
                    bx, by);
            }
            wirePen = new Pen(Color.FromArgb(0x1F, 0x4D, 0x2B), Math.Max(1.5f, size * 0.14f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var layer = fx.StaticLayer;
            if (layer != null && s.ShowLights)
                using (var g2 = Graphics.FromImage(layer))
                {
                    g2.SmoothingMode = SmoothingMode.AntiAlias;
                    g2.DrawPath(wirePen, wire);
                }

            // Glazen lampje in lokale coördinaten (fitting op y=0, lampje wijst naar +y)
            float socketH = size * 0.45f, bw = size * 0.5f, bh = size * 1.35f, top = socketH * 0.9f;
            glass = new GraphicsPath();
            glass.AddBezier(0, top, bw * 1.25f, top, bw * 1.15f, top + bh * 0.62f, 0, top + bh);
            glass.AddBezier(0, top + bh, -bw * 1.15f, top + bh * 0.62f, -bw * 1.25f, top, 0, top);
            glass.CloseFigure();
            glassCenterY = top + bh * 0.5f;
            glowRadius = size * 2.4f;
            spriteD = (int)Math.Ceiling(2f * (glassCenterY + glowRadius)) + 4;

            // Gloed: voor elke kleur een paar vooraf getekende helderheidsniveaus
            int d = Math.Max(2, (int)Math.Ceiling(glowRadius * 2));
            glow = new Bitmap[palette.Length][];
            for (int c = 0; c < palette.Length; c++)
            {
                glow[c] = new Bitmap[GlowLevels];
                var col = Color.FromArgb(palette[c]);
                for (int l = 0; l < GlowLevels; l++)
                {
                    float f = (l + 1f) / GlowLevels;
                    var bmp = new Bitmap(d, d, PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(bmp))
                    using (var gp = new GraphicsPath())
                    {
                        gp.AddEllipse(0, 0, d - 1, d - 1);
                        using (var pgb = new PathGradientBrush(gp))
                        {
                            pgb.CenterPoint = new PointF(d / 2f, d / 2f);
                            pgb.InterpolationColors = new ColorBlend
                            {
                                Positions = new[] { 0f, 0.55f, 1f },
                                Colors = new[]
                                {
                                    Color.FromArgb(0, col),
                                    Color.FromArgb((int)(60 * f), col),
                                    Color.FromArgb((int)(170 * f), col)
                                }
                            };
                            g.FillEllipse(pgb, 0, 0, d - 1, d - 1);
                        }
                    }
                    glow[c][l] = bmp;
                }
            }
        }

        /// <summary>Punt en richting op afstand s langs de afgeronde rechthoek (met de klok mee).</summary>
        static void PointTan(float s, float x0, float y0, float x1, float y1, float r,
            out float x, out float y, out float tx, out float ty)
        {
            float sw = (x1 - x0) - 2 * r, sh = (y1 - y0) - 2 * r, arc = r * (float)Math.PI / 2f;
            float total = 2 * sw + 2 * sh + 4 * arc;
            s %= total; if (s < 0) s += total;
            float[] seg = { sw, arc, sh, arc, sw, arc, sh, arc };
            int k = 0;
            while (k < 7 && s >= seg[k]) { s -= seg[k]; k++; }
            switch (k)
            {
                case 0: x = x0 + r + s; y = y0; tx = 1; ty = 0; return;
                case 2: x = x1; y = y0 + r + s; tx = 0; ty = 1; return;
                case 4: x = x1 - r - s; y = y1; tx = -1; ty = 0; return;
                case 6: x = x0; y = y1 - r - s; tx = 0; ty = -1; return;
            }
            float cx = (k == 1 || k == 3) ? x1 - r : x0 + r;
            float cy = (k == 1 || k == 7) ? y0 + r : y1 - r;
            float th0 = -(float)Math.PI / 2f + (k - 1) / 2 * (float)Math.PI / 2f;
            float th = th0 + (r > 0 ? s / r : 0);
            x = cx + r * (float)Math.Cos(th); y = cy + r * (float)Math.Sin(th);
            tx = -(float)Math.Sin(th); ty = (float)Math.Cos(th);
        }

        float Intensity(int i, float t)
        {
            float sp = s.Speed / 5f;
            switch (s.Mode)
            {
                case Modes.Steady: return 1f;
                case Modes.Alternate:
                    {
                        float v = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t * sp * 0.5f + (i % 2) * Math.PI);
                        return 0.12f + 0.88f * Clamp01((v - 0.3f) / 0.4f);
                    }
                case Modes.Chase:
                    {
                        float k = (i - t * sp * 3f) % 4f;
                        if (k < 0) k += 4f;
                        return 0.12f + 0.88f * (float)Math.Exp(-k * k * 1.2f);
                    }
                case Modes.Wave:
                    {
                        float v = 0.5f + 0.5f * (float)Math.Sin(i * 0.35f - t * sp * 2.5f);
                        return 0.25f + 0.75f * v;
                    }
                default: // Twinkle
                    {
                        float v = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t * sp * freq[i] + phase[i]);
                        return 0.2f + 0.8f * (float)Math.Pow(v, 2.2);
                    }
            }
        }

        static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        public void Draw(Graphics g, float t)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            fx.Update();
            fx.DrawBack(g);
            if (s.ShowLights) DrawLights(g, t);
            fx.DrawFront(g);
        }

        // Elk lampje (gloed, fitting, glas en glimlicht) wordt per kleur, helderheid en hoek
        // een keer getekend en daarna als plaatje op het scherm gezet: dat is veel sneller.
        const int Levels = 8, AngleStep = 15;
        readonly System.Collections.Generic.Dictionary<int, Bitmap> sprites = new System.Collections.Generic.Dictionary<int, Bitmap>();
        int spriteD;

        Bitmap Sprite(int ci, int level, int bucket)
        {
            int key = (ci * Levels + level) * 64 + bucket;
            Bitmap bmp;
            if (sprites.TryGetValue(key, out bmp)) return bmp;
            if (sprites.Count > 900) { foreach (var b in sprites.Values) b.Dispose(); sprites.Clear(); }
            var palette = Palettes.Colors[s.Palette];
            var baseCol = Color.FromArgb(palette[ci]);
            float k = level / (float)(Levels - 1);
            float socketW = size * 0.42f, socketH = size * 0.45f;
            bmp = new Bitmap(spriteD, spriteD, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(spriteD / 2f, spriteD / 2f);
                g.RotateTransform(bucket * AngleStep);
                int gl = Math.Min(GlowLevels - 1, (int)(k * k * GlowLevels));
                int gd = glow[ci][gl].Width;
                g.DrawImageUnscaled(glow[ci][gl], (int)(-gd / 2f), (int)(glassCenterY - gd / 2f));
                g.FillRectangle(socketBrush, -socketW / 2, 0, socketW, socketH);
                brush.Color = Lerp(Darken(baseCol), baseCol, k);
                g.FillPath(brush, glass);
                brush.Color = Color.FromArgb((int)(60 + 140 * k), 255, 255, 255);
                g.FillEllipse(brush, -size * 0.28f, socketH + size * 0.15f, size * 0.2f, size * 0.5f);
            }
            sprites[key] = bmp;
            return bmp;
        }

        void DrawLights(Graphics g, float t)
        {
            if (count == 0 || wire == null) return;
            var palette = Palettes.Colors[s.Palette];
            int waveShift = s.Mode == Modes.Wave ? (int)(t * s.Speed / 5f * 1.5f) : 0;
            int half = spriteD / 2, buckets = 360 / AngleStep;

            for (int i = 0; i < count; i++)
            {
                int ci = (colorIdx[i] + waveShift) % palette.Length;
                int level = Math.Max(0, Math.Min(Levels - 1, (int)Math.Round(Intensity(i, t) * (Levels - 1))));
                int bucket = ((int)Math.Round(ang[i] / AngleStep) % buckets + buckets) % buckets;
                g.DrawImageUnscaled(Sprite(ci, level, bucket), (int)Math.Round(px[i]) - half, (int)Math.Round(py[i]) - half);
            }
        }

        static Color Darken(Color c)
        {
            return Color.FromArgb((int)(c.R * 0.28f + 20), (int)(c.G * 0.28f + 20), (int)(c.B * 0.28f + 20));
        }

        static Color Lerp(Color a, Color b, float f)
        {
            f = Clamp01(f);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
        }

        void Dispose(bool all)
        {
            if (wire != null) { wire.Dispose(); wire = null; }
            if (glass != null) { glass.Dispose(); glass = null; }
            if (wirePen != null) { wirePen.Dispose(); wirePen = null; }
            foreach (var row in glow) foreach (var b in row) if (b != null) b.Dispose();
            glow = new Bitmap[0][];
            foreach (var b in sprites.Values) b.Dispose();
            sprites.Clear();
            count = 0;
            if (all) { brush.Dispose(); socketBrush.Dispose(); fx.Dispose(); }
        }

        public void Dispose() { Dispose(true); }
    }
}
