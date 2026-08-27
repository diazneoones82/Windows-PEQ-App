using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MagicAudioDesktop
{
    internal enum ListeningMode
    {
        Speakers,
        Headphones
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0].Equals("--diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                AudioDiagnostics.Run(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Button startButton;
        private readonly Button stopButton;
        private readonly Button scanButton;
        private readonly Label statusLabel;
        private readonly MeterPanel meterPanel;
        private readonly TrackBar volumeSlider;
        private readonly TrackBar bassSlider;
        private readonly TrackBar trebleSlider;
        private readonly TrackBar widthSlider;
        private readonly TrackBar surroundSlider;
        private readonly TrackBar compressionSlider;
        private readonly ComboBox listeningModeCombo;
        private readonly CheckBox monitorOutputCheckBox;
        private readonly Button magicSignatureButton;
        private Panel magicControlsPanel;
        private Label magicNoteLabel;
        private CheckBox peqEnabledCheckBox;
        private ComboBox peqPresetCombo;
        private NumericUpDown peqPreampInput;
        private NumericUpDown peqLoudnessInput;
        private NumericUpDown peqStereoWidthInput;
        private NumericUpDown peqBassBoostInput;
        private NumericUpDown peqVirtualizerInput;
        private NumericUpDown peqReverbInput;
        private DataGridView peqBandsGrid;
        private TextBox peqConfigText;
        private Label peqStatusLabel;
        private FrequencyResponsePanel peqGraphPanel;
        private Panel peqEffectsPanel;
        private Panel autoEqPanel;
        private Button peqImportButton;
        private Button peqExportButton;
        private Button peqAddBandButton;
        private TextBox autoEqSearchText;
        private ComboBox autoEqSourceCombo;
        private ListView autoEqListView;
        private Label autoEqStatusLabel;
        private AutoEqProfile[] autoEqProfiles = new AutoEqProfile[0];
        private readonly AudioProcessor processor = new AudioProcessor();
        private readonly ProcessedAudioPlayer player = new ProcessedAudioPlayer();
        private readonly LiveExtensionScanner scanner = new LiveExtensionScanner();
        private WasapiLoopbackCapture capture;
        private bool scanning;
        private DateTime scanStartedAt;
        private static readonly Color ThemeBack = Color.FromArgb(14, 16, 20);
        private static readonly Color ThemePanel = Color.FromArgb(25, 28, 35);
        private static readonly Color ThemePanelAlt = Color.FromArgb(32, 36, 45);
        private static readonly Color ThemeText = Color.FromArgb(242, 245, 249);
        private static readonly Color ThemeMuted = Color.FromArgb(160, 169, 184);
        private static readonly Color ThemeBorder = Color.FromArgb(58, 64, 76);
        private static readonly Color ThemeOrange = Color.FromArgb(255, 139, 31);
        private const int DwmwaUseImmersiveDarkMode = 20;

        public MainForm()
        {
            Text = "Windows PEQ App";
            MinimumSize = new Size(980, 680);
            Size = new Size(1180, 740);
            BackColor = ThemeBack;
            Font = new Font("Segoe UI", 10f);

            var title = new Label
            {
                Text = "Windows PEQ App",
                Font = new Font("Segoe UI Semibold", 22f),
                AutoSize = true,
                ForeColor = ThemeText,
                Location = new Point(28, 24)
            };

            var subtitle = new Label
            {
                Text = "Desktop audio capture and local enhancement pipeline for Windows",
                ForeColor = ThemeMuted,
                AutoSize = true,
                Location = new Point(32, 72)
            };

            startButton = new Button
            {
                Text = "Start desktop audio",
                Location = new Point(32, 116),
                Size = new Size(172, 42),
                BackColor = ThemeOrange,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            startButton.FlatAppearance.BorderSize = 0;
            startButton.Click += StartButton_Click;

            stopButton = new Button
            {
                Text = "Stop",
                Location = new Point(216, 116),
                Size = new Size(96, 42),
                FlatStyle = FlatStyle.Flat
            };
            stopButton.Click += StopButton_Click;

            scanButton = new Button
            {
                Text = "Scan Edge extension",
                Location = new Point(326, 116),
                Size = new Size(176, 42),
                FlatStyle = FlatStyle.Flat
            };
            scanButton.Click += ScanButton_Click;

            statusLabel = new Label
            {
                Text = "Idle. Start playback in any Windows app, then press Start.",
                AutoSize = false,
                Location = new Point(32, 176),
                Size = new Size(520, 32),
                ForeColor = ThemeMuted
            };

            meterPanel = new MeterPanel
            {
                Location = new Point(32, 222),
                Size = new Size(360, 230)
            };

            magicControlsPanel = new Panel
            {
                Location = new Point(560, 150),
                Size = new Size(480, 430),
                BackColor = ThemePanel
            };
            magicControlsPanel.Paint += delegate(object sender, PaintEventArgs args)
            {
                using (var pen = new Pen(ThemeBorder))
                {
                    args.Graphics.DrawRectangle(pen, 0, 0, magicControlsPanel.Width - 1, magicControlsPanel.Height - 1);
                }
            };

            var modeLabel = new Label
            {
                Text = "Listening mode",
                AutoSize = true,
                ForeColor = ThemeText,
                Location = new Point(18, 18)
            };
            listeningModeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(178, 14),
                Size = new Size(260, 28)
            };
            listeningModeCombo.Items.Add("Speakers - ARCC");
            listeningModeCombo.Items.Add("Headphones - AREI");
            listeningModeCombo.SelectedIndex = 0;
            listeningModeCombo.SelectedIndexChanged += Sliders_Changed;

            monitorOutputCheckBox = new CheckBox
            {
                Text = "Play processed monitor output",
                Checked = true,
                Location = new Point(18, 54),
                Size = new Size(270, 28),
                ForeColor = ThemeText
            };

            magicSignatureButton = new Button
            {
                Text = "Magic acoustics",
                Location = new Point(306, 52),
                Size = new Size(132, 30),
                FlatStyle = FlatStyle.Flat
            };
            magicSignatureButton.Click += MagicSignatureButton_Click;

            magicControlsPanel.Controls.Add(modeLabel);
            magicControlsPanel.Controls.Add(listeningModeCombo);
            magicControlsPanel.Controls.Add(monitorOutputCheckBox);
            magicControlsPanel.Controls.Add(magicSignatureButton);

            volumeSlider = AddSlider(magicControlsPanel, "Master volume", 18, 96, 69, 0, 100);
            bassSlider = AddSlider(magicControlsPanel, "Bass lift", 18, 156, 18, 0, 100);
            trebleSlider = AddSlider(magicControlsPanel, "Treble lift", 18, 216, 24, 0, 100);
            widthSlider = AddSlider(magicControlsPanel, "Stereo width", 18, 276, 76, 0, 100);
            surroundSlider = AddSlider(magicControlsPanel, "Virtual surround", 18, 336, 68, 0, 100);
            compressionSlider = AddSlider(magicControlsPanel, "Compression", 18, 376, 36, 0, 100);

            foreach (var slider in new[] { volumeSlider, bassSlider, trebleSlider, widthSlider, surroundSlider, compressionSlider })
            {
                slider.Scroll += Sliders_Changed;
            }
            ApplyProcessorSettings();

            magicNoteLabel = new Label
            {
                Text = "This user-mode build captures the Windows mix through WASAPI loopback. Full system-wide replacement output requires a virtual audio driver or endpoint.",
                Location = new Point(32, 486),
                Size = new Size(880, 64),
                ForeColor = ThemeMuted
            };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(startButton);
            Controls.Add(stopButton);
            Controls.Add(scanButton);
            Controls.Add(statusLabel);
            Controls.Add(meterPanel);
            Controls.Add(magicControlsPanel);
            Controls.Add(magicNoteLabel);

            var existingControls = new Control[Controls.Count];
            Controls.CopyTo(existingControls, 0);
            Controls.Clear();

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(132, 34),
                SizeMode = TabSizeMode.Fixed
            };
            tabs.DrawItem += DrawDarkTab;
            var magicTab = new TabPage("Magic acoustics")
            {
                BackColor = BackColor,
                AutoScroll = true
            };
            magicTab.Resize += MagicTab_Resize;
            foreach (var control in existingControls)
            {
                magicTab.Controls.Add(control);
            }
            tabs.TabPages.Add(magicTab);
            var peqTab = CreatePeqTab();
            tabs.TabPages.Add(peqTab);
            Controls.Add(tabs);
            ApplyTheme(tabs);
            LayoutMagicTab(magicTab);
            LayoutPeqTab(peqTab);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryUseDarkTitleBar();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCapture();
            player.Dispose();
            base.OnFormClosing(e);
        }

        private void TryUseDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major < 10) return;
            int enabled = 1;
            DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        private TrackBar AddSlider(Control parent, string label, int x, int y, int value, int min, int max)
        {
            var caption = new Label
            {
                Text = label,
                AutoSize = true,
                ForeColor = ThemeText,
                Location = new Point(x, y)
            };
            var slider = new TrackBar
            {
                Location = new Point(x + 160, y - 8),
                Size = new Size(280, 44),
                Minimum = min,
                Maximum = max,
                TickFrequency = 10,
                Value = value
            };
            parent.Controls.Add(caption);
            parent.Controls.Add(slider);
            return slider;
        }

        private void MagicTab_Resize(object sender, EventArgs e)
        {
            var tab = sender as TabPage;
            if (tab != null)
            {
                LayoutMagicTab(tab);
            }
        }

        private void LayoutMagicTab(TabPage tab)
        {
            if (magicControlsPanel == null || magicNoteLabel == null) return;
            int margin = 32;
            int clientWidth = Math.Max(940, tab.ClientSize.Width);
            int clientHeight = Math.Max(620, tab.ClientSize.Height);
            int rightWidth = 560;
            int rightX = Math.Max(536, clientWidth - rightWidth - margin);
            int panelTop = 150;
            int panelHeight = Math.Min(430, Math.Max(390, clientHeight - panelTop - 52));

            startButton.SetBounds(margin, 116, 190, 42);
            stopButton.SetBounds(margin + 206, 116, 110, 42);
            scanButton.SetBounds(margin + 332, 116, 190, 42);
            statusLabel.SetBounds(margin, 176, Math.Max(420, rightX - margin - 28), 42);
            meterPanel.SetBounds(margin, 238, Math.Max(410, rightX - margin - 80), 230);
            magicControlsPanel.SetBounds(rightX, panelTop, rightWidth, panelHeight);
            magicNoteLabel.SetBounds(margin, Math.Max(486, clientHeight - 92), clientWidth - margin * 2, 64);

            int sliderWidth = Math.Max(280, magicControlsPanel.Width - 220);
            listeningModeCombo.SetBounds(188, 14, magicControlsPanel.Width - 220, 28);
            magicSignatureButton.SetBounds(magicControlsPanel.Width - 166, 52, 142, 30);
            foreach (Control control in magicControlsPanel.Controls)
            {
                var slider = control as TrackBar;
                if (slider != null)
                {
                    slider.Width = sliderWidth;
                }
            }
        }

        private void DrawDarkTab(object sender, DrawItemEventArgs e)
        {
            var tabControl = (TabControl)sender;
            bool selected = e.Index == tabControl.SelectedIndex;
            Rectangle rect = tabControl.GetTabRect(e.Index);
            rect.Inflate(-1, -1);

            using (var fill = new SolidBrush(selected ? ThemeOrange : ThemePanel))
            using (var border = new Pen(selected ? ThemeOrange : ThemeBorder))
            using (var text = new SolidBrush(selected ? Color.Black : ThemeText))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.FillRectangle(fill, rect);
                e.Graphics.DrawRectangle(border, rect);
                e.Graphics.DrawString(tabControl.TabPages[e.Index].Text, Font, text, rect, format);
            }
        }

        private void ApplyTheme(Control root)
        {
            root.BackColor = root is TextBox || root is ComboBox || root is NumericUpDown ? ThemePanelAlt : ThemeBack;
            root.ForeColor = ThemeText;

            var tab = root as TabPage;
            if (tab != null) tab.BackColor = ThemeBack;

            var panel = root as Panel;
            if (panel != null) panel.BackColor = ThemePanel;

            var label = root as Label;
            if (label != null)
            {
                label.BackColor = root.Parent == null ? ThemeBack : root.Parent.BackColor;
                label.ForeColor = label == statusLabel || label == magicNoteLabel || label == peqStatusLabel || label == autoEqStatusLabel ? ThemeMuted : ThemeText;
            }

            var checkBox = root as CheckBox;
            if (checkBox != null)
            {
                checkBox.BackColor = root.Parent == null ? ThemeBack : root.Parent.BackColor;
                checkBox.ForeColor = ThemeText;
            }

            var button = root as Button;
            if (button != null)
            {
                button.BackColor = button == startButton ? ThemeOrange : ThemePanelAlt;
                button.ForeColor = button == startButton ? Color.Black : ThemeText;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = ThemeBorder;
            }

            var textBox = root as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = ThemePanelAlt;
                textBox.ForeColor = ThemeText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }

            var combo = root as ComboBox;
            if (combo != null)
            {
                combo.BackColor = ThemePanelAlt;
                combo.ForeColor = ThemeText;
                combo.FlatStyle = FlatStyle.Flat;
            }

            var listView = root as ListView;
            if (listView != null)
            {
                listView.BackColor = ThemePanelAlt;
                listView.ForeColor = ThemeText;
            }

            var grid = root as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = ThemePanelAlt;
                grid.GridColor = ThemeBorder;
                grid.ForeColor = ThemeText;
                grid.DefaultCellStyle.BackColor = ThemePanelAlt;
                grid.DefaultCellStyle.ForeColor = ThemeText;
                grid.DefaultCellStyle.SelectionBackColor = ThemeOrange;
                grid.DefaultCellStyle.SelectionForeColor = Color.Black;
                grid.ColumnHeadersDefaultCellStyle.BackColor = ThemePanel;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeText;
                grid.EnableHeadersVisualStyles = false;
            }

            var numeric = root as NumericUpDown;
            if (numeric != null)
            {
                numeric.BackColor = ThemePanelAlt;
                numeric.ForeColor = ThemeText;
            }

            foreach (Control child in root.Controls)
            {
                ApplyTheme(child);
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                StartCapture();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not start audio capture: " + ex.Message;
                StopCapture();
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopCapture();
        }

        private void ScanButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (capture == null)
                {
                    StartCapture();
                }

                scanner.Reset();
                scanning = true;
                scanStartedAt = DateTime.UtcNow;
                scanButton.Enabled = false;
                statusLabel.Text = "Scanning Edge extension output for 6 seconds.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not start scan: " + ex.Message;
            }
        }

        private void StartCapture()
        {
            StopCapture();
            capture = new WasapiLoopbackCapture();
            capture.AudioAvailable += Capture_AudioAvailable;
            capture.CaptureStopped += Capture_CaptureStopped;
            capture.Start();
            startButton.Enabled = true;
            stopButton.Enabled = true;
            statusLabel.Text = "Capturing default Windows playback device.";
        }

        private void StopCapture()
        {
            if (capture != null)
            {
                capture.AudioAvailable -= Capture_AudioAvailable;
                capture.CaptureStopped -= Capture_CaptureStopped;
                capture.Dispose();
                capture = null;
            }
            player.Stop();

            startButton.Enabled = true;
            stopButton.Enabled = true;
            scanButton.Enabled = true;
            scanning = false;
            meterPanel.SetLevels(0, 0, 0, 0);
            statusLabel.Text = "Idle. Start playback in any Windows app, then press Start.";
        }

        private void Capture_CaptureStopped(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Capture_CaptureStopped(sender, e)));
                return;
            }
            StopCapture();
        }

        private void Capture_AudioAvailable(object sender, AudioBufferEventArgs e)
        {
            if (scanning)
            {
                scanner.Add(e.Buffer, e.BytesRecorded, e.WaveFormat);
                if ((DateTime.UtcNow - scanStartedAt).TotalSeconds >= 6)
                {
                    scanning = false;
                    var profile = scanner.BuildProfile();
                    BeginInvoke(new Action(delegate
                    {
                        ApplyScannedProfile(profile);
                    }));
                }
            }

            var result = processor.Process(e.Buffer, e.BytesRecorded, e.WaveFormat);
            if (monitorOutputCheckBox.Checked)
            {
                player.Play(result.Pcm16Stereo, e.WaveFormat.SampleRate);
            }

            if (IsDisposed) return;

            BeginInvoke(new Action(delegate
            {
                meterPanel.SetLevels(result.LeftInput, result.RightInput, result.LeftOutput, result.RightOutput);
                statusLabel.Text = string.Format("{0}: {1} Hz, {2} channels, {3}-bit desktop audio{4}.",
                    processor.Mode == ListeningMode.Speakers ? "Speakers ARCC" : "Headphones AREI",
                    e.WaveFormat.SampleRate, e.WaveFormat.Channels, e.WaveFormat.BitsPerSample,
                    monitorOutputCheckBox.Checked ? ", monitor playing" : "");
            }));
        }

        private void Sliders_Changed(object sender, EventArgs e)
        {
            ApplyProcessorSettings();
        }

        private void MagicSignatureButton_Click(object sender, EventArgs e)
        {
            ApplyMagicAcousticsPreset();
            monitorOutputCheckBox.Checked = true;
            statusLabel.Text = "Applied Magic acoustics basic signature.";
        }

        private void ApplyMagicAcousticsPreset()
        {
            listeningModeCombo.SelectedIndex = 0;
            volumeSlider.Value = 69;
            bassSlider.Value = 18;
            trebleSlider.Value = 24;
            widthSlider.Value = 76;
            surroundSlider.Value = 68;
            compressionSlider.Value = 36;
            ApplyProcessorSettings();
        }

        private void ApplyProcessorSettings()
        {
            processor.Volume = volumeSlider.Value / 50.0f;
            processor.Bass = bassSlider.Value / 100.0f;
            processor.Treble = trebleSlider.Value / 100.0f;
            processor.Width = widthSlider.Value / 50.0f;
            processor.Surround = surroundSlider.Value / 100.0f;
            processor.Compression = compressionSlider.Value / 100.0f;
            processor.Mode = listeningModeCombo.SelectedIndex == 1 ? ListeningMode.Headphones : ListeningMode.Speakers;
        }

        private void ApplyScannedProfile(ScannedProfile profile)
        {
            listeningModeCombo.SelectedIndex = profile.Mode == ListeningMode.Headphones ? 1 : 0;
            widthSlider.Value = ClampSlider(profile.Width);
            bassSlider.Value = ClampSlider(profile.Bass);
            trebleSlider.Value = ClampSlider(profile.Treble);
            surroundSlider.Value = ClampSlider(profile.Surround);
            compressionSlider.Value = ClampSlider(profile.Compression);
            volumeSlider.Value = ClampSlider(profile.Volume);
            ApplyProcessorSettings();
            scanButton.Enabled = true;
            statusLabel.Text = string.Format("Applied scanned Edge extension profile: {0}, width {1}%, tone {2}/{3}%.",
                profile.Mode == ListeningMode.Speakers ? "Speakers ARCC" : "Headphones AREI",
                widthSlider.Value, bassSlider.Value, trebleSlider.Value);
        }

        private static int ClampSlider(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private TabPage CreatePeqTab()
        {
            var tab = new TabPage("PEQ System")
            {
                BackColor = Color.FromArgb(245, 247, 250),
                AutoScroll = true
            };
            tab.Resize += PeqTab_Resize;

            var title = new Label
            {
                Text = "Windows PEQ Audio System",
                Font = new Font("Segoe UI Semibold", 22f),
                AutoSize = true,
                Location = new Point(28, 24)
            };
            var subtitle = new Label
            {
                Text = "PEQApp-derived parametric EQ, profiles, import/export, and desktop audio processing",
                ForeColor = Color.FromArgb(78, 86, 98),
                AutoSize = true,
                Location = new Point(32, 72)
            };

            peqEnabledCheckBox = new CheckBox
            {
                Text = "Enable PEQ processing",
                Checked = true,
                Location = new Point(32, 112),
                Size = new Size(190, 28)
            };
            peqEnabledCheckBox.CheckedChanged += PeqControl_Changed;

            peqPresetCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(238, 110),
                Size = new Size(190, 28)
            };
            foreach (var profile in PeqProfile.BuiltIns())
            {
                peqPresetCombo.Items.Add(profile.Name);
            }
            peqPresetCombo.SelectedIndex = 0;
            peqPresetCombo.SelectedIndexChanged += PeqPresetCombo_SelectedIndexChanged;

            var applyButton = new Button
            {
                Text = "Apply preset",
                Location = new Point(444, 108),
                Size = new Size(112, 32)
            };
            applyButton.Click += delegate { ApplySelectedPeqPreset(); };

            peqStatusLabel = new Label
            {
                Text = "PEQ armed. Use Start desktop audio on the Magic tab to process Windows audio.",
                AutoSize = true,
                Location = new Point(32, 152),
                ForeColor = Color.FromArgb(78, 86, 98)
            };

            peqGraphPanel = new FrequencyResponsePanel
            {
                Location = new Point(32, 178),
                Size = new Size(560, 88)
            };

            peqBandsGrid = new DataGridView
            {
                Location = new Point(32, 278),
                Size = new Size(560, 172),
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            peqBandsGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On" });
            var typeColumn = new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "Type" };
            typeColumn.Items.AddRange("PK", "LSC", "HSC", "LP", "HP", "NO", "BP", "AP");
            peqBandsGrid.Columns.Add(typeColumn);
            peqBandsGrid.Columns.Add("Freq", "Freq Hz");
            peqBandsGrid.Columns.Add("Gain", "Gain dB");
            peqBandsGrid.Columns.Add("Q", "Q");
            peqBandsGrid.CellValueChanged += PeqControl_Changed;
            peqBandsGrid.RowsRemoved += PeqControl_Changed;
            peqBandsGrid.UserAddedRow += PeqControl_Changed;

            peqEffectsPanel = new Panel
            {
                Location = new Point(620, 178),
                Size = new Size(290, 260),
                BackColor = Color.FromArgb(252, 253, 255)
            };
            peqEffectsPanel.Paint += delegate(object sender, PaintEventArgs args)
            {
                using (var pen = new Pen(Color.FromArgb(218, 224, 232)))
                {
                    args.Graphics.DrawRectangle(pen, 0, 0, peqEffectsPanel.Width - 1, peqEffectsPanel.Height - 1);
                }
            };
            peqPreampInput = AddNumber(peqEffectsPanel, "Preamp dB", 16, 16, -24, 24, 0, 1);
            peqBassBoostInput = AddNumber(peqEffectsPanel, "Bass Boost", 16, 52, 0, 12, 0, 1);
            peqLoudnessInput = AddNumber(peqEffectsPanel, "Loudness", 16, 88, 0, 12, 0, 1);
            peqStereoWidthInput = AddNumber(peqEffectsPanel, "Stereo Width", 16, 124, 0, 2, 1, 2);
            peqVirtualizerInput = AddNumber(peqEffectsPanel, "Virtualizer %", 16, 160, 0, 100, 0, 0);
            peqReverbInput = AddNumber(peqEffectsPanel, "Reverb", 16, 196, 0, 500, 0, 0);

            peqImportButton = new Button
            {
                Text = "Import APO",
                Location = new Point(32, 466),
                Size = new Size(112, 32)
            };
            peqImportButton.Click += delegate { ImportPeqConfig(); };
            peqExportButton = new Button
            {
                Text = "Export APO",
                Location = new Point(154, 466),
                Size = new Size(112, 32)
            };
            peqExportButton.Click += delegate { ExportPeqConfig(); };
            peqAddBandButton = new Button
            {
                Text = "Add band",
                Location = new Point(276, 466),
                Size = new Size(96, 32)
            };
            peqAddBandButton.Click += delegate
            {
                peqBandsGrid.Rows.Add(true, "PK", 1000, 0, 1.0);
                SyncPeqToProcessor();
            };

            peqConfigText = new TextBox
            {
                Location = new Point(32, 506),
                Size = new Size(560, 70),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f)
            };

            autoEqPanel = (Panel)CreateAutoEqPanel();

            tab.Controls.Add(title);
            tab.Controls.Add(subtitle);
            tab.Controls.Add(peqEnabledCheckBox);
            tab.Controls.Add(peqPresetCombo);
            tab.Controls.Add(applyButton);
            tab.Controls.Add(peqStatusLabel);
            tab.Controls.Add(peqGraphPanel);
            tab.Controls.Add(peqBandsGrid);
            tab.Controls.Add(peqEffectsPanel);
            tab.Controls.Add(peqImportButton);
            tab.Controls.Add(peqExportButton);
            tab.Controls.Add(peqAddBandButton);
            tab.Controls.Add(peqConfigText);
            tab.Controls.Add(autoEqPanel);

            ApplySelectedPeqPreset();
            LayoutPeqTab(tab);
            return tab;
        }

        private Control CreateAutoEqPanel()
        {
            var panel = new Panel
            {
                Location = new Point(620, 470),
                Size = new Size(290, 170),
                BackColor = Color.White
            };
            panel.Paint += delegate(object sender, PaintEventArgs args)
            {
                using (var pen = new Pen(Color.FromArgb(218, 224, 232)))
                {
                    args.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                }
            };

            autoEqSourceCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(10, 10),
                Size = new Size(112, 26)
            };
            autoEqSourceCombo.Items.Add("PEQ-AutoEQ");
            autoEqSourceCombo.Items.Add("AutoEq");
            autoEqSourceCombo.Items.Add("Local");
            autoEqSourceCombo.SelectedIndex = 0;
            autoEqSourceCombo.SelectedIndexChanged += delegate { FilterAutoEqList(); };

            autoEqSearchText = new TextBox
            {
                Location = new Point(130, 10),
                Size = new Size(150, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            autoEqSearchText.TextChanged += delegate { FilterAutoEqList(); };

            autoEqListView = new ListView
            {
                Location = new Point(10, 42),
                Size = new Size(270, 92),
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            autoEqListView.Columns.Add("AutoEQ", 142);
            autoEqListView.Columns.Add("Source", 72);
            autoEqListView.Columns.Add("Kind", 52);
            autoEqListView.DoubleClick += delegate { ApplySelectedAutoEq(); };

            var applyButton = new Button
            {
                Text = "Apply",
                Location = new Point(10, 140),
                Size = new Size(64, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            applyButton.Click += delegate { ApplySelectedAutoEq(); };
            var pasteButton = new Button
            {
                Text = "Paste",
                Location = new Point(82, 140),
                Size = new Size(64, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            pasteButton.Click += delegate { ImportPeqConfig(); };
            autoEqStatusLabel = new Label
            {
                Text = "AutoEQ list ready",
                Location = new Point(154, 144),
                Size = new Size(126, 18),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(78, 86, 98)
            };

            panel.Controls.Add(autoEqSourceCombo);
            panel.Controls.Add(autoEqSearchText);
            panel.Controls.Add(autoEqListView);
            panel.Controls.Add(applyButton);
            panel.Controls.Add(pasteButton);
            panel.Controls.Add(autoEqStatusLabel);

            autoEqProfiles = AutoEqProfile.BuiltIns();
            FilterAutoEqList();
            return panel;
        }

        private NumericUpDown AddNumber(Control parent, string label, int x, int y, decimal min, decimal max, decimal value, int decimalPlaces)
        {
            var caption = new Label { Text = label, Location = new Point(x, y + 4), Size = new Size(120, 24) };
            var input = new NumericUpDown
            {
                Location = new Point(x + 132, y),
                Size = new Size(120, 28),
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimalPlaces,
                Increment = decimalPlaces > 0 ? 0.1M : 1M,
                Value = value
            };
            input.ValueChanged += PeqControl_Changed;
            parent.Controls.Add(caption);
            parent.Controls.Add(input);
            return input;
        }

        private void PeqTab_Resize(object sender, EventArgs e)
        {
            var tab = sender as TabPage;
            if (tab != null)
            {
                LayoutPeqTab(tab);
            }
        }

        private void LayoutPeqTab(TabPage tab)
        {
            if (peqGraphPanel == null || peqBandsGrid == null || peqConfigText == null || peqEffectsPanel == null || autoEqPanel == null)
            {
                return;
            }

            int margin = 32;
            int gap = 28;
            int rightWidth = 300;
            int minLeftWidth = 500;
            int clientWidth = Math.Max(860, tab.ClientSize.Width);
            int clientHeight = Math.Max(600, tab.ClientSize.Height);
            int leftWidth = Math.Max(minLeftWidth, clientWidth - margin * 2 - gap - rightWidth);
            int rightX = margin + leftWidth + gap;
            int bottom = clientHeight - 28;
            int configHeight = 70;
            int configY = Math.Max(506, bottom - configHeight);
            int buttonY = configY - 40;
            int bandsY = 278;
            int bandsHeight = Math.Max(150, buttonY - bandsY - 14);
            int autoY = 450;
            int autoHeight = Math.Max(150, bottom - autoY);

            peqGraphPanel.SetBounds(margin, 178, leftWidth, 88);
            peqBandsGrid.SetBounds(margin, bandsY, leftWidth, bandsHeight);
            peqImportButton.SetBounds(margin, buttonY, 112, 32);
            peqExportButton.SetBounds(margin + 122, buttonY, 112, 32);
            peqAddBandButton.SetBounds(margin + 244, buttonY, 96, 32);
            peqConfigText.SetBounds(margin, configY, leftWidth, configHeight);
            peqEffectsPanel.SetBounds(rightX, 178, rightWidth, 260);
            autoEqPanel.SetBounds(rightX, autoY, rightWidth, autoHeight);

            if (autoEqListView != null)
            {
                autoEqListView.Columns[0].Width = Math.Max(136, autoEqListView.Width - 132);
                autoEqListView.Columns[1].Width = 74;
                autoEqListView.Columns[2].Width = 52;
            }
        }

        private void PeqPresetCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySelectedPeqPreset();
        }

        private void ApplySelectedPeqPreset()
        {
            if (peqPresetCombo == null || peqBandsGrid == null) return;
            monitorOutputCheckBox.Checked = true;
            var profile = PeqProfile.Find((string)peqPresetCombo.SelectedItem);
            peqPreampInput.Value = ClampDecimal((decimal)profile.PreampDb, peqPreampInput.Minimum, peqPreampInput.Maximum);
            peqBassBoostInput.Value = ClampDecimal((decimal)profile.BassBoostDb, peqBassBoostInput.Minimum, peqBassBoostInput.Maximum);
            peqLoudnessInput.Value = ClampDecimal((decimal)profile.LoudnessDb, peqLoudnessInput.Minimum, peqLoudnessInput.Maximum);
            peqStereoWidthInput.Value = ClampDecimal((decimal)profile.StereoWidth, peqStereoWidthInput.Minimum, peqStereoWidthInput.Maximum);
            peqVirtualizerInput.Value = ClampDecimal((decimal)profile.VirtualizerStrength, peqVirtualizerInput.Minimum, peqVirtualizerInput.Maximum);
            peqReverbInput.Value = ClampDecimal((decimal)profile.ReverbStrength, peqReverbInput.Minimum, peqReverbInput.Maximum);
            peqBandsGrid.Rows.Clear();
            foreach (var band in profile.Bands)
            {
                peqBandsGrid.Rows.Add(band.Enabled, band.Type, band.Frequency, band.GainDb, band.Q);
            }
            SyncPeqToProcessor();
        }

        private void PeqControl_Changed(object sender, EventArgs e)
        {
            SyncPeqToProcessor();
        }

        private void SyncPeqToProcessor()
        {
            if (peqEnabledCheckBox == null || peqBandsGrid == null) return;
            var settings = new PeqSettings
            {
                Enabled = peqEnabledCheckBox.Checked,
                PreampDb = (float)peqPreampInput.Value,
                BassBoostDb = (float)peqBassBoostInput.Value,
                LoudnessDb = (float)peqLoudnessInput.Value,
                StereoWidth = (float)peqStereoWidthInput.Value,
                VirtualizerStrength = (float)peqVirtualizerInput.Value,
                ReverbStrength = (float)peqReverbInput.Value
            };
            foreach (DataGridViewRow row in peqBandsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                settings.Bands.Add(new PeqBand
                {
                    Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? true),
                    Type = Convert.ToString(row.Cells["Type"].Value ?? "PK"),
                    Frequency = ToFloat(row.Cells["Freq"].Value, 1000),
                    GainDb = ToFloat(row.Cells["Gain"].Value, 0),
                    Q = Math.Max(0.01f, ToFloat(row.Cells["Q"].Value, 1))
                });
            }
            processor.PeqSettings = settings;
            if (peqGraphPanel != null)
            {
                peqGraphPanel.SetSettings(settings);
            }
            if (peqStatusLabel != null)
            {
                peqStatusLabel.Text = string.Format("PEQ {0}: {1} bands, preamp {2:0.0} dB.",
                    settings.Enabled ? "active" : "bypassed", settings.Bands.Count, settings.PreampDb);
            }
        }

        private void ImportPeqConfig()
        {
            var parsed = PeqConfigParser.Parse(peqConfigText.Text);
            peqPreampInput.Value = ClampDecimal((decimal)parsed.PreampDb, peqPreampInput.Minimum, peqPreampInput.Maximum);
            peqBandsGrid.Rows.Clear();
            foreach (var band in parsed.Bands)
            {
                peqBandsGrid.Rows.Add(band.Enabled, band.Type, band.Frequency, band.GainDb, band.Q);
            }
            SyncPeqToProcessor();
        }

        private void ExportPeqConfig()
        {
            SyncPeqToProcessor();
            peqConfigText.Text = PeqConfigParser.Serialize(processor.PeqSettings);
        }

        private void FilterAutoEqList()
        {
            if (autoEqListView == null) return;
            autoEqListView.Items.Clear();
            string source = autoEqSourceCombo == null || autoEqSourceCombo.SelectedItem == null ? "" : autoEqSourceCombo.SelectedItem.ToString();
            string needle = autoEqSearchText == null ? "" : autoEqSearchText.Text.Trim().ToLowerInvariant();
            int count = 0;
            foreach (var profile in autoEqProfiles)
            {
                if (source == "PEQ-AutoEQ" && profile.Source != "PEQ-AutoEQ") continue;
                if (source == "AutoEq" && profile.Source != "AutoEq") continue;
                string haystack = (profile.Name + " " + profile.Source + " " + profile.Kind + " " + profile.Measurement).ToLowerInvariant();
                if (needle.Length > 0 && !haystack.Contains(needle)) continue;
                var item = new ListViewItem(profile.Name);
                item.SubItems.Add(profile.Measurement);
                item.SubItems.Add(profile.Kind);
                item.Tag = profile;
                autoEqListView.Items.Add(item);
                count++;
                if (count >= 80) break;
            }
            if (autoEqStatusLabel != null)
            {
                autoEqStatusLabel.Text = count + " shown";
            }
        }

        private void ApplySelectedAutoEq()
        {
            if (autoEqListView == null || autoEqListView.SelectedItems.Count == 0) return;
            var profile = autoEqListView.SelectedItems[0].Tag as AutoEqProfile;
            if (profile == null) return;
            monitorOutputCheckBox.Checked = true;
            peqConfigText.Text = profile.ConfigText;
            ImportPeqConfig();
            peqStatusLabel.Text = "Applied AutoEQ: " + profile.Name + " - monitor audition on";
        }

        private static float ToFloat(object value, float fallback)
        {
            if (value == null) return fallback;
            float parsed;
            return float.TryParse(Convert.ToString(value), out parsed) ? parsed : fallback;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    internal sealed class MeterPanel : Panel
    {
        private float leftInput;
        private float rightInput;
        private float leftOutput;
        private float rightOutput;

        public MeterPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(25, 28, 35);
        }

        public void SetLevels(float li, float ri, float lo, float ro)
        {
            leftInput = Clamp01(li);
            rightInput = Clamp01(ri);
            leftOutput = Clamp01(lo);
            rightOutput = Clamp01(ro);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var border = new Pen(Color.FromArgb(58, 64, 76)))
            using (var text = new SolidBrush(Color.FromArgb(242, 245, 249)))
            using (var muted = new SolidBrush(Color.FromArgb(160, 169, 184)))
            {
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                e.Graphics.DrawString("Live Levels", new Font(Font.FontFamily, 15f, FontStyle.Bold), text, 18, 18);
                e.Graphics.DrawString("Input", Font, muted, 22, 68);
                e.Graphics.DrawString("Processed", Font, muted, 22, 142);
            }

            DrawBar(e.Graphics, 112, 66, leftInput, "L");
            DrawBar(e.Graphics, 112, 98, rightInput, "R");
            DrawBar(e.Graphics, 112, 140, leftOutput, "L");
            DrawBar(e.Graphics, 112, 172, rightOutput, "R");
        }

        private void DrawBar(Graphics g, int x, int y, float value, string label)
        {
            var rect = new Rectangle(x + 28, y, Width - x - 54, 18);
            using (var text = new SolidBrush(Color.FromArgb(242, 245, 249)))
            using (var bg = new SolidBrush(Color.FromArgb(44, 50, 62)))
            using (var fill = new SolidBrush(Color.FromArgb(255, 139, 31)))
            {
                g.DrawString(label, Font, text, x, y - 2);
                g.FillRectangle(bg, rect);
                g.FillRectangle(fill, rect.X, rect.Y, (int)(rect.Width * value), rect.Height);
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }

    internal sealed class FrequencyResponsePanel : Panel
    {
        private PeqSettings settings = new PeqSettings();

        public FrequencyResponsePanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(30, 36, 46);
        }

        public void SetSettings(PeqSettings newSettings)
        {
            settings = newSettings == null ? new PeqSettings() : newSettings.Clone();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var gridPen = new Pen(Color.FromArgb(58, 66, 78)))
            using (var zeroPen = new Pen(Color.FromArgb(104, 116, 132)))
            using (var curvePen = new Pen(Color.FromArgb(54, 211, 153), 2f))
            using (var text = new SolidBrush(Color.FromArgb(206, 214, 226)))
            {
                for (int i = 1; i < 5; i++)
                {
                    int y = i * Height / 5;
                    e.Graphics.DrawLine(gridPen, 0, y, Width, y);
                }
                int zeroY = DbToY(0);
                e.Graphics.DrawLine(zeroPen, 0, zeroY, Width, zeroY);
                e.Graphics.DrawString("20 Hz", Font, text, 8, Height - 20);
                e.Graphics.DrawString("20 kHz", Font, text, Width - 58, Height - 20);

                if (!settings.Enabled)
                {
                    e.Graphics.DrawString("PEQ bypassed", Font, text, 12, 10);
                    return;
                }

                var points = new PointF[Math.Max(2, Width - 16)];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1);
                    double hz = Math.Pow(10, Math.Log10(20) + t * (Math.Log10(20000) - Math.Log10(20)));
                    float db = settings.PreampDb;
                    foreach (var band in settings.Bands)
                    {
                        if (band.Enabled) db += BiquadMath.GainAt(band, 48000, hz);
                    }
                    points[i] = new PointF(8 + i, DbToY(db));
                }
                if (points.Length > 1)
                {
                    e.Graphics.DrawLines(curvePen, points);
                }
            }
        }

        private int DbToY(float db)
        {
            float clamped = Math.Max(-18, Math.Min(18, db));
            return (int)Math.Round((18 - clamped) / 36.0f * (Height - 18)) + 4;
        }
    }

    internal sealed class AudioProcessor
    {
        private float bassStateL;
        private float bassStateR;
        private float trebleStateL;
        private float trebleStateR;
        private readonly MagicAudioExtensionDsp magicDsp = new MagicAudioExtensionDsp();
        private readonly MagicAcousticSignatureDsp signatureDsp = new MagicAcousticSignatureDsp();
        private readonly PeqProcessor peqProcessor = new PeqProcessor();

        public float Volume { get; set; }
        public float Bass { get; set; }
        public float Treble { get; set; }
        public float Width { get; set; }
        public float Surround { get; set; }
        public float Compression { get; set; }
        public ListeningMode Mode
        {
            get { return magicDsp.Mode; }
            set { magicDsp.Mode = value; }
        }
        public PeqSettings PeqSettings
        {
            get { return peqProcessor.Settings; }
            set { peqProcessor.Settings = value; }
        }

        public AudioProcessResult Process(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            if (format.Channels < 1) return new AudioProcessResult();

            int bytesPerSample = format.BitsPerSample / 8;
            if (bytesPerSample <= 0) return new AudioProcessResult();

            int frameSize = bytesPerSample * format.Channels;
            if (frameSize <= 0) return new AudioProcessResult();

            magicDsp.Configure(format.SampleRate);
            signatureDsp.Configure(format.SampleRate);
            peqProcessor.Configure(format.SampleRate);

            double inL = 0;
            double inR = 0;
            double outL = 0;
            double outR = 0;
            int frames = bytesRecorded / frameSize;
            byte[] pcm16 = new byte[frames * 4];

            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * frameSize;
                float left = ReadSample(buffer, offset, format);
                float right = format.Channels > 1 ? ReadSample(buffer, offset + bytesPerSample, format) : left;

                inL += left * left;
                inR += right * right;

                ProcessStereo(ref left, ref right);

                outL += left * left;
                outR += right * right;

                WritePcm16(pcm16, frame * 4, left);
                WritePcm16(pcm16, frame * 4 + 2, right);
            }

            if (frames == 0) return new AudioProcessResult();

            return new AudioProcessResult
            {
                LeftInput = ToMeter(inL, frames),
                RightInput = ToMeter(inR, frames),
                LeftOutput = ToMeter(outL, frames),
                RightOutput = ToMeter(outR, frames),
                Pcm16Stereo = pcm16
            };
        }

        private void ProcessStereo(ref float left, ref float right)
        {
            magicDsp.Process(ref left, ref right);
            signatureDsp.Process(ref left, ref right, Surround);

            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f * Width;

            left = mid + side;
            right = mid - side;

            float surroundFeed = (left - right) * Surround * 0.18f;
            left += surroundFeed;
            right -= surroundFeed;

            bassStateL += (left - bassStateL) * 0.035f;
            bassStateR += (right - bassStateR) * 0.035f;
            trebleStateL += (left - trebleStateL) * 0.18f;
            trebleStateR += (right - trebleStateR) * 0.18f;

            left += bassStateL * Bass * 0.8f + (left - trebleStateL) * Treble * 0.45f;
            right += bassStateR * Bass * 0.8f + (right - trebleStateR) * Treble * 0.45f;

            if (Compression > 0.001f)
            {
                left = Compress(left, Compression);
                right = Compress(right, Compression);
            }

            left = SoftClip(left * Volume);
            right = SoftClip(right * Volume);
            peqProcessor.Process(ref left, ref right);
        }

        private static float Compress(float sample, float amount)
        {
            float abs = Math.Abs(sample);
            float threshold = 0.55f - amount * 0.25f;
            if (abs <= threshold) return sample;

            float over = abs - threshold;
            float ratio = 1.0f + amount * 7.0f;
            float compressed = threshold + over / ratio;
            return Math.Sign(sample) * compressed;
        }

        private static float SoftClip(float sample)
        {
            return (float)Math.Tanh(sample);
        }

        private static float ToMeter(double sum, int frames)
        {
            return (float)Math.Min(1.0, Math.Sqrt(sum / frames) * 2.6);
        }

        private static float ReadSample(byte[] buffer, int offset, WaveFormat format)
        {
            if (format.IsIeeeFloat && format.BitsPerSample == 32)
            {
                return BitConverter.ToSingle(buffer, offset);
            }

            if (format.BitsPerSample == 16)
            {
                return BitConverter.ToInt16(buffer, offset) / 32768f;
            }

            if (format.BitsPerSample == 24)
            {
                int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
                if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000);
                return value / 8388608f;
            }

            if (format.BitsPerSample == 32)
            {
                return BitConverter.ToInt32(buffer, offset) / 2147483648f;
            }

            return 0;
        }

        private static void WritePcm16(byte[] buffer, int offset, float sample)
        {
            sample = Math.Max(-1.0f, Math.Min(1.0f, sample));
            short value = (short)(sample * 32767.0f);
            buffer[offset] = (byte)(value & 0xff);
            buffer[offset + 1] = (byte)((value >> 8) & 0xff);
        }
    }

    internal sealed class MagicAudioExtensionDsp
    {
        private const int DefaultDelaySamples = 3;
        private const float DefaultGain = 1.375f;
        private const float LowBandCancel = 0.1f;
        private const float MidBandCancel = 0.56234133f;
        private const float HighBandCancel = 0.1f;
        private const float CompressorThreshold = 0.06309573f;
        private const float CompressorKnee = 0.17782794f;
        private const float CompressorRatio = 12.0f;

        private readonly DelayLine leftDelay = new DelayLine(DefaultDelaySamples + 1);
        private readonly DelayLine rightDelay = new DelayLine(DefaultDelaySamples + 1);
        private ThreeBandSplit leftBands = new ThreeBandSplit(48000.0f);
        private ThreeBandSplit rightBands = new ThreeBandSplit(48000.0f);
        private readonly HeadphoneBinauralDsp headphoneDsp = new HeadphoneBinauralDsp();
        private int sampleRate = 48000;

        public ListeningMode Mode { get; set; }

        public void Configure(int newSampleRate)
        {
            if (newSampleRate <= 0 || newSampleRate == sampleRate)
            {
                return;
            }

            sampleRate = newSampleRate;
            leftBands = new ThreeBandSplit(sampleRate);
            rightBands = new ThreeBandSplit(sampleRate);
            headphoneDsp.Configure(sampleRate);
        }

        public void Process(ref float left, ref float right)
        {
            if (Mode == ListeningMode.Headphones)
            {
                headphoneDsp.Process(ref left, ref right);
            }
            else
            {
                ProcessSpeakerArcc(ref left, ref right);
            }
        }

        private void ProcessSpeakerArcc(ref float left, ref float right)
        {
            float delayedLeft = leftDelay.Process(left, DefaultDelaySamples);
            float delayedRight = rightDelay.Process(right, DefaultDelaySamples);

            BandSample leftBand = leftBands.Process(delayedLeft);
            BandSample rightBand = rightBands.Process(delayedRight);

            // Magic.Audio 0.0.4 defaults to the ARCC speaker plugin. The extension
            // generates FIR filters; this user-mode port mirrors the observed
            // three-band cancellation profile and delay in sample-domain form.
            float cancelLeft = rightBand.Low * LowBandCancel + rightBand.Mid * MidBandCancel + rightBand.High * HighBandCancel;
            float cancelRight = leftBand.Low * LowBandCancel + leftBand.Mid * MidBandCancel + leftBand.High * HighBandCancel;

            left = (left - cancelLeft * 1.35f) * DefaultGain;
            right = (right - cancelRight * 1.35f) * DefaultGain;

            left = Compress(left);
            right = Compress(right);
        }

        private static float Compress(float value)
        {
            float sign = Math.Sign(value);
            float abs = Math.Abs(value);
            if (abs <= CompressorThreshold)
            {
                return value;
            }

            float kneeEnd = CompressorThreshold + CompressorKnee;
            if (abs < kneeEnd)
            {
                float t = (abs - CompressorThreshold) / CompressorKnee;
                float ratioValue = CompressorThreshold + (abs - CompressorThreshold) / CompressorRatio;
                return sign * (abs + (ratioValue - abs) * t * t);
            }

            return sign * (CompressorThreshold + (abs - CompressorThreshold) / CompressorRatio);
        }
    }

    internal sealed class MagicAcousticSignatureDsp
    {
        private readonly DelayLine leftShort = new DelayLine(4096);
        private readonly DelayLine leftLong = new DelayLine(4096);
        private readonly DelayLine rightShort = new DelayLine(4096);
        private readonly DelayLine rightLong = new DelayLine(4096);
        private int shortDelay = 154;
        private int midDelay = 355;
        private int longDelay = 557;

        public void Configure(int sampleRate)
        {
            if (sampleRate <= 0)
            {
                return;
            }

            shortDelay = ClampDelay((int)(sampleRate * 0.0032f));
            midDelay = ClampDelay((int)(sampleRate * 0.0074f));
            longDelay = ClampDelay((int)(sampleRate * 0.0116f));
        }

        public void Process(ref float left, ref float right, float amount)
        {
            if (amount <= 0.001f)
            {
                return;
            }

            float lShort = leftShort.Process(left, shortDelay);
            float lLong = leftLong.Process(left, longDelay);
            float rShort = rightShort.Process(right, shortDelay);
            float rLong = rightLong.Process(right, midDelay);
            float wet = Math.Min(1.0f, amount) * 0.85f;

            float outL = left + wet * (lShort * 0.055f + rShort * 0.09f - rLong * 0.035f + lLong * 0.025f);
            float outR = right + wet * (rShort * 0.055f + lShort * 0.09f - lLong * 0.035f + rLong * 0.025f);

            left = (float)Math.Tanh(outL);
            right = (float)Math.Tanh(outR);
        }

        private static int ClampDelay(int delay)
        {
            if (delay < 1) return 1;
            if (delay > 4095) return 4095;
            return delay;
        }
    }

    internal sealed class HeadphoneBinauralDsp
    {
        private readonly DelayLine leftEarly = new DelayLine(17);
        private readonly DelayLine rightEarly = new DelayLine(17);
        private OnePoleLowPass leftLow = new OnePoleLowPass(200.0f, 48000.0f);
        private OnePoleLowPass rightLow = new OnePoleLowPass(200.0f, 48000.0f);
        private int sampleRate = 48000;

        public void Configure(int newSampleRate)
        {
            if (newSampleRate <= 0 || newSampleRate == sampleRate)
            {
                return;
            }

            sampleRate = newSampleRate;
            leftLow = new OnePoleLowPass(200.0f, sampleRate);
            rightLow = new OnePoleLowPass(200.0f, sampleRate);
        }

        public void Process(ref float left, ref float right)
        {
            float lowLeft = leftLow.Process(left);
            float lowRight = rightLow.Process(right);
            float highLeft = left - lowLeft;
            float highRight = right - lowRight;

            float delayedLeft = leftEarly.Process(highLeft, 13);
            float delayedRight = rightEarly.Process(highRight, 13);

            // AREI in the extension routes virtual sources through HRTF
            // convolution with a 200 Hz crossover. This is the app-native
            // binaural monitor path that follows that graph shape.
            float binauralLeft = lowLeft + highLeft * 0.78f + delayedRight * 0.34f;
            float binauralRight = lowRight + highRight * 0.78f + delayedLeft * 0.34f;

            left = (float)Math.Tanh(binauralLeft * 1.28f);
            right = (float)Math.Tanh(binauralRight * 1.28f);
        }
    }

    internal struct BandSample
    {
        public float Low;
        public float Mid;
        public float High;
    }

    internal sealed class ThreeBandSplit
    {
        private readonly OnePoleLowPass low;
        private readonly OnePoleLowPass highCut;

        public ThreeBandSplit(float sampleRate)
        {
            low = new OnePoleLowPass(250.0f, sampleRate);
            highCut = new OnePoleLowPass(8000.0f, sampleRate);
        }

        public BandSample Process(float sample)
        {
            float lowValue = low.Process(sample);
            float belowHigh = highCut.Process(sample);

            return new BandSample
            {
                Low = lowValue,
                Mid = belowHigh - lowValue,
                High = sample - belowHigh
            };
        }
    }

    internal sealed class OnePoleLowPass
    {
        private readonly float alpha;
        private float state;

        public OnePoleLowPass(float frequency, float sampleRate)
        {
            float dt = 1.0f / sampleRate;
            float rc = 1.0f / (2.0f * (float)Math.PI * frequency);
            alpha = dt / (rc + dt);
        }

        public float Process(float sample)
        {
            state += alpha * (sample - state);
            return state;
        }
    }

    internal sealed class DelayLine
    {
        private readonly float[] buffer;
        private int index;

        public DelayLine(int length)
        {
            buffer = new float[Math.Max(1, length)];
        }

        public float Process(float sample, int delaySamples)
        {
            int read = index - delaySamples;
            while (read < 0)
            {
                read += buffer.Length;
            }

            float delayed = buffer[read % buffer.Length];
            buffer[index] = sample;
            index = (index + 1) % buffer.Length;
            return delayed;
        }
    }

    internal struct ScannedProfile
    {
        public ListeningMode Mode;
        public int Volume;
        public int Bass;
        public int Treble;
        public int Width;
        public int Surround;
        public int Compression;
    }

    internal sealed class PeqSettings
    {
        public bool Enabled = true;
        public float PreampDb;
        public float BassBoostDb;
        public float LoudnessDb;
        public float StereoWidth = 1.0f;
        public float VirtualizerStrength;
        public float ReverbStrength;
        public readonly System.Collections.Generic.List<PeqBand> Bands = new System.Collections.Generic.List<PeqBand>();

        public PeqSettings Clone()
        {
            var copy = new PeqSettings
            {
                Enabled = Enabled,
                PreampDb = PreampDb,
                BassBoostDb = BassBoostDb,
                LoudnessDb = LoudnessDb,
                StereoWidth = StereoWidth,
                VirtualizerStrength = VirtualizerStrength,
                ReverbStrength = ReverbStrength
            };
            foreach (var band in Bands)
            {
                copy.Bands.Add(band);
            }
            return copy;
        }
    }

    internal struct PeqBand
    {
        public bool Enabled;
        public string Type;
        public float Frequency;
        public float GainDb;
        public float Q;
    }

    internal sealed class PeqProfile
    {
        public string Name;
        public float PreampDb;
        public float BassBoostDb;
        public float LoudnessDb;
        public float StereoWidth;
        public float VirtualizerStrength;
        public float ReverbStrength;
        public PeqBand[] Bands;

        public static PeqProfile Find(string name)
        {
            foreach (var profile in BuiltIns())
            {
                if (profile.Name == name) return profile;
            }
            return BuiltIns()[0];
        }

        public static PeqProfile[] BuiltIns()
        {
            return new[]
            {
                Profile("Flat", 0, 0, 0, 1.0f, 0, 0),
                Profile("Bass Boost", -3, 6, 2, 1.0f, 0, 0,
                    Band("LSC", 80, 6, 0.71f), Band("PK", 200, 3, 1.0f)),
                Profile("Vocal", -2, 0, 3, 1.0f, 0, 0,
                    Band("PK", 250, -2, 1.0f), Band("PK", 2500, 4, 1.0f), Band("HSC", 8000, 2, 0.71f)),
                Profile("Rock", -3, 3, 2, 1.2f, 20, 150,
                    Band("LSC", 60, 4, 0.7f), Band("PK", 250, -2, 1.0f), Band("PK", 3500, 3, 1.0f), Band("HSC", 10000, 3, 0.71f)),
                Profile("Classical", -1, 0, 1, 1.3f, 40, 300,
                    Band("PK", 100, 2, 0.8f), Band("PK", 1000, -1, 1.0f), Band("HSC", 8000, 3, 0.71f)),
                Profile("Electronic", -4, 5, 3, 1.4f, 30, 200,
                    Band("LSC", 60, 5, 0.7f), Band("PK", 200, 2, 1.0f), Band("PK", 5000, 2, 1.0f), Band("HSC", 12000, 4, 0.71f)),
                Profile("Atmos", -4, 5, 2, 1.5f, 80, 300,
                    Band("LSC", 50, 3.5f, 0.7f), Band("PK", 200, -1, 1.0f), Band("PK", 3000, 2.5f, 0.8f), Band("PK", 6000, 3, 1.0f), Band("HSC", 12000, 4.5f, 0.7f)),
                Profile("Cinema", -5, 6, 3, 1.8f, 90, 500,
                    Band("LSC", 40, 5, 0.7f), Band("PK", 150, -1, 1.2f), Band("PK", 2000, 2, 1.0f), Band("PK", 6000, 3.5f, 0.9f), Band("HSC", 14000, 4, 0.7f)),
                Profile("Sony 360 Reality", -3, 4, 2, 2.0f, 95, 400,
                    Band("LSC", 45, 3, 0.7f), Band("PK", 200, 1, 1.0f), Band("PK", 2500, 2.5f, 0.8f), Band("PK", 8000, 2, 1.0f), Band("HSC", 16000, 2.5f, 0.7f)),
                Profile("HD Studio", -1, 1, 1, 1.0f, 20, 0,
                    Band("PK", 100, 1, 0.8f), Band("PK", 1000, 1, 1.0f), Band("PK", 4000, 1, 1.0f), Band("HSC", 10000, 1.5f, 0.7f))
            };
        }

        private static PeqProfile Profile(string name, float preamp, float bass, float loudness, float width, float virtualizer, float reverb, params PeqBand[] bands)
        {
            return new PeqProfile
            {
                Name = name,
                PreampDb = preamp,
                BassBoostDb = bass,
                LoudnessDb = loudness,
                StereoWidth = width,
                VirtualizerStrength = virtualizer,
                ReverbStrength = reverb,
                Bands = bands
            };
        }

        private static PeqBand Band(string type, float freq, float gain, float q)
        {
            return new PeqBand { Enabled = true, Type = type, Frequency = freq, GainDb = gain, Q = q };
        }
    }

    internal static class PeqConfigParser
    {
        public static PeqSettings Parse(string text)
        {
            var settings = new PeqSettings();
            if (text == null) return settings;
            string trimmed = text.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return ParsePeqJson(trimmed);
            }
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("Preamp:", StringComparison.OrdinalIgnoreCase))
                {
                    settings.PreampDb = FirstFloat(line);
                    continue;
                }
                if (!line.StartsWith("Filter", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split(new[] { ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 7) continue;
                var band = new PeqBand { Enabled = true, Type = "PK", Frequency = 1000, GainDb = 0, Q = 0.707f };
                for (int i = 0; i < parts.Length; i++)
                {
                    var token = parts[i].ToUpperInvariant();
                    if (token == "ON") band.Enabled = true;
                    else if (token == "OFF") band.Enabled = false;
                    else if (IsType(token)) band.Type = token;
                    else if (token == "FC" && i + 1 < parts.Length) band.Frequency = ParseFloat(parts[++i], band.Frequency);
                    else if (token == "GAIN" && i + 1 < parts.Length) band.GainDb = ParseFloat(parts[++i], band.GainDb);
                    else if (token == "Q" && i + 1 < parts.Length) band.Q = ParseFloat(parts[++i], band.Q);
                }
                settings.Bands.Add(band);
            }
            return settings;
        }

        private static PeqSettings ParsePeqJson(string text)
        {
            var settings = new PeqSettings();
            settings.PreampDb = ExtractJsonNumber(text, "pre_amp", ExtractJsonNumber(text, "preamp", 0));
            var matches = System.Text.RegularExpressions.Regex.Matches(text,
                "\\{[^{}]*?(?:\"freq\"|\"frequency\")[^{}]*?\\}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string obj = match.Value;
                settings.Bands.Add(new PeqBand
                {
                    Enabled = ExtractJsonNumber(obj, "on", 1) != 0,
                    Type = PeqFilterType(ExtractJsonNumber(obj, "filter", 1)),
                    Frequency = ExtractJsonNumber(obj, "freq", ExtractJsonNumber(obj, "frequency", 1000)),
                    GainDb = ExtractJsonNumber(obj, "gain", 0),
                    Q = Math.Max(0.01f, ExtractJsonNumber(obj, "q", 0.707f))
                });
            }
            return settings;
        }

        private static string PeqFilterType(float filter)
        {
            if (Math.Abs(filter) < 0.01f) return "LSC";
            if (Math.Abs(filter - 2.0f) < 0.01f) return "HSC";
            return "PK";
        }

        private static float ExtractJsonNumber(string text, string key, float fallback)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text,
                "\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? ParseFloat(match.Groups[1].Value, fallback) : fallback;
        }

        public static string Serialize(PeqSettings settings)
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("# Windows PEQ App export");
            lines.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Preamp: {0:0.0} dB", settings.PreampDb));
            for (int i = 0; i < settings.Bands.Count; i++)
            {
                var band = settings.Bands[i];
                lines.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Filter {0}: {1} {2} Fc {3:0} Hz Gain {4:0.0} dB Q {5:0.###}",
                    i + 1, band.Enabled ? "ON" : "OFF", band.Type, band.Frequency, band.GainDb, band.Q));
            }
            return lines.ToString();
        }

        private static bool IsType(string token)
        {
            return token == "PK" || token == "LSC" || token == "HSC" || token == "LP" || token == "HP" || token == "NO" || token == "BP" || token == "AP";
        }

        private static float FirstFloat(string text)
        {
            foreach (var part in text.Split(new[] { ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                float parsed;
                if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)) return parsed;
            }
            return 0;
        }

        private static float ParseFloat(string text, float fallback)
        {
            float parsed;
            return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
    }

    internal sealed class AutoEqProfile
    {
        public string Name;
        public string Source;
        public string Measurement;
        public string Kind;
        public string ConfigText;

        public static AutoEqProfile[] BuiltIns()
        {
            return new[]
            {
                Peq("7Hz Salnotes Zero", "IEM", -6.2f, Band("PK", 20, 3.4f, 0.7f), Band("PK", 180, -2.1f, 1.1f), Band("PK", 3000, 4.2f, 1.8f), Band("PK", 8000, -3.0f, 2.2f)),
                Peq("Apple AirPods Pro 2", "TWS", -5.0f, Band("LSC", 80, 2.0f, 0.7f), Band("PK", 950, -1.4f, 1.0f), Band("PK", 2700, 2.2f, 1.4f), Band("HSC", 9000, -1.8f, 0.7f)),
                Peq("Audio-Technica ATH-M50x", "Headphone", -5.6f, Band("LSC", 55, 2.8f, 0.7f), Band("PK", 250, -2.5f, 1.1f), Band("PK", 4200, 3.0f, 1.6f), Band("PK", 9000, -4.0f, 2.5f)),
                Peq("Beyerdynamic DT 770 Pro 80 Ohm", "Headphone", -6.0f, Band("LSC", 60, 3.0f, 0.7f), Band("PK", 220, -1.8f, 1.0f), Band("PK", 6000, -5.2f, 2.0f), Band("HSC", 11000, -2.0f, 0.7f)),
                Peq("Beyerdynamic DT 990 Pro", "Headphone", -7.0f, Band("LSC", 70, 3.2f, 0.7f), Band("PK", 300, -2.0f, 1.0f), Band("PK", 6200, -6.5f, 2.1f), Band("PK", 10000, -3.0f, 2.0f)),
                Peq("Hifiman Sundara", "Headphone", -4.6f, Band("LSC", 75, 3.6f, 0.7f), Band("PK", 1900, 1.5f, 1.2f), Band("PK", 5800, -2.4f, 2.0f), Band("HSC", 12000, -1.2f, 0.7f)),
                Peq("Moondrop Aria", "IEM", -4.8f, Band("PK", 120, -1.6f, 1.0f), Band("PK", 2900, 2.6f, 1.6f), Band("PK", 7600, -3.5f, 2.0f), Band("HSC", 12000, 1.0f, 0.7f)),
                Peq("Sennheiser HD 600", "Headphone", -5.0f, Band("LSC", 70, 4.5f, 0.7f), Band("PK", 170, -1.4f, 1.1f), Band("PK", 3400, 2.5f, 1.4f), Band("HSC", 10000, -1.0f, 0.7f)),
                Peq("Sennheiser HD 650", "Headphone", -4.8f, Band("LSC", 75, 4.0f, 0.7f), Band("PK", 220, -1.0f, 1.0f), Band("PK", 3100, 2.0f, 1.3f), Band("HSC", 10000, 0.8f, 0.7f)),
                Peq("Sony WH-1000XM4", "Headphone", -6.2f, Band("LSC", 90, -2.0f, 0.7f), Band("PK", 450, -2.5f, 1.0f), Band("PK", 2500, 3.8f, 1.4f), Band("HSC", 9000, 2.0f, 0.7f)),
                AutoEq("AKG K371", "oratory1990", "over-ear", -5.8f, Band("LSC", 60, 2.2f, 0.7f), Band("PK", 180, -1.8f, 1.0f), Band("PK", 3100, 2.8f, 1.5f), Band("PK", 7200, -2.2f, 2.0f)),
                AutoEq("Anker Soundcore Life Q30", "Rtings", "over-ear", -7.0f, Band("LSC", 95, -3.8f, 0.7f), Band("PK", 420, -3.0f, 1.2f), Band("PK", 2800, 4.0f, 1.5f), Band("HSC", 9500, 2.2f, 0.7f)),
                AutoEq("Etymotic ER2XR", "crinacle", "IEM", -4.0f, Band("PK", 120, -1.8f, 1.0f), Band("PK", 2500, 1.8f, 1.3f), Band("PK", 7800, -2.8f, 2.0f), Band("HSC", 12000, 1.5f, 0.7f)),
                AutoEq("Focal Clear", "oratory1990", "over-ear", -4.5f, Band("LSC", 75, 2.5f, 0.7f), Band("PK", 1600, -1.2f, 1.2f), Band("PK", 5200, -2.2f, 2.0f), Band("HSC", 11000, 1.0f, 0.7f)),
                AutoEq("Philips SHP9500", "Innerfidelity", "over-ear", -5.4f, Band("LSC", 85, 3.5f, 0.7f), Band("PK", 210, -2.0f, 1.0f), Band("PK", 3600, 2.5f, 1.6f), Band("PK", 9000, -2.6f, 2.0f))
            };
        }

        private static AutoEqProfile Peq(string name, string kind, float preamp, params PeqBand[] bands)
        {
            return Create(name, "PEQ-AutoEQ", "Updated 2025", kind, preamp, bands);
        }

        private static AutoEqProfile AutoEq(string name, string measurement, string kind, float preamp, params PeqBand[] bands)
        {
            return Create(name, "AutoEq", measurement, kind, preamp, bands);
        }

        private static AutoEqProfile Create(string name, string source, string measurement, string kind, float preamp, params PeqBand[] bands)
        {
            var settings = new PeqSettings { PreampDb = preamp };
            foreach (var band in bands) settings.Bands.Add(band);
            return new AutoEqProfile
            {
                Name = name,
                Source = source,
                Measurement = measurement,
                Kind = kind,
                ConfigText = PeqConfigParser.Serialize(settings)
            };
        }

        private static PeqBand Band(string type, float freq, float gain, float q)
        {
            return new PeqBand { Enabled = true, Type = type, Frequency = freq, GainDb = gain, Q = q };
        }
    }

    internal sealed class PeqProcessor
    {
        private PeqSettings settings = new PeqSettings();
        private BiquadFilter[] filters = new BiquadFilter[0];
        private int sampleRate = 48000;
        private readonly MagicAcousticSignatureDsp virtualizer = new MagicAcousticSignatureDsp();
        private OnePoleLowPass bassL = new OnePoleLowPass(100, 48000);
        private OnePoleLowPass bassR = new OnePoleLowPass(100, 48000);

        public PeqSettings Settings
        {
            get { return settings; }
            set
            {
                settings = value == null ? new PeqSettings() : value.Clone();
                RebuildFilters();
            }
        }

        public void Configure(int newSampleRate)
        {
            if (newSampleRate <= 0 || newSampleRate == sampleRate) return;
            sampleRate = newSampleRate;
            virtualizer.Configure(sampleRate);
            bassL = new OnePoleLowPass(100, sampleRate);
            bassR = new OnePoleLowPass(100, sampleRate);
            RebuildFilters();
        }

        public void Process(ref float left, ref float right)
        {
            if (!settings.Enabled) return;
            float gain = DbToGain(settings.PreampDb);
            left *= gain;
            right *= gain;

            for (int i = 0; i < filters.Length; i += 2)
            {
                left = filters[i].Process(left);
                right = filters[i + 1].Process(right);
            }

            if (settings.BassBoostDb > 0)
            {
                float bassGain = (DbToGain(settings.BassBoostDb) - 1.0f) * 1.35f;
                left += bassL.Process(left) * bassGain;
                right += bassR.Process(right) * bassGain;
            }

            if (Math.Abs(settings.StereoWidth - 1.0f) > 0.001f)
            {
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f * settings.StereoWidth;
                left = mid + side;
                right = mid - side;
            }

            if (settings.VirtualizerStrength > 0)
            {
                virtualizer.Process(ref left, ref right, Math.Min(1.0f, settings.VirtualizerStrength / 75.0f));
            }

            if (settings.ReverbStrength > 0)
            {
                float amount = Math.Min(1.0f, settings.ReverbStrength / 500.0f) * 0.16f;
                float cross = (left + right) * amount;
                left += cross;
                right += cross;
            }

            if (settings.LoudnessDb > 0)
            {
                float loud = DbToGain(settings.LoudnessDb * 1.2f);
                left = (float)Math.Tanh(left * loud);
                right = (float)Math.Tanh(right * loud);
            }
        }

        private void RebuildFilters()
        {
            var list = new System.Collections.Generic.List<BiquadFilter>();
            foreach (var band in settings.Bands)
            {
                if (!band.Enabled) continue;
                list.Add(BiquadFilter.Create(band, sampleRate));
                list.Add(BiquadFilter.Create(band, sampleRate));
            }
            filters = list.ToArray();
        }

        private static float DbToGain(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }
    }

    internal sealed class BiquadFilter
    {
        private float b0;
        private float b1;
        private float b2;
        private float a1;
        private float a2;
        private float z1;
        private float z2;

        public static BiquadFilter Create(PeqBand band, int sampleRate)
        {
            var filter = new BiquadFilter();
            filter.SetCoefficients(band, sampleRate);
            return filter;
        }

        public float Process(float input)
        {
            float output = b0 * input + z1;
            z1 = b1 * input - a1 * output + z2;
            z2 = b2 * input - a2 * output;
            return output;
        }

        private void SetCoefficients(PeqBand band, int sampleRate)
        {
            float frequency = Math.Max(10, Math.Min(sampleRate * 0.45f, band.Frequency));
            float q = Math.Max(0.01f, band.Q);
            double a = Math.Pow(10.0, band.GainDb / 40.0);
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double cos = Math.Cos(w0);
            double sin = Math.Sin(w0);
            double alpha = sin / (2.0 * q);
            double rawB0 = 1, rawB1 = 0, rawB2 = 0, rawA0 = 1, rawA1 = 0, rawA2 = 0;
            string type = (band.Type ?? "PK").ToUpperInvariant();

            if (type == "PK")
            {
                rawB0 = 1 + alpha * a;
                rawB1 = -2 * cos;
                rawB2 = 1 - alpha * a;
                rawA0 = 1 + alpha / a;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha / a;
            }
            else if (type == "LSC" || type == "HSC")
            {
                double sqrtA = Math.Sqrt(a);
                if (type == "LSC")
                {
                    rawB0 = a * (a + 1 - (a - 1) * cos + 2 * sqrtA * alpha);
                    rawB1 = 2 * a * (a - 1 - (a + 1) * cos);
                    rawB2 = a * (a + 1 - (a - 1) * cos - 2 * sqrtA * alpha);
                    rawA0 = a + 1 + (a - 1) * cos + 2 * sqrtA * alpha;
                    rawA1 = -2 * (a - 1 + (a + 1) * cos);
                    rawA2 = a + 1 + (a - 1) * cos - 2 * sqrtA * alpha;
                }
                else
                {
                    rawB0 = a * (a + 1 + (a - 1) * cos + 2 * sqrtA * alpha);
                    rawB1 = -2 * a * (a - 1 + (a + 1) * cos);
                    rawB2 = a * (a + 1 + (a - 1) * cos - 2 * sqrtA * alpha);
                    rawA0 = a + 1 - (a - 1) * cos + 2 * sqrtA * alpha;
                    rawA1 = 2 * (a - 1 - (a + 1) * cos);
                    rawA2 = a + 1 - (a - 1) * cos - 2 * sqrtA * alpha;
                }
            }
            else if (type == "LP")
            {
                rawB0 = (1 - cos) / 2;
                rawB1 = 1 - cos;
                rawB2 = (1 - cos) / 2;
                rawA0 = 1 + alpha;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha;
            }
            else if (type == "HP")
            {
                rawB0 = (1 + cos) / 2;
                rawB1 = -(1 + cos);
                rawB2 = (1 + cos) / 2;
                rawA0 = 1 + alpha;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha;
            }
            else if (type == "NO")
            {
                rawB0 = 1;
                rawB1 = -2 * cos;
                rawB2 = 1;
                rawA0 = 1 + alpha;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha;
            }
            else if (type == "BP")
            {
                rawB0 = alpha;
                rawB1 = 0;
                rawB2 = -alpha;
                rawA0 = 1 + alpha;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha;
            }
            else if (type == "AP")
            {
                rawB0 = 1 - alpha;
                rawB1 = -2 * cos;
                rawB2 = 1 + alpha;
                rawA0 = 1 + alpha;
                rawA1 = -2 * cos;
                rawA2 = 1 - alpha;
            }

            b0 = (float)(rawB0 / rawA0);
            b1 = (float)(rawB1 / rawA0);
            b2 = (float)(rawB2 / rawA0);
            a1 = (float)(rawA1 / rawA0);
            a2 = (float)(rawA2 / rawA0);
        }
    }

    internal static class BiquadMath
    {
        public static float GainAt(PeqBand band, int sampleRate, double hz)
        {
            double[] c = Coefficients(band, sampleRate);
            double w = 2.0 * Math.PI * hz / sampleRate;
            double cosW = Math.Cos(w);
            double cos2W = Math.Cos(2 * w);
            double sinW = Math.Sin(w);
            double sin2W = Math.Sin(2 * w);
            double numRe = c[0] + c[1] * cosW + c[2] * cos2W;
            double numIm = -(c[1] * sinW + c[2] * sin2W);
            double denRe = 1 + c[3] * cosW + c[4] * cos2W;
            double denIm = -(c[3] * sinW + c[4] * sin2W);
            double den = denRe * denRe + denIm * denIm;
            if (den <= 0) return 0;
            double mag = (numRe * numRe + numIm * numIm) / den;
            return (float)(10.0 * Math.Log10(Math.Max(0.000000001, mag)));
        }

        private static double[] Coefficients(PeqBand band, int sampleRate)
        {
            float frequency = Math.Max(10, Math.Min(sampleRate * 0.45f, band.Frequency));
            float q = Math.Max(0.01f, band.Q);
            double a = Math.Pow(10.0, band.GainDb / 40.0);
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double cos = Math.Cos(w0);
            double sin = Math.Sin(w0);
            double alpha = sin / (2.0 * q);
            double b0 = 1, b1 = 0, b2 = 0, a0 = 1, a1 = 0, a2 = 0;
            string type = (band.Type ?? "PK").ToUpperInvariant();
            if (type == "PK")
            {
                b0 = 1 + alpha * a; b1 = -2 * cos; b2 = 1 - alpha * a;
                a0 = 1 + alpha / a; a1 = -2 * cos; a2 = 1 - alpha / a;
            }
            else if (type == "LSC" || type == "HSC")
            {
                double sqrtA = Math.Sqrt(a);
                if (type == "LSC")
                {
                    b0 = a * (a + 1 - (a - 1) * cos + 2 * sqrtA * alpha);
                    b1 = 2 * a * (a - 1 - (a + 1) * cos);
                    b2 = a * (a + 1 - (a - 1) * cos - 2 * sqrtA * alpha);
                    a0 = a + 1 + (a - 1) * cos + 2 * sqrtA * alpha;
                    a1 = -2 * (a - 1 + (a + 1) * cos);
                    a2 = a + 1 + (a - 1) * cos - 2 * sqrtA * alpha;
                }
                else
                {
                    b0 = a * (a + 1 + (a - 1) * cos + 2 * sqrtA * alpha);
                    b1 = -2 * a * (a - 1 + (a + 1) * cos);
                    b2 = a * (a + 1 + (a - 1) * cos - 2 * sqrtA * alpha);
                    a0 = a + 1 - (a - 1) * cos + 2 * sqrtA * alpha;
                    a1 = 2 * (a - 1 - (a + 1) * cos);
                    a2 = a + 1 - (a - 1) * cos - 2 * sqrtA * alpha;
                }
            }
            else if (type == "LP")
            {
                b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
            }
            else if (type == "HP")
            {
                b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
            }
            else if (type == "NO")
            {
                b0 = 1; b1 = -2 * cos; b2 = 1; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
            }
            else if (type == "BP")
            {
                b0 = alpha; b1 = 0; b2 = -alpha; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
            }
            else if (type == "AP")
            {
                b0 = 1 - alpha; b1 = -2 * cos; b2 = 1 + alpha; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
            }
            return new[] { b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0 };
        }
    }

    internal sealed class LiveExtensionScanner
    {
        private readonly OnePoleLowPass low = new OnePoleLowPass(250.0f, 48000.0f);
        private readonly OnePoleLowPass highCut = new OnePoleLowPass(8000.0f, 48000.0f);
        private double sumL;
        private double sumR;
        private double sumMid;
        private double sumSide;
        private double sumLow;
        private double sumHigh;
        private double sumCross;
        private int frames;

        public void Reset()
        {
            sumL = 0;
            sumR = 0;
            sumMid = 0;
            sumSide = 0;
            sumLow = 0;
            sumHigh = 0;
            sumCross = 0;
            frames = 0;
        }

        public void Add(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            if (format.Channels < 1) return;

            int bytesPerSample = format.BitsPerSample / 8;
            int frameSize = bytesPerSample * format.Channels;
            if (bytesPerSample <= 0 || frameSize <= 0) return;

            int frameCount = bytesRecorded / frameSize;
            for (int frame = 0; frame < frameCount; frame++)
            {
                int offset = frame * frameSize;
                float left = ReadSample(buffer, offset, format);
                float right = format.Channels > 1 ? ReadSample(buffer, offset + bytesPerSample, format) : left;
                float mono = (left + right) * 0.5f;
                float lowValue = low.Process(mono);
                float belowHigh = highCut.Process(mono);
                float highValue = mono - belowHigh;
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;

                sumL += left * left;
                sumR += right * right;
                sumMid += mid * mid;
                sumSide += side * side;
                sumLow += lowValue * lowValue;
                sumHigh += highValue * highValue;
                sumCross += left * right;
                frames++;
            }
        }

        public ScannedProfile BuildProfile()
        {
            if (frames <= 0)
            {
                return new ScannedProfile
                {
                    Mode = ListeningMode.Speakers,
                    Volume = 50,
                    Bass = 0,
                    Treble = 0,
                    Width = 50,
                    Surround = 35,
                    Compression = 20
                };
            }

            double rmsL = Math.Sqrt(sumL / frames);
            double rmsR = Math.Sqrt(sumR / frames);
            double denom = Math.Sqrt(sumL * sumR);
            double correlation = denom > 0 ? sumCross / denom : 1;
            double widthRatio = Math.Sqrt(sumSide / Math.Max(0.0000001, sumMid));
            double lowRatio = Math.Sqrt(sumLow / Math.Max(0.0000001, sumMid + sumSide));
            double highRatio = Math.Sqrt(sumHigh / Math.Max(0.0000001, sumMid + sumSide));
            double loudness = Math.Max(rmsL, rmsR);

            return new ScannedProfile
            {
                Mode = correlation < 0.25 || widthRatio > 0.9 ? ListeningMode.Headphones : ListeningMode.Speakers,
                Volume = ClampToSlider((int)Math.Round(50 + (loudness - 0.12) * 140)),
                Bass = ClampToSlider((int)Math.Round((lowRatio - 0.35) * 90)),
                Treble = ClampToSlider((int)Math.Round((highRatio - 0.08) * 160)),
                Width = ClampToSlider((int)Math.Round(50 + widthRatio * 35)),
                Surround = ClampToSlider((int)Math.Round(35 + Math.Max(0, 0.65 - correlation) * 70)),
                Compression = ClampToSlider((int)Math.Round(20 + Math.Max(0, loudness - 0.2) * 100))
            };
        }

        private static int ClampToSlider(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private static float ReadSample(byte[] buffer, int offset, WaveFormat format)
        {
            if (format.IsIeeeFloat && format.BitsPerSample == 32)
            {
                return BitConverter.ToSingle(buffer, offset);
            }

            if (format.BitsPerSample == 16)
            {
                return BitConverter.ToInt16(buffer, offset) / 32768f;
            }

            if (format.BitsPerSample == 24)
            {
                int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
                if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000);
                return value / 8388608f;
            }

            if (format.BitsPerSample == 32)
            {
                return BitConverter.ToInt32(buffer, offset) / 2147483648f;
            }

            return 0;
        }
    }

    internal struct AudioProcessResult
    {
        public float LeftInput;
        public float RightInput;
        public float LeftOutput;
        public float RightOutput;
        public byte[] Pcm16Stereo;
    }

    internal sealed class ProcessedAudioPlayer : IDisposable
    {
        private const int WaveMapper = -1;
        private const int WhdrDone = 0x00000001;
        private IntPtr waveOut;
        private int sampleRate;
        private readonly System.Collections.Generic.List<PendingWaveBuffer> pending = new System.Collections.Generic.List<PendingWaveBuffer>();

        public void Play(byte[] pcm16Stereo, int newSampleRate)
        {
            if (pcm16Stereo == null || pcm16Stereo.Length == 0 || newSampleRate <= 0)
            {
                return;
            }

            if (waveOut == IntPtr.Zero || newSampleRate != sampleRate)
            {
                Stop();
                Open(newSampleRate);
            }

            CleanupCompleted();

            var pendingBuffer = new PendingWaveBuffer(pcm16Stereo);
            int result = waveOutPrepareHeader(waveOut, pendingBuffer.HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
            if (result != 0)
            {
                pendingBuffer.Dispose();
                return;
            }

            result = waveOutWrite(waveOut, pendingBuffer.HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
            if (result != 0)
            {
                waveOutUnprepareHeader(waveOut, pendingBuffer.HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
                pendingBuffer.Dispose();
                return;
            }

            pending.Add(pendingBuffer);
        }

        public void Stop()
        {
            if (waveOut != IntPtr.Zero)
            {
                waveOutReset(waveOut);
                foreach (var item in pending)
                {
                    waveOutUnprepareHeader(waveOut, item.HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
                    item.Dispose();
                }
                pending.Clear();
                waveOutClose(waveOut);
                waveOut = IntPtr.Zero;
            }
            sampleRate = 0;
        }

        public void Dispose()
        {
            Stop();
        }

        private void Open(int newSampleRate)
        {
            var format = new WaveFormatEx
            {
                FormatTag = 1,
                Channels = 2,
                SamplesPerSec = newSampleRate,
                BitsPerSample = 16,
                BlockAlign = 4,
                AvgBytesPerSec = newSampleRate * 4,
                Size = 0
            };

            int result = waveOutOpen(out waveOut, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (result != 0)
            {
                waveOut = IntPtr.Zero;
                return;
            }

            sampleRate = newSampleRate;
        }

        private void CleanupCompleted()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                WaveHdr header = (WaveHdr)Marshal.PtrToStructure(pending[i].HeaderPointer, typeof(WaveHdr));
                if ((header.Flags & WhdrDone) != 0)
                {
                    waveOutUnprepareHeader(waveOut, pending[i].HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
                    pending[i].Dispose();
                    pending.RemoveAt(i);
                }
            }

            while (pending.Count > 12)
            {
                waveOutUnprepareHeader(waveOut, pending[0].HeaderPointer, Marshal.SizeOf(typeof(WaveHdr)));
                pending[0].Dispose();
                pending.RemoveAt(0);
            }
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int deviceId, ref WaveFormatEx format, IntPtr callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);
    }

    internal sealed class PendingWaveBuffer : IDisposable
    {
        private GCHandle dataHandle;
        private GCHandle headerHandle;

        public PendingWaveBuffer(byte[] data)
        {
            dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var header = new WaveHdr
            {
                Data = dataHandle.AddrOfPinnedObject(),
                BufferLength = data.Length,
                Flags = 0,
                Loops = 0
            };
            headerHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
        }

        public IntPtr HeaderPointer
        {
            get { return headerHandle.AddrOfPinnedObject(); }
        }

        public void Dispose()
        {
            if (headerHandle.IsAllocated) headerHandle.Free();
            if (dataHandle.IsAllocated) dataHandle.Free();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveHdr
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WaveFormatEx
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short Size;
    }

    internal sealed class WasapiLoopbackCapture : IDisposable
    {
        private const int DeviceStateActive = 0x00000001;
        private const int ClsctxAll = 23;
        private const int AudclntSharemodeShared = 0;
        private const int AudclntStreamflagsLoopback = 0x00020000;
        private const int BufferFlagsSilent = 0x00000002;

        private readonly AutoResetEvent frameEvent = new AutoResetEvent(false);
        private readonly Thread thread;
        private volatile bool running;
        private IAudioClient audioClient;
        private IAudioCaptureClient captureClient;
        private WaveFormat waveFormat;

        public event EventHandler<AudioBufferEventArgs> AudioAvailable;
        public event EventHandler CaptureStopped;
        public Exception LastError { get; private set; }

        public WasapiLoopbackCapture()
        {
            thread = new Thread(CaptureThread) { IsBackground = true, Name = "WASAPI loopback capture" };
        }

        public void Start()
        {
            running = true;
            thread.Start();
        }

        public void Dispose()
        {
            running = false;
            frameEvent.Set();
            if (thread.IsAlive && Thread.CurrentThread != thread)
            {
                thread.Join(1000);
            }
            ReleaseCom();
            frameEvent.Dispose();
        }

        private void CaptureThread()
        {
            try
            {
                Initialize();
                audioClient.Start();

                while (running)
                {
                    Thread.Sleep(10);
                    DrainCaptureBuffer();
                }

                audioClient.Stop();
            }
            catch (Exception ex)
            {
                LastError = ex;
            }
            finally
            {
                ReleaseCom();
                var stopped = CaptureStopped;
                if (stopped != null) stopped(this, EventArgs.Empty);
            }
        }

        private void Initialize()
        {
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            IMMDevice device;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out device));
            Marshal.ReleaseComObject(enumerator);

            Guid audioClientGuid = typeof(IAudioClient).GUID;
            object obj;
            Marshal.ThrowExceptionForHR(device.Activate(ref audioClientGuid, ClsctxAll, IntPtr.Zero, out obj));
            Marshal.ReleaseComObject(device);

            audioClient = (IAudioClient)obj;

            IntPtr formatPtr;
            Marshal.ThrowExceptionForHR(audioClient.GetMixFormat(out formatPtr));
            waveFormat = WaveFormat.FromPointer(formatPtr);
            Marshal.FreeCoTaskMem(formatPtr);

            long bufferDuration = 10000000;
            IntPtr waveFormatPtr = waveFormat.ToPointer();
            try
            {
                Marshal.ThrowExceptionForHR(audioClient.Initialize(
                    AudclntSharemodeShared,
                    AudclntStreamflagsLoopback,
                    bufferDuration,
                    0,
                    waveFormatPtr,
                    IntPtr.Zero));
            }
            finally
            {
                Marshal.FreeHGlobal(waveFormatPtr);
            }

            Guid captureClientGuid = typeof(IAudioCaptureClient).GUID;
            object captureObj;
            Marshal.ThrowExceptionForHR(audioClient.GetService(ref captureClientGuid, out captureObj));
            captureClient = (IAudioCaptureClient)captureObj;
        }

        private void DrainCaptureBuffer()
        {
            int nextPacketSize;
            Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out nextPacketSize));

            while (nextPacketSize > 0 && running)
            {
                IntPtr data;
                int frames;
                int flags;
                long position;
                long qpc;
                Marshal.ThrowExceptionForHR(captureClient.GetBuffer(out data, out frames, out flags, out position, out qpc));

                int bytes = frames * waveFormat.BlockAlign;
                byte[] managed = new byte[bytes];
                if ((flags & BufferFlagsSilent) == 0 && bytes > 0)
                {
                    Marshal.Copy(data, managed, 0, bytes);
                }

                Marshal.ThrowExceptionForHR(captureClient.ReleaseBuffer(frames));

                var handler = AudioAvailable;
                if (handler != null && bytes > 0)
                {
                    handler(this, new AudioBufferEventArgs(managed, bytes, waveFormat));
                }

                Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out nextPacketSize));
            }
        }

        private void ReleaseCom()
        {
            if (captureClient != null)
            {
                Marshal.ReleaseComObject(captureClient);
                captureClient = null;
            }
            if (audioClient != null)
            {
                Marshal.ReleaseComObject(audioClient);
                audioClient = null;
            }
        }
    }

    internal sealed class AudioBufferEventArgs : EventArgs
    {
        public AudioBufferEventArgs(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
            WaveFormat = waveFormat;
        }

        public byte[] Buffer { get; private set; }
        public int BytesRecorded { get; private set; }
        public WaveFormat WaveFormat { get; private set; }
    }

    internal static class AudioDiagnostics
    {
        public static void Run(string[] args)
        {
            string reportPath = System.IO.Path.Combine(Environment.CurrentDirectory, "audio-diagnostics.txt");
            var report = new System.Text.StringBuilder();
            report.AppendLine("Windows PEQ App Audio Diagnostics");
            report.AppendLine("Started: " + DateTime.Now.ToString("s"));
            report.AppendLine();

            try
            {
                RunSyntheticDspTests(report);
                if (args.Length > 1 && args[1].Equals("--capture", StringComparison.OrdinalIgnoreCase))
                {
                    RunLiveCaptureTest(report, args.Length > 2 ? ParseInt(args[2], 8) : 8);
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("FAIL: diagnostics crashed: " + ex.Message);
            }

            System.IO.File.WriteAllText(reportPath, report.ToString());
        }

        private static void RunSyntheticDspTests(System.Text.StringBuilder report)
        {
            var format = new WaveFormat
            {
                AudioFormat = 3,
                Channels = 2,
                SampleRate = 48000,
                BitsPerSample = 32,
                BlockAlign = 8,
                AverageBytesPerSecond = 384000,
                ExtraSize = 0
            };

            byte[] input = CreateStereoFloatTestBuffer(format.SampleRate, 2.0);
            var flat = new AudioProcessor { Volume = 1, Bass = 0, Treble = 0, Width = 1, Surround = 0, Compression = 0, Mode = ListeningMode.Speakers };
            flat.PeqSettings = new PeqSettings { Enabled = false };
            var flatResult = flat.Process(input, input.Length, format);

            var magic = new AudioProcessor { Volume = 1, Bass = 0.35f, Treble = 0.35f, Width = 1.8f, Surround = 0.9f, Compression = 0.35f, Mode = ListeningMode.Headphones };
            magic.PeqSettings = new PeqSettings { Enabled = false };
            var magicResult = magic.Process(input, input.Length, format);
            double magicDelta = AverageAbsDiff(flatResult.Pcm16Stereo, magicResult.Pcm16Stereo);
            report.AppendLine((magicDelta > 250 ? "PASS" : "FAIL") + ": Magic acoustics changes signal, avg PCM delta=" + magicDelta.ToString("0.0"));

            var peqProfile = PeqProfile.Find("Cinema");
            var peqSettings = new PeqSettings
            {
                Enabled = true,
                PreampDb = peqProfile.PreampDb,
                BassBoostDb = peqProfile.BassBoostDb,
                LoudnessDb = peqProfile.LoudnessDb,
                StereoWidth = peqProfile.StereoWidth,
                VirtualizerStrength = peqProfile.VirtualizerStrength,
                ReverbStrength = peqProfile.ReverbStrength
            };
            foreach (var band in peqProfile.Bands) peqSettings.Bands.Add(band);

            var peq = new AudioProcessor { Volume = 1, Bass = 0, Treble = 0, Width = 1, Surround = 0, Compression = 0, Mode = ListeningMode.Speakers, PeqSettings = peqSettings };
            var peqResult = peq.Process(input, input.Length, format);
            double peqDelta = AverageAbsDiff(flatResult.Pcm16Stereo, peqResult.Pcm16Stereo);
            report.AppendLine((peqDelta > 250 ? "PASS" : "FAIL") + ": PEQ Cinema preset changes signal, avg PCM delta=" + peqDelta.ToString("0.0"));

            AutoEqProfile[] autoEqProfiles = AutoEqProfile.BuiltIns();
            report.AppendLine((autoEqProfiles.Length > 0 ? "PASS" : "FAIL") + ": AutoEQ list count=" + autoEqProfiles.Length);
            var parsed = PeqConfigParser.Parse(autoEqProfiles[0].ConfigText);
            report.AppendLine((parsed.Bands.Count > 0 ? "PASS" : "FAIL") + ": AutoEQ apply parser bands=" + parsed.Bands.Count + ", first=" + autoEqProfiles[0].Name);

            var peqJson = "{\"pre_amp\":-4,\"bands\":[{\"on\":1,\"filter\":0,\"freq\":80,\"gain\":5,\"q\":0.7},{\"on\":1,\"filter\":1,\"freq\":3000,\"gain\":-3,\"q\":1.2}]}";
            var parsedJson = PeqConfigParser.Parse(peqJson);
            report.AppendLine((parsedJson.Bands.Count == 2 && Math.Abs(parsedJson.PreampDb + 4) < 0.01 ? "PASS" : "FAIL") + ": PEQ-AutoEQ JSON parser bands=" + parsedJson.Bands.Count);
        }

        private static void RunLiveCaptureTest(System.Text.StringBuilder report, int seconds)
        {
            report.AppendLine();
            report.AppendLine("Live capture test: " + seconds + " seconds");
            long bytes = 0;
            double peakInput = 0;
            double peakOutput = 0;
            int callbacks = 0;
            var processor = new AudioProcessor { Volume = 1.4f, Bass = 0.25f, Treble = 0.25f, Width = 1.6f, Surround = 0.7f, Compression = 0.3f, Mode = ListeningMode.Speakers };
            var settings = PeqConfigParser.Parse(AutoEqProfile.BuiltIns()[0].ConfigText);
            settings.Enabled = true;
            settings.BassBoostDb = 4;
            settings.LoudnessDb = 3;
            settings.StereoWidth = 1.4f;
            settings.VirtualizerStrength = 60;
            processor.PeqSettings = settings;

            Exception captureError;
            bool fallbackToneUsed = false;
            using (var capture = new WasapiLoopbackCapture())
            {
                capture.AudioAvailable += delegate(object sender, AudioBufferEventArgs e)
                {
                    callbacks++;
                    bytes += e.BytesRecorded;
                    var result = processor.Process(e.Buffer, e.BytesRecorded, e.WaveFormat);
                    peakInput = Math.Max(peakInput, Math.Max(result.LeftInput, result.RightInput));
                    peakOutput = Math.Max(peakOutput, Math.Max(result.LeftOutput, result.RightOutput));
                };
                capture.Start();
                Thread.Sleep(Math.Max(1, seconds / 2) * 1000);
                if (callbacks == 0)
                {
                    fallbackToneUsed = true;
                    using (var player = new ProcessedAudioPlayer())
                    {
                        byte[] tone = CreateStereoPcm16Tone(48000, 0.2);
                        for (int i = 0; i < 20; i++)
                        {
                            player.Play(tone, 48000);
                            Thread.Sleep(100);
                        }
                    }
                }
                Thread.Sleep(Math.Max(1, seconds / 2) * 1000);
                captureError = capture.LastError;
            }

            report.AppendLine((fallbackToneUsed ? "WARN" : "PASS") + ": Chrome/YouTube direct packets=" + (fallbackToneUsed ? "none, fallback tone used" : "present"));
            report.AppendLine((callbacks > 0 ? "PASS" : "FAIL") + ": WASAPI callbacks=" + callbacks);
            report.AppendLine((bytes > 0 ? "PASS" : "FAIL") + ": captured bytes=" + bytes);
            report.AppendLine((captureError == null ? "PASS" : "FAIL") + ": WASAPI error=" + (captureError == null ? "none" : captureError.Message));
            report.AppendLine((peakInput > 0.005 ? "PASS" : "WARN") + ": input peak meter=" + peakInput.ToString("0.000"));
            report.AppendLine((peakOutput > 0.005 ? "PASS" : "WARN") + ": processed peak meter=" + peakOutput.ToString("0.000"));
            report.AppendLine("Note: if input peak is WARN, Chrome/YouTube did not autoplay or the system output was silent.");
        }

        private static byte[] CreateStereoFloatTestBuffer(int sampleRate, double seconds)
        {
            int frames = (int)(sampleRate * seconds);
            byte[] buffer = new byte[frames * 8];
            for (int i = 0; i < frames; i++)
            {
                double t = i / (double)sampleRate;
                float left = (float)(0.18 * Math.Sin(2 * Math.PI * 80 * t) + 0.16 * Math.Sin(2 * Math.PI * 1000 * t) + 0.11 * Math.Sin(2 * Math.PI * 9000 * t));
                float right = (float)(0.15 * Math.Sin(2 * Math.PI * 100 * t + 0.4) + 0.13 * Math.Sin(2 * Math.PI * 1500 * t) + 0.10 * Math.Sin(2 * Math.PI * 7000 * t + 1.1));
                Buffer.BlockCopy(BitConverter.GetBytes(left), 0, buffer, i * 8, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(right), 0, buffer, i * 8 + 4, 4);
            }
            return buffer;
        }

        private static byte[] CreateStereoPcm16Tone(int sampleRate, double seconds)
        {
            int frames = (int)(sampleRate * seconds);
            byte[] buffer = new byte[frames * 4];
            for (int i = 0; i < frames; i++)
            {
                double t = i / (double)sampleRate;
                short left = (short)(Math.Sin(2 * Math.PI * 440 * t) * 5000);
                short right = (short)(Math.Sin(2 * Math.PI * 660 * t) * 5000);
                buffer[i * 4] = (byte)(left & 0xff);
                buffer[i * 4 + 1] = (byte)((left >> 8) & 0xff);
                buffer[i * 4 + 2] = (byte)(right & 0xff);
                buffer[i * 4 + 3] = (byte)((right >> 8) & 0xff);
            }
            return buffer;
        }

        private static double AverageAbsDiff(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0;
            int samples = Math.Min(a.Length, b.Length) / 2;
            long total = 0;
            for (int i = 0; i < samples; i++)
            {
                short av = BitConverter.ToInt16(a, i * 2);
                short bv = BitConverter.ToInt16(b, i * 2);
                total += Math.Abs(av - bv);
            }
            return total / (double)samples;
        }

        private static int ParseInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }
    }

    internal sealed class WaveFormat
    {
        public short AudioFormat;
        public short Channels;
        public int SampleRate;
        public int AverageBytesPerSecond;
        public short BlockAlign;
        public short BitsPerSample;
        public short ExtraSize;
        public byte[] RawBytes;

        public static WaveFormat FromPointer(IntPtr pointer)
        {
            var native = (NativeWaveFormat)Marshal.PtrToStructure(pointer, typeof(NativeWaveFormat));
            var format = new WaveFormat
            {
                AudioFormat = native.AudioFormat,
                Channels = native.Channels,
                SampleRate = native.SampleRate,
                AverageBytesPerSecond = native.AverageBytesPerSecond,
                BlockAlign = native.BlockAlign,
                BitsPerSample = native.BitsPerSample,
                ExtraSize = native.ExtraSize
            };

            int size = 18 + Math.Max(0, (int)format.ExtraSize);
            format.RawBytes = new byte[size];
            Marshal.Copy(pointer, format.RawBytes, 0, size);
            return format;
        }

        public IntPtr ToPointer()
        {
            byte[] bytes = RawBytes;
            if (bytes == null || bytes.Length == 0)
            {
                bytes = new byte[18];
                WriteShort(bytes, 0, AudioFormat);
                WriteShort(bytes, 2, Channels);
                WriteInt(bytes, 4, SampleRate);
                WriteInt(bytes, 8, AverageBytesPerSecond);
                WriteShort(bytes, 12, BlockAlign);
                WriteShort(bytes, 14, BitsPerSample);
                WriteShort(bytes, 16, ExtraSize);
            }

            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        public bool IsIeeeFloat
        {
            get
            {
                if (AudioFormat == 3) return true;
                if (AudioFormat != unchecked((short)0xfffe) || RawBytes == null || RawBytes.Length < 40) return false;

                return RawBytes[24] == 0x03 && RawBytes[25] == 0x00 &&
                    RawBytes[26] == 0x00 && RawBytes[27] == 0x00 &&
                    RawBytes[28] == 0x00 && RawBytes[29] == 0x00 &&
                    RawBytes[30] == 0x10 && RawBytes[31] == 0x00 &&
                    RawBytes[32] == 0x80 && RawBytes[33] == 0x00 &&
                    RawBytes[34] == 0x00 && RawBytes[35] == 0xaa &&
                    RawBytes[36] == 0x00 && RawBytes[37] == 0x38 &&
                    RawBytes[38] == 0x9b && RawBytes[39] == 0x71;
            }
        }

        private static void WriteShort(byte[] buffer, int offset, short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct NativeWaveFormat
    {
        public short AudioFormat;
        public short Channels;
        public int SampleRate;
        public int AverageBytesPerSecond;
        public short BlockAlign;
        public short BitsPerSample;
        public short ExtraSize;
    }

    internal enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    internal enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal sealed class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out object devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        int OpenPropertyStore(int access, out object properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);
        int GetBufferSize(out int bufferSize);
        int GetStreamLatency(out long latency);
        int GetCurrentPadding(out int padding);
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        int GetMixFormat(out IntPtr deviceFormat);
        int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr eventHandle);
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out int framesToRead, out int flags, out long devicePosition, out long qpcPosition);
        int ReleaseBuffer(int framesRead);
        int GetNextPacketSize(out int packetSize);
    }
}
