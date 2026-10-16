using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Kerstlampjes
{
    static class Program
    {
        public const string Repo = "ericbruggema/kerstlampjes";
        public const string RepoUrl = "https://github.com/" + Repo;

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor;
            }
        }

        [STAThread]
        static void Main()
        {
            bool first;
            using (new Mutex(true, "Kerstlampjes.Windows.SingleInstance", out first))
            {
                if (!first) return; // draait al: het icoon staat in het systeemvak
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }

        /// <summary>Een klein lampje als icoon (aan = kleurig, uit = grijs).</summary>
        public static Icon AppIcon(bool on)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var col = on ? Color.FromArgb(0xFF, 0x3B, 0x30) : Color.FromArgb(0x88, 0x88, 0x88);
                    var glow = on ? Color.FromArgb(0xFF, 0xD5, 0x4F) : Color.FromArgb(0xAA, 0xAA, 0xAA);
                    using (var b = new SolidBrush(Color.FromArgb(0x2B, 0x3A, 0x30)))
                        g.FillRectangle(b, 12, 1, 8, 7);
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(16, 6, 30, 6, 28, 20, 16, 30);
                        path.AddBezier(16, 30, 4, 20, 2, 6, 16, 6);
                        using (var b = new LinearGradientBrush(new Rectangle(0, 4, 32, 28), glow, col, 70f))
                            g.FillPath(b, path);
                    }
                    using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                        g.FillEllipse(b, 9, 10, 5, 9);
                }
                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }
    }

    /// <summary>Het systeemvak-icoon, de overlays en alles wat daarbij hoort.</summary>
    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly Settings s = Settings.Load();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly List<Overlay> overlays = new List<Overlay>();
        readonly System.Windows.Forms.Timer frame = new System.Windows.Forms.Timer { Interval = 40 };
        readonly System.Windows.Forms.Timer slow = new System.Windows.Forms.Timer { Interval = 500 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ToolStripMenuItem onItem, autoItem;
        SettingsForm settingsForm;
        bool hiddenByFullscreen, rebuildPending, dirty = true;
        Icon iconOn, iconOff;
        IntPtr hook;
        Native.HookProc hookProc;
        readonly Queue<Point> clicks = new Queue<Point>();
        int lastX, lastDir, reversals;
        long wiggleStart;

        public TrayApp()
        {
            iconOn = Program.AppIcon(true);
            iconOff = Program.AppIcon(false);

            var menu = new ContextMenuStrip();
            onItem = new ToolStripMenuItem(Tr.t("Lampjes aan")) { CheckOnClick = false };
            onItem.Click += delegate { Toggle(); };
            var settingsItem = new ToolStripMenuItem(Tr.t("Instellingen…"));
            settingsItem.Click += delegate { ShowSettings(); };
            var fireItem = new ToolStripMenuItem(Tr.t("Vuurwerk afsteken"));
            fireItem.Click += delegate { foreach (var o in overlays) o.Fx.LaunchFirework(); };
            var icicleItem = new ToolStripMenuItem(Tr.t("Ijspegels laten vallen"));
            icicleItem.Click += delegate { foreach (var o in overlays) o.Fx.DropIcicles(); };
            var snowItem = new ToolStripMenuItem(Tr.t("Sneeuw wegvegen"));
            snowItem.Click += delegate { foreach (var o in overlays) o.Fx.Shake(); };
            autoItem = new ToolStripMenuItem(Tr.t("Starten met Windows"));
            autoItem.Click += delegate { SetAutostart(!GetAutostart()); };
            var updItem = new ToolStripMenuItem(Tr.t("Controleren op updates"));
            updItem.Click += delegate { CheckUpdates(); };
            var aboutItem = new ToolStripMenuItem(Tr.t("Over Kerstlampjes…"));
            aboutItem.Click += delegate { ShowAbout(); };
            var exitItem = new ToolStripMenuItem(Tr.t("Afsluiten"));
            exitItem.Click += delegate { ExitThread(); };
            menu.Items.AddRange(new ToolStripItem[]
            {
                onItem, settingsItem, new ToolStripSeparator(), fireItem, icicleItem, snowItem, new ToolStripSeparator(), autoItem, updItem, aboutItem,
                new ToolStripSeparator(), exitItem
            });
            menu.Opening += delegate
            {
                onItem.Checked = s.On;
                autoItem.Checked = GetAutostart();
            };

            tray.ContextMenuStrip = menu;
            tray.Text = Truncate(Tr.t("Kerstlampjes"), 63);
            tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
            tray.Visible = true;
            UpdateTrayIcon();

            frame.Tick += delegate { Tick(); };
            slow.Tick += delegate { SlowTick(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { rebuildPending = true; };

            RebuildOverlays();
            UpdateMic();
            hookProc = MouseHook;
            hook = Native.SetWindowsHookEx(14, hookProc, Native.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
            frame.Start();
            slow.Start();
        }

        static string Truncate(string t, int n) { return t.Length <= n ? t : t.Substring(0, n); }

        void UpdateTrayIcon()
        {
            tray.Icon = s.On ? iconOn : iconOff;
            // Tooltip met uitleg (max. 63 tekens in Windows): naam + aan/uit
            tray.Text = Truncate(Tr.t("Kerstlampjes"), 40) + (s.On ? "" : " (" + Tr.t("uit") + ")");
        }

        void Toggle()
        {
            s.On = !s.On;
            s.Save();
            UpdateTrayIcon();
            ApplyVisibility();
            UpdateMic();
        }

        void RebuildOverlays()
        {
            foreach (var o in overlays) { o.Hide(); o.Dispose(); }
            overlays.Clear();
            var screens = s.AllScreens ? Screen.AllScreens : new[] { Screen.PrimaryScreen };
            foreach (var sc in screens)
            {
                var o = new Overlay(sc.WorkingArea);
                o.Apply(s);
                overlays.Add(o);
            }
            ApplyVisibility();
            dirty = true;
        }

        void ReapplySettings()
        {
            s.Save();
            if (overlays.Count != (s.AllScreens ? Screen.AllScreens.Length : 1)) { RebuildOverlays(); return; }
            foreach (var o in overlays) o.Apply(s);
            dirty = true;
            UpdateMic();
        }

        bool micWarned;

        /// <summary>De microfoon staat alleen open bij het effect "Op de muziek" terwijl de lampjes zichtbaar zijn.</summary>
        void UpdateMic()
        {
            bool want = s.Mode == Modes.Music && s.On && !hiddenByFullscreen;
            if (want && !MicLevel.Running)
            {
                if (!MicLevel.Start() && !micWarned)
                {
                    micWarned = true;
                    MessageBox.Show(Tr.t("De microfoon is niet beschikbaar. Controleer in de Windows-instellingen (Privacy en beveiliging, Microfoon) of apps toegang hebben."),
                        Tr.t("Kerstlampjes"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (!want && MicLevel.Running) MicLevel.Stop();
        }

        void ApplyVisibility()
        {
            bool show = s.On && !hiddenByFullscreen;
            foreach (var o in overlays)
            {
                if (show && !o.Visible) { o.Show(); o.KeepOnTop(); o.Render((float)clock.Elapsed.TotalSeconds); }
                else if (!show && o.Visible) o.Hide();
            }
            dirty = true;
            UpdateMic();
        }

        // Muisklikken lezen we mee zonder ze tegen te houden: ze gaan altijd door naar het venster eronder.
        IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam.ToInt32() == 0x201) // WM_LBUTTONDOWN
            {
                var info = (Native.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                if (clicks.Count < 8) clicks.Enqueue(new Point(info.pt.x, info.pt.y));
            }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        void HandleMouse()
        {
            while (clicks.Count > 0)
            {
                var pt = clicks.Dequeue();
                foreach (var o in overlays)
                    if (o.Bounds.Contains(pt) && o.Fx.Click(pt.X - o.Left, pt.Y - o.Top)) { dirty = true; break; }
            }
            if ((s.DecorMask & Decor.Pile) == 0) return;
            var pos = Cursor.Position;
            // de muis door de sneeuw halen veegt hem weg
            foreach (var o in overlays)
                if (o.Bounds.Contains(pos)) o.Fx.Plow(pos.X - o.Left, pos.Y - o.Top);
            // de muis heen en weer schudden haalt alle sneeuw weg
            int dx = pos.X - lastX; lastX = pos.X;
            long now = clock.ElapsedMilliseconds;
            if (Math.Abs(dx) > 30)
            {
                int dir = dx > 0 ? 1 : -1;
                if (lastDir != 0 && dir != lastDir) { if (reversals == 0) wiggleStart = now; reversals++; }
                lastDir = dir;
            }
            if (reversals > 0 && now - wiggleStart > 900) reversals = 0;
            if (reversals >= 6) { reversals = 0; foreach (var o in overlays) o.Fx.Shake(); }
        }

        void Tick()
        {
            if (rebuildPending) { rebuildPending = false; RebuildOverlays(); }
            if (!s.On || hiddenByFullscreen) { clicks.Clear(); return; }
            HandleMouse();
            MicLevel.Poll();
            bool animated = overlays.Count > 0 && overlays[0].Animated;
            // Vast licht verandert niet: alleen opnieuw tekenen als er iets gewijzigd is.
            if (!animated && !dirty) return;
            dirty = false;
            float t = (float)clock.Elapsed.TotalSeconds;
            foreach (var o in overlays) o.Render(t);
        }

        void SlowTick()
        {
            bool full = false;
            if (s.HideFullscreen)
            {
                int state;
                // 2 = volledig scherm, 3 = Direct3D-volledig scherm, 4 = presentatie
                if (Native.SHQueryUserNotificationState(out state) == 0) full = state == 2 || state == 3 || state == 4;
            }
            if (s.DesktopOnly && !Native.DesktopInFront()) full = true;
            if (full != hiddenByFullscreen) { hiddenByFullscreen = full; ApplyVisibility(); }
            else if (s.On && !full) foreach (var o in overlays) o.KeepOnTop();
        }

        void ShowSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Show(); settingsForm.Activate(); return; }
            settingsForm = new SettingsForm(s, ReapplySettings, GetAutostart, SetAutostart);
            settingsForm.FormClosed += delegate { settingsForm = null; };
            settingsForm.Show();
        }

        static bool GetAutostart()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("Kerstlampjes") != null; }
            catch { return false; }
        }

        static void SetAutostart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("Kerstlampjes", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("Kerstlampjes", false);
                }
            }
            catch { }
        }

        void ShowAbout()
        {
            new AboutForm().ShowDialog();
        }

        void CheckUpdates()
        {
            // Alleen op verzoek: dit is het enige moment dat er iets via internet gaat.
            var ui = SynchronizationContext.Current;
            new Thread(() =>
            {
                string latest = null, page = null;
                try
                {
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
                    var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Program.Repo + "/releases?per_page=30");
                    req.UserAgent = "Kerstlampjes-Windows";
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = 8000; req.ReadWriteTimeout = 8000;
                    string json;
                    using (var r = req.GetResponse())
                    using (var sr = new System.IO.StreamReader(r.GetResponseStream()))
                        json = sr.ReadToEnd();
                    // eerste release met tag "windows-vX.Y" (nieuwste staat bovenaan)
                    var m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"windows-v([0-9.]+)\"");
                    if (m.Success)
                    {
                        latest = m.Groups[1].Value;
                        page = Program.RepoUrl + "/releases/tag/windows-v" + latest;
                    }
                }
                catch { }
                ui.Post(_ => ShowUpdateResult(latest, page), null);
            }) { IsBackground = true }.Start();
        }

        static bool IsNewer(string latest, string current)
        {
            Version a, b;
            if (!Version.TryParse(latest, out a) || !Version.TryParse(current, out b)) return false;
            return a > b;
        }

        static void ShowUpdateResult(string latest, string page)
        {
            string name = Tr.t("Kerstlampjes");
            if (latest == null)
                MessageBox.Show(Tr.t("Controleren is niet gelukt. Ben je online?"), name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (IsNewer(latest, Program.Version))
            {
                if (MessageBox.Show(Tr.f("Er is een nieuwe versie: {0} (jij hebt {1}).\n\nDownloadpagina openen?", latest, Program.Version),
                        name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start(page);
            }
            else
                MessageBox.Show(Tr.f("Je hebt de nieuwste versie ({0}).", Program.Version), name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void ExitThreadCore()
        {
            frame.Stop(); slow.Stop();
            MicLevel.Stop();
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            foreach (var o in overlays) o.Dispose();
            settingsForm?.Dispose();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }
    }

    class AboutForm : Form
    {
        public AboutForm()
        {
            Text = Tr.t("Over") + " – " + Tr.t("Kerstlampjes");
            Icon = Program.AppIcon(true);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            Padding = new Padding(16);

            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill
            };
            var title = new Label
            {
                Text = "🎄 " + Tr.t("Kerstlampjes") + "  " + Tr.t("Versie") + " " + Program.Version,
                AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.Controls.Add(title);
            panel.Controls.Add(Para(Tr.t("Een snoer kerstlampjes langs de rand van je scherm. Het blijft altijd zichtbaar, klikt niet in de weg en zit in het systeemvak (rechtsonder, bij de klok): daar zet je het uit, past de instellingen aan en lees je dit.")));
            panel.Controls.Add(Para(Tr.t("Er gaat niets via internet, behalve als je zelf op “Controleren op updates” klikt.")));
            panel.Controls.Add(Para(Tr.t("Gratis en open source (MIT-licentie). Gebruik je code of ideeën uit dit project? Vermeld dan een verwijzing naar:")));
            var link = new LinkLabel { Text = Program.RepoUrl, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            link.LinkClicked += delegate { Process.Start(Program.RepoUrl); };
            panel.Controls.Add(link);
            var ok = new Button { Text = Tr.t("Sluiten"), AutoSize = true };
            ok.Click += delegate { Close(); };
            panel.Controls.Add(ok);
            AcceptButton = ok;
            Controls.Add(panel);
            ClientSize = panel.PreferredSize + new Size(Padding.Horizontal, Padding.Vertical);
        }

        Label Para(string text)
        {
            return new Label { Text = text, MaximumSize = new Size(420, 0), AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
        }
    }
}
