using System;
using System.Collections.Generic;
using System.Drawing;

namespace Kerstlampjes
{
    /// <summary>Soorten vuurwerkshows (instelling "Vuurwerkshow").</summary>
    public static class FireShows
    {
        public const int Random = 0, Rockets = 1, Fountains = 2, Mortars = 3, Candles = 4, Wheels = 5, Grand = 6;
        public static readonly string[] Names =
            { "Willekeurig", "Pijlen", "Fonteinen", "Vuurpotten", "Romeinse kaarsen", "Vuurraden", "Grote show" };
    }

    /// <summary>
    /// Vuurwerk: pijlen, vuurpotten (mortieren), fonteinen, Romeinse kaarsen, vuurraden en een
    /// gechoreografeerde grote show. Alles komt op willekeurige plekken langs de onderrand.
    /// </summary>
    public partial class RimDecor
    {
        class Shot { public float At; public Action Do; }
        class Emitter { public float X, Y, Age, Life, Angle, Hue, Timer; public int Kind; }

        readonly List<Shot> queue = new List<Shot>();
        readonly List<Emitter> emitters = new List<Emitter>();
        long lastBoomMs;

        float LaunchX() { return R(w * 0.06f, w * 0.94f); }

        void Schedule(float delay, Action a) { queue.Add(new Shot { At = time + delay, Do = a }); }

        // ------------------------------------------------------------------ losse onderdelen

        void ShootRocket(float x, float vx, float vy, float ty, int style, float hue, int count)
        {
            rockets.Add(new Rocket { X = x, Y = h, VX = vx, VY = vy, TY = ty, Style = style, Hue = hue, Count = count });
        }

        void Rocket1(float x)
        {
            ShootRocket(x, R(-30f, 30f) * d, -R(420f, 620f) * d, R(h * 0.15f, h * 0.5f), -1, -1f, 0);
        }

        void Mortars(float x)
        {
            float hue = R(0f, 360f);
            for (int i = 0; i < 5; i++)
            {
                float xx = x + R(-10f, 10f) * d, vx = R(-90f, 90f) * d, vy = -R(520f, 680f) * d, ty = R(h * 0.2f, h * 0.45f);
                int style = rnd.Next(5);
                Schedule(i * 0.22f, () => ShootRocket(xx, vx, vy, ty, style, hue, 40));
            }
        }

        void Fan(float x)
        {
            float hue = R(0f, 360f);
            for (int i = 0; i < 7; i++)
                ShootRocket(x, (i - 3) * 50f * d, -R(540f, 580f) * d, R(h * 0.25f, h * 0.4f), 0, hue + i * 12f, 36);
        }

        void Candle(float x, int dir)
        {
            for (int i = 0; i < 8; i++)
            {
                float hue = i * 45f, vx = dir * R(40f, 90f) * d, vy = -R(380f, 460f) * d, ty = R(h * 0.35f, h * 0.6f);
                Schedule(i * 0.2f, () => ShootRocket(x, vx, vy, ty, 0, hue, 14));
            }
        }

        void Fountain(float x)
        {
            emitters.Add(new Emitter { X = x, Y = h - 2f * d, Life = R(4f, 6f), Kind = 0 });
        }

        void Wheel(float x)
        {
            emitters.Add(new Emitter { X = x, Y = R(h * 0.55f, h * 0.8f), Life = 5f, Kind = 1, Hue = R(0f, 360f) });
        }

        void Finale(int rocketsCount)
        {
            for (int i = 0; i < rocketsCount; i++)
                Schedule(R(0f, 3f), () => ShootRocket(LaunchX(), R(-40f, 40f) * d, -R(480f, 660f) * d, R(h * 0.15f, h * 0.5f), -1, -1f, 46));
        }

        // ------------------------------------------------------------------ show en planning

        /// <summary>De grote show: ongeveer 32 seconden met alles erin.</summary>
        void StartGrandShow()
        {
            Schedule(0f, () => { Fountain(w * 0.12f); Fountain(w * 0.88f); });
            Schedule(2f, () => Mortars(w * 0.3f));
            Schedule(3.5f, () => Mortars(w * 0.7f));
            Schedule(6f, () => { Wheel(w * 0.08f); Wheel(w * 0.92f); });
            Schedule(8f, () => Fan(w * 0.5f));
            Schedule(11f, () => { Candle(w * 0.15f, 1); Candle(w * 0.85f, -1); });
            for (int i = 0; i < 6; i++) Schedule(15f + i * 0.5f, () => Rocket1(LaunchX()));
            Schedule(20f, () => { Mortars(w * 0.5f); Fountain(w * 0.25f); Fountain(w * 0.75f); });
            Schedule(24f, () => { Fan(w * 0.3f); Fan(w * 0.7f); });
            Schedule(27f, () => Finale(16));
        }

        void RandomAction(int mode)
        {
            if (mode == FireShows.Random)
            {
                double p = rnd.NextDouble();
                if (p < 0.35) Rocket1(LaunchX());
                else if (p < 0.5) Mortars(LaunchX());
                else if (p < 0.65) { Fountain(LaunchX()); if (rnd.Next(2) == 0) Fountain(LaunchX()); }
                else if (p < 0.75) { float x = LaunchX(); Candle(x, x < w / 2f ? 1 : -1); }
                else if (p < 0.85) Wheel(LaunchX());
                else if (p < 0.95) Fan(LaunchX());
                else Finale(6);
                return;
            }
            switch (mode)
            {
                case FireShows.Rockets: Rocket1(LaunchX()); if (rnd.Next(3) == 0) Rocket1(LaunchX()); break;
                case FireShows.Fountains: for (int i = 0, n = 1 + rnd.Next(3); i < n; i++) Fountain(LaunchX()); break;
                case FireShows.Mortars: Mortars(LaunchX()); break;
                case FireShows.Candles: { float x = LaunchX(); Candle(x, x < w / 2f ? 1 : -1); break; }
                case FireShows.Wheels: Wheel(LaunchX()); break;
            }
        }

        /// <summary>Handmatig afsteken (tray-menu): de grote show, of een onderdeel van de gekozen soort.</summary>
        public void LaunchFirework()
        {
            if (w <= 0) return;
            if (s.FireShow == FireShows.Grand) StartGrandShow(); else RandomAction(s.FireShow);
        }

        void UpdateFireworks(float dt)
        {
            if (On(Decor.Fireworks))
            {
                fireT -= dt;
                if (fireT <= 0f)
                {
                    if (s.FireShow == FireShows.Grand)
                    {
                        if (queue.Count == 0 && emitters.Count == 0 && sparks.Count < 60) { StartGrandShow(); fireT = 32f + R(15f, 35f); }
                        else fireT = 2f;
                    }
                    else { RandomAction(s.FireShow); fireT = R(1.2f, 5.5f); }
                }
            }

            for (int i = queue.Count - 1; i >= 0; i--)
                if (time >= queue[i].At) { var a = queue[i].Do; queue.RemoveAt(i); a(); }

            for (int i = rockets.Count - 1; i >= 0; i--)
            {
                var rk = rockets[i];
                rk.X += rk.VX * dt; rk.Y += rk.VY * dt;
                if (rnd.Next(2) == 0 && sparks.Count < 900)
                    sparks.Add(new Spark { X = rk.X, Y = rk.Y, VX = R(-12f, 12f) * d, VY = R(10f, 40f) * d, Life = R(0.25f, 0.5f), Col = Color.FromArgb(0xFF, 0xD0, 0x80), Size = 1.2f * d, Grav = 40f * d });
                if (rk.Y <= rk.TY) { Burst(rk.X, rk.Y, rk.Style, rk.Hue, rk.Count); rockets.RemoveAt(i); }
            }

            for (int i = emitters.Count - 1; i >= 0; i--)
            {
                var e = emitters[i];
                e.Age += dt;
                if (e.Age > e.Life) { emitters.RemoveAt(i); continue; }
                e.Timer -= dt;
                float interval = e.Kind == 0 ? 0.012f : 0.008f;
                if (e.Kind == 1) e.Angle += 9f * dt;
                while (e.Timer <= 0f)
                {
                    e.Timer += interval;
                    if (sparks.Count > 900) continue;
                    if (e.Kind == 0)
                    {
                        float a = -(float)Math.PI / 2f + R(-0.22f, 0.22f), sp = R(260f, 380f) * d;
                        sparks.Add(new Spark { X = e.X, Y = e.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp, Life = R(0.5f, 0.9f), Col = Hsv(R(30f, 55f), R(0.1f, 0.5f), 1f), Size = R(1.2f, 2.2f) * d, Grav = 520f * d });
                    }
                    else
                    {
                        for (int arm = 0; arm < 2; arm++)
                        {
                            float a = e.Angle + arm * (float)Math.PI + R(-0.1f, 0.1f), sp = R(120f, 200f) * d;
                            sparks.Add(new Spark { X = e.X, Y = e.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp, Life = R(0.5f, 0.8f), Col = Hsv((e.Hue + e.Age * 70f) % 360f, 0.6f, 1f), Size = R(1.4f, 2.4f) * d, Grav = 120f * d });
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ knallen

        void Burst(float x, float y, int style = -1, float hue = -1f, int count = 0)
        {
            long now = clock.ElapsedMilliseconds;
            if (now - lastBoomMs > 150) { lastBoomMs = now; Sounds.Boom(s.Sound); }
            if (hue < 0f) hue = R(0f, 360f);
            if (style < 0) style = rnd.Next(5);
            switch (style)
            {
                case 1: Ring(x, y, count > 0 ? count : 46, 130f, hue, 1.0f, 1.8f, 0f); break;
                case 2: // treurwilg: gouden vonken met luchtweerstand, vallen langzaam
                    for (int i = 0; i < (count > 0 ? count : 44); i++)
                    {
                        float a = R(0f, 6.283f), sp = R(60f, 150f) * d;
                        Add(x, y, (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp, R(2.2f, 3.2f), Hsv(R(36f, 46f), R(0.35f, 0.6f), 1f), R(1.4f, 2.4f) * d, 45f * d, 1.1f);
                    }
                    break;
                case 3: Ring(x, y, 30, 90f, hue, 1.1f, 1.8f, 0f); Ring(x, y, 34, 150f, hue + 180f, 1.1f, 1.8f, 0f); break;
                case 4: // hartje
                    for (int i = 0; i < 44; i++)
                    {
                        float t = (float)(2 * Math.PI * i / 44);
                        float vx = 16f * (float)Math.Pow(Math.Sin(t), 3), vy = -(13f * (float)Math.Cos(t) - 5f * (float)Math.Cos(2 * t) - 2f * (float)Math.Cos(3 * t) - (float)Math.Cos(4 * t));
                        Add(x, y, vx * 8f * d, vy * 8f * d, R(1.3f, 1.7f), Hsv((hue < 30f || hue > 330f) ? hue : 350f, 0.7f, 1f), R(1.8f, 2.8f) * d, 60f * d, 0f);
                    }
                    break;
                default:
                    for (int i = 0; i < (count > 0 ? count : 46); i++)
                    {
                        float a = (float)(2 * Math.PI * i / (count > 0 ? count : 46)) + R(-0.05f, 0.05f), sp = R(50f, 170f) * d;
                        Add(x, y, (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp, R(1.0f, 1.8f), Hsv((hue + R(-25f, 25f) + 360f) % 360f, R(0.45f, 0.9f), 1f), R(1.6f, 3.0f) * d, 85f * d, 0f);
                    }
                    break;
            }
        }

        void Ring(float x, float y, int n, float speed, float hue, float lifeMin, float lifeMax, float drag)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)(2 * Math.PI * i / n) + R(-0.04f, 0.04f);
                Add(x, y, (float)Math.Cos(a) * speed * d, (float)Math.Sin(a) * speed * d, R(lifeMin, lifeMax), Hsv((hue % 360f + 360f) % 360f, R(0.5f, 0.9f), 1f), R(1.8f, 3f) * d, 85f * d, drag);
            }
        }

        void Add(float x, float y, float vx, float vy, float life, Color col, float size, float grav, float drag)
        {
            if (sparks.Count > 900) return;
            sparks.Add(new Spark { X = x, Y = y, VX = vx, VY = vy, Life = life, Col = col, Size = size, Grav = grav, Drag = drag });
        }

        void DrawEmitters(Graphics g)
        {
            foreach (var e in emitters)
            {
                float f = 1f - e.Age / e.Life;
                Circle(g, e.X, e.Y, 5f * d, Color.FromArgb(A(90 * f + 40), 255, 190, 90));
                Circle(g, e.X, e.Y, 2.2f * d, Color.FromArgb(A(255 * f), 255, 245, 210));
            }
        }
    }
}
