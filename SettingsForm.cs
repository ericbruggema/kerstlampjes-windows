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

        ComboBox mode, palette;
        TrackBar speed, size, spacing, corner, inset, sag, opacity;
        CheckBox allScreens, hideFull, autostart;

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

            var table = new TableLayoutPanel
            {
                ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));

            mode = Combo(Modes.Names);
            palette = Combo(Palettes.Names);
            AddRow(table, "Effect", mode);
            AddRow(table, "Kleuren", palette);
            speed = Slider(table, "Snelheid", 1, 10);
            size = Slider(table, "Grootte", 6, 40);
            spacing = Slider(table, "Afstand tussen lampjes", 20, 120);
            corner = Slider(table, "Hoekafronding", 0, 80);
            inset = Slider(table, "Afstand van de rand", 0, 40);
            sag = Slider(table, "Doorhang van de draad", 0, 20);
            opacity = Slider(table, "Doorzichtigheid", 30, 100);

            allScreens = Check(Tr.t("Op alle schermen"));
            hideFull = Check(Tr.t("Verbergen bij schermvullende apps (spellen, films)"));
            autostart = Check(Tr.t("Starten met Windows"));
            foreach (var c in new[] { allScreens, hideFull, autostart })
            {
                table.RowCount++;
                table.Controls.Add(c, 0, table.RowCount - 1);
                table.SetColumnSpan(c, 3);
            }

            var reset = new Button { Text = Tr.t("Standaardwaarden"), AutoSize = true };
            reset.Click += delegate { ResetToDefaults(); };
            var close = new Button { Text = Tr.t("Sluiten"), AutoSize = true, DialogResult = DialogResult.OK };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(reset);
            table.RowCount++;
            table.Controls.Add(buttons, 0, table.RowCount - 1);
            table.SetColumnSpan(buttons, 3);
            AcceptButton = close;

            Controls.Add(table);
            ClientSize = table.PreferredSize + new Size(Padding.Horizontal, Padding.Vertical);
            LoadValues();

            mode.SelectedIndexChanged += delegate { Commit(); };
            palette.SelectedIndexChanged += delegate { Commit(); };
            allScreens.CheckedChanged += delegate { Commit(); };
            hideFull.CheckedChanged += delegate { Commit(); };
            autostart.CheckedChanged += delegate { if (!loading) setAutostart(autostart.Checked); };
        }

        ComboBox Combo(string[] names)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            foreach (var n in names) c.Items.Add(Tr.t(n));
            return c;
        }

        CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 3) }; }

        static void AddRow(TableLayoutPanel t, string label, Control c)
        {
            t.RowCount++;
            int row = t.RowCount - 1;
            t.Controls.Add(new Label { Text = Tr.t(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, row);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            t.Controls.Add(c, 1, row);
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
            AddRow(t, label, tb);
            t.Controls.Add(val, 2, t.RowCount - 1);
            return tb;
        }

        void LoadValues()
        {
            loading = true;
            mode.SelectedIndex = s.Mode;
            palette.SelectedIndex = s.Palette;
            speed.Value = s.Speed; size.Value = s.Size; spacing.Value = s.Spacing; corner.Value = s.Corner;
            inset.Value = s.Inset; sag.Value = s.Sag; opacity.Value = s.Opacity;
            allScreens.Checked = s.AllScreens; hideFull.Checked = s.HideFullscreen;
            autostart.Checked = getAutostart();
            loading = false;
        }

        void Commit()
        {
            if (loading) return;
            s.Mode = Math.Max(0, mode.SelectedIndex);
            s.Palette = Math.Max(0, palette.SelectedIndex);
            s.Speed = speed.Value; s.Size = size.Value; s.Spacing = spacing.Value; s.Corner = corner.Value;
            s.Inset = inset.Value; s.Sag = sag.Value; s.Opacity = opacity.Value;
            s.AllScreens = allScreens.Checked; s.HideFullscreen = hideFull.Checked;
            changed();
        }

        void ResetToDefaults()
        {
            var d = new Settings();
            s.Mode = d.Mode; s.Palette = d.Palette; s.Speed = d.Speed; s.Size = d.Size; s.Spacing = d.Spacing;
            s.Corner = d.Corner; s.Inset = d.Inset; s.Sag = d.Sag; s.Opacity = d.Opacity;
            s.AllScreens = d.AllScreens; s.HideFullscreen = d.HideFullscreen;
            LoadValues();
            changed();
        }
    }
}
