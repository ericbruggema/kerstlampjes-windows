using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kerstlampjes
{
    /// <summary>Instellingen: elke wijziging werkt meteen door en wordt bewaard.</summary>
    public class SettingsForm : Form
    {
        readonly Settings s;
        readonly Action changed;
        readonly Func<bool> getAutostart;
        readonly Action<bool> setAutostart;
        bool loading;

        ComboBox mode, palette, trainView, fireShow;
        TrackBar speed, size, spacing, corner, inset, sag, opacity, wagons;
        CheckBox snowFull, desktopOnly, allScreens, hideFull, autostart, showLights, trainLights, garlandBalls, sound;
        CheckBox[] decor;
        TextBox marquee;

        public SettingsForm(Settings settings, Action changed, Func<bool> getAutostart, Action<bool> setAutostart)
        {
            s = settings; this.changed = changed;
            this.getAutostart = getAutostart; this.setAutostart = setAutostart;

            Text = Tr.t("Instellingen…").TrimEnd('…') + " – " + Tr.t("Kerstlampjes");
            Icon = Program.AppIcon(true);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            Padding = new Padding(12);

            // ---- tabblad 1: lampjes
            var t1 = NewTable();
            mode = Combo(Modes.Names);
            palette = Combo(Palettes.Names);
            AddRow(t1, "Effect", mode);
            AddRow(t1, "Kleuren", palette);
            speed = Slider(t1, "Snelheid", 1, 10);
            size = Slider(t1, "Grootte", 6, 40);
            spacing = Slider(t1, "Afstand tussen lampjes", 20, 120);
            corner = Slider(t1, "Hoekafronding", 0, 80);
            inset = Slider(t1, "Afstand van de rand", 0, 40);
            sag = Slider(t1, "Doorhang van de draad", 0, 20);
            opacity = Slider(t1, "Doorzichtigheid", 30, 100);
            showLights = AddCheck(t1, Tr.t("Lampjes tonen"));
            allScreens = AddCheck(t1, Tr.t("Op alle schermen"));
            desktopOnly = AddCheck(t1, Tr.t("Alleen op het bureaublad (niet boven programma's)"));
            hideFull = AddCheck(t1, Tr.t("Verbergen bij schermvullende apps (spellen, films)"));
            autostart = AddCheck(t1, Tr.t("Starten met Windows"));

            // ---- tabblad 2: versiering
            var t2 = NewTable();
            decor = new CheckBox[Decor.Flags.Length];
            for (int i = 0; i < decor.Length; i++) decor[i] = AddCheck(t2, Tr.t(Decor.Names[i]));
            snowFull = AddCheck(t2, Tr.t("Sneeuw over het hele scherm"));
            fireShow = Combo(FireShows.Names);
            AddRow(t2, "Vuurwerkshow", fireShow);
            trainView = Combo(Decor.TrainViews);
            AddRow(t2, "Treintje", trainView);
            wagons = Slider(t2, "Aantal wagons", 1, 8);
            trainLights = AddCheck(t2, Tr.t("Lampjes op de trein"));
            garlandBalls = AddCheck(t2, Tr.t("Ballen in de slinger"));
            marquee = new TextBox { MaxLength = 80 };
            AddRow(t2, "Lichtkrant", marquee);
            sound = AddCheck(t2, Tr.t("Geluid"));

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var p1 = new TabPage(Tr.t("Lampjes")) { Padding = new Padding(8) };
            var p2 = new TabPage(Tr.t("Versiering")) { Padding = new Padding(8), AutoScroll = true };
            p1.Controls.Add(t1); p2.Controls.Add(t2);
            tabs.TabPages.Add(p1); tabs.TabPages.Add(p2);

            var reset = new Button { Text = Tr.t("Standaardwaarden"), AutoSize = true };
            reset.Click += delegate { ResetToDefaults(); };
            var close = new Button { Text = Tr.t("Sluiten"), AutoSize = true };
            close.Click += delegate { Close(); };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(reset);
            AcceptButton = close;

            Controls.Add(tabs);
            Controls.Add(buttons);
            int pageH = Math.Max(t1.PreferredSize.Height, t2.PreferredSize.Height);
            int pageW = Math.Max(t1.PreferredSize.Width, t2.PreferredSize.Width);
            ClientSize = new Size(pageW + 16 + Padding.Horizontal + 8,
                Math.Min(pageH, Screen.PrimaryScreen.WorkingArea.Height - 260) + 48 + buttons.PreferredSize.Height + Padding.Vertical);
            LoadValues();
            UpdateEnabled();

            EventHandler commit = delegate { Commit(); };
            mode.SelectedIndexChanged += commit; fireShow.SelectedIndexChanged += commit; palette.SelectedIndexChanged += commit; trainView.SelectedIndexChanged += commit;
            foreach (var c in new[] { snowFull, desktopOnly, allScreens, hideFull, showLights, trainLights, garlandBalls, sound }) c.CheckedChanged += commit;
            foreach (var c in decor) c.CheckedChanged += commit;
            marquee.TextChanged += commit;
            foreach (var c in decor) c.CheckedChanged += delegate { UpdateEnabled(); };
            autostart.CheckedChanged += delegate { if (!loading) setAutostart(autostart.Checked); };
        }

        static TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            return t;
        }

        ComboBox Combo(string[] names)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            foreach (var n in names) c.Items.Add(Tr.t(n));
            return c;
        }

        static CheckBox AddCheck(TableLayoutPanel t, string text)
        {
            var c = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            t.RowCount++;
            t.Controls.Add(c, 0, t.RowCount - 1);
            t.SetColumnSpan(c, 3);
            return c;
        }

        static void AddRow(TableLayoutPanel t, string label, Control c)
        {
            t.RowCount++;
            int row = t.RowCount - 1;
            t.Controls.Add(new Label { Text = Tr.t(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, row);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            t.Controls.Add(c, 1, row);
            t.SetColumnSpan(c, 2);
        }

        TrackBar Slider(TableLayoutPanel t, string label, int min, int max)
        {
            var tb = new TrackBar
            {
                Minimum = min, Maximum = max, TickStyle = TickStyle.None, AutoSize = false, Height = 28,
                Dock = DockStyle.Fill
            };
            var val = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = "0" };
            tb.ValueChanged += delegate { val.Text = tb.Value.ToString(); if (!loading) Commit(); };
            t.RowCount++;
            int row = t.RowCount - 1;
            t.Controls.Add(new Label { Text = Tr.t(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, row);
            t.Controls.Add(tb, 1, row);
            t.Controls.Add(val, 2, row);
            return tb;
        }

        void LoadValues()
        {
            loading = true;
            mode.SelectedIndex = s.Mode;
            palette.SelectedIndex = s.Palette;
            speed.Value = s.Speed; size.Value = s.Size; spacing.Value = s.Spacing; corner.Value = s.Corner;
            inset.Value = s.Inset; sag.Value = s.Sag; opacity.Value = s.Opacity;
            allScreens.Checked = s.AllScreens; hideFull.Checked = s.HideFullscreen; showLights.Checked = s.ShowLights; desktopOnly.Checked = s.DesktopOnly;
            autostart.Checked = getAutostart();
            for (int i = 0; i < decor.Length; i++) decor[i].Checked = (s.DecorMask & Decor.Flags[i]) != 0;
            fireShow.SelectedIndex = Math.Max(0, Math.Min(FireShows.Names.Length - 1, s.FireShow));
            trainView.SelectedIndex = s.TrainSide ? 1 : 0;
            wagons.Value = s.TrainWagons;
            snowFull.Checked = s.SnowFull; trainLights.Checked = s.TrainLights; garlandBalls.Checked = s.GarlandBalls; sound.Checked = s.Sound;
            marquee.Text = s.Marquee ?? "";
            loading = false;
        }

        /// <summary>Opties die bij een onderdeel horen zijn pas te kiezen als dat onderdeel aan staat.</summary>
        void UpdateEnabled()
        {
            Func<int, bool> on = f => decor[Array.IndexOf(Decor.Flags, f)].Checked;
            garlandBalls.Enabled = on(Decor.Garland);
            snowFull.Enabled = on(Decor.Snow);
            fireShow.Enabled = on(Decor.Fireworks);
            trainView.Enabled = wagons.Enabled = trainLights.Enabled = on(Decor.Train);
            marquee.Enabled = on(Decor.Marquee);
        }

        void Commit()
        {
            if (loading) return;
            s.Mode = Math.Max(0, mode.SelectedIndex);
            s.Palette = Math.Max(0, palette.SelectedIndex);
            s.Speed = speed.Value; s.Size = size.Value; s.Spacing = spacing.Value; s.Corner = corner.Value;
            s.Inset = inset.Value; s.Sag = sag.Value; s.Opacity = opacity.Value;
            s.AllScreens = allScreens.Checked; s.HideFullscreen = hideFull.Checked; s.ShowLights = showLights.Checked; s.DesktopOnly = desktopOnly.Checked;
            int mask = 0;
            for (int i = 0; i < decor.Length; i++) if (decor[i].Checked) mask |= Decor.Flags[i];
            s.DecorMask = mask;
            s.FireShow = Math.Max(0, fireShow.SelectedIndex);
            s.TrainSide = trainView.SelectedIndex == 1;
            s.TrainWagons = wagons.Value;
            s.SnowFull = snowFull.Checked; s.TrainLights = trainLights.Checked; s.GarlandBalls = garlandBalls.Checked; s.Sound = sound.Checked;
            s.Marquee = marquee.Text;
            changed();
        }

        void ResetToDefaults()
        {
            var d = new Settings();
            s.Mode = d.Mode; s.Palette = d.Palette; s.Speed = d.Speed; s.Size = d.Size; s.Spacing = d.Spacing;
            s.Corner = d.Corner; s.Inset = d.Inset; s.Sag = d.Sag; s.Opacity = d.Opacity;
            s.AllScreens = d.AllScreens; s.HideFullscreen = d.HideFullscreen; s.ShowLights = d.ShowLights; s.DesktopOnly = d.DesktopOnly;
            s.DecorMask = d.DecorMask; s.TrainSide = d.TrainSide; s.TrainWagons = d.TrainWagons;
            s.FireShow = d.FireShow; s.SnowFull = d.SnowFull; s.TrainLights = d.TrainLights; s.GarlandBalls = d.GarlandBalls; s.Sound = d.Sound; s.Marquee = d.Marquee;
            LoadValues();
            changed();
        }
    }
}
