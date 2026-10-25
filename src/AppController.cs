using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace NotchPrompter
{
    /// <summary>Owns the island, tray icon, global shortcuts, camera watcher and editor, and wires them together.</summary>
    internal sealed class AppController
    {
        public const string ShowSignal = @"Local\NotchPrompter.Show";
        public const string QuitSignal = @"Local\NotchPrompter.Quit";

        readonly Options _opts;
        Settings _s;
        IslandWindow _island;
        EditorWindow _editor;
        HotkeyManager _hotkeys;
        CameraWatcher _camera;
        WF.NotifyIcon _tray;
        System.Windows.Threading.DispatcherTimer _saveTimer;
        EventWaitHandle _showEvent, _quitEvent;
        RegisteredWaitHandle _showWait, _quitWait;
        bool _exiting;
        bool _captureOverride;   // --capture-visible, cleared once the tray option is touched

        WF.ToolStripMenuItem _playItem, _countdownItem, _cameraItem, _captureItem, _idleItem, _centerItem, _startupItem;

        public AppController(Options opts)
        {
            _opts = opts;
            _captureOverride = opts.CaptureVisible;
        }

        bool HideFromCapture() => _s.ExcludeFromCapture && !_captureOverride;

        public void Start()
        {
            Directory.CreateDirectory(Settings.Dir);
            _s = Settings.Load();
            Theme.Install();

            _saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (o, e) => { _saveTimer.Stop(); _s.Save(); };

            _island = new IslandWindow(_s, _opts.CaptureVisible);
            _island.SetScript(ScriptStore.Load(), restoreOffset: true);
            _island.EditRequested += OpenEditor;
            _island.SettingsChanged += ScheduleSave;
            _island.Show();

            BuildTray();
            RegisterHotkeys();

            _camera = new CameraWatcher();
            _camera.Changed += on => _island.SetCameraActive(on);
            _camera.Start();

            ListenForSignals();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            if (_s.StartWithWindows) AutoStart.Set(true);   // also refreshes the path if the exe moved

            if (!_s.FirstRunDone)
            {
                _s.FirstRunDone = true;
                _s.Save();
                Balloon("NotchPrompter is running",
                    "Hover the notch at the top of your screen or press Ctrl+Alt+T. Ctrl+Alt+E edits your script.");
            }
            if (_hotkeys.Failed.Count > 0)
                Balloon("Some shortcuts are taken", "Another app already uses: " + string.Join(", ", _hotkeys.Failed));

            if (_opts.Expand) _island.Expand();
            if (_opts.Play) _island.TogglePlay();
            if (_opts.Edit) OpenEditor();
            _island.ApplyIdleVisibility();
            UpdateTrayText();
        }

        // ------------------------------------------------------------------ actions

        void OpenEditor()
        {
            if (_editor == null)
            {
                _editor = new EditorWindow(_s, ScriptStore.Load(), HideFromCapture);
                _editor.ScriptChanged += text =>
                {
                    ScriptStore.Save(text);
                    _island.SetScript(text);
                };
                _editor.StartRequested += () =>
                {
                    _island.Restart(quiet: true);
                    _island.Expand();
                    _island.TogglePlay();
                };
                _editor.Closed += (o, e) => _editor = null;
                _editor.Show();
            }
            if (_editor.WindowState == WindowState.Minimized) _editor.WindowState = WindowState.Normal;
            _editor.Activate();
        }

        void OpenScriptFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Text scripts (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                string text = File.ReadAllText(dlg.FileName);
                if (_editor != null) _editor.SetText(text);
                ScriptStore.Save(text);
                _island.SetScript(text);
                _island.Restart(quiet: true);
                _island.Expand();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't open that file:\n" + ex.Message, "NotchPrompter");
            }
        }

        void ApplyCapture()
        {
            _island.ApplyCaptureSetting();
            _editor?.ApplyCaptureSetting();
            UpdateTrayText();
        }

        void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void Exit()
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                _island.SaveState();
                _s.Save();
                _hotkeys?.Dispose();
                _camera?.Dispose();
                _showWait?.Unregister(null);
                _quitWait?.Unregister(null);
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                _editor?.Close();
            }
            catch (Exception ex)
            {
                Log.Write("exit cleanup failed: " + ex);
            }
            Application.Current.Shutdown();
        }

        // ------------------------------------------------------------------ wiring

        void RegisterHotkeys()
        {
            const uint CA = Native.MOD_CONTROL | Native.MOD_ALT;
            _hotkeys = new HotkeyManager();
            _hotkeys.Register("Ctrl+Alt+T", CA, Key.T, () => _island.Toggle());
            _hotkeys.Register("Ctrl+Alt+Space", CA, Key.Space, () => _island.TogglePlay());
            _hotkeys.Register("Ctrl+Alt+R", CA, Key.R, () => _island.Restart());
            _hotkeys.Register("Ctrl+Alt+E", CA, Key.E, OpenEditor);
            _hotkeys.Register("Ctrl+Alt+=", CA, Key.OemPlus, () => _island.ChangeWpm(+10), repeat: true);
            _hotkeys.Register("Ctrl+Alt+-", CA, Key.OemMinus, () => _island.ChangeWpm(-10), repeat: true);
            _hotkeys.Register("Ctrl+Alt+]", CA, Key.OemCloseBrackets, () => _island.ChangeFont(+2), repeat: true);
            _hotkeys.Register("Ctrl+Alt+[", CA, Key.OemOpenBrackets, () => _island.ChangeFont(-2), repeat: true);
            _hotkeys.Register("Ctrl+Alt+PageUp", CA, Key.PageUp, () => _island.Jump(-3), repeat: true);
            _hotkeys.Register("Ctrl+Alt+PageDown", CA, Key.PageDown, () => _island.Jump(+3), repeat: true);
        }

        /// <summary>A second launch of the exe signals this instance instead of starting another one.</summary>
        void ListenForSignals()
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
            _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitSignal);
            var dispatcher = Application.Current.Dispatcher;
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
                (st, timedOut) => dispatcher.BeginInvoke(new Action(() => _island.Expand())), null, -1, false);
            _quitWait = ThreadPool.RegisterWaitForSingleObject(_quitEvent,
                (st, timedOut) => dispatcher.BeginInvoke(new Action(Exit)), null, -1, false);
        }

        void OnDisplaySettingsChanged(object sender, EventArgs e) =>
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _island.Reposition()));

        // ------------------------------------------------------------------ tray

        void BuildTray()
        {
            var menu = new WF.ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                Font = new SD.Font("Segoe UI", 9.5f),
                ShowImageMargin = false,
                ShowCheckMargin = true,
            };

            menu.Items.Add(Item("Show / hide teleprompter", "Ctrl+Alt+T", () => _island.Toggle()));
            _playItem = Item("Play", "Ctrl+Alt+Space", () => _island.TogglePlay());
            menu.Items.Add(_playItem);
            menu.Items.Add(Item("Back to the top", "Ctrl+Alt+R", () => _island.Restart()));
            menu.Items.Add(Item("Edit script…", "Ctrl+Alt+E", OpenEditor));
            menu.Items.Add(Item("Open script file…", null, OpenScriptFile));
            menu.Items.Add(new WF.ToolStripSeparator());

            var size = new WF.ToolStripMenuItem("Island size") { ForeColor = SD.Color.White };
            size.DropDown.BackColor = SD.Color.FromArgb(36, 36, 38);
            size.DropDownItems.Add(Item("Small", null, () => _island.ApplyPreset(500, 150, 24)));
            size.DropDownItems.Add(Item("Medium", null, () => _island.ApplyPreset(620, 200, 28)));
            size.DropDownItems.Add(Item("Large", null, () => _island.ApplyPreset(780, 260, 34)));
            size.DropDownItems.Add(Item("Extra large", null, () => _island.ApplyPreset(960, 330, 42)));
            size.DropDown.Opened += (o, e) => CaptureGuard.Apply(size.DropDown.Handle, HideFromCapture());
            menu.Items.Add(size);

            _countdownItem = Toggle("3-2-1 countdown before playing", v => _s.Countdown = v);
            _cameraItem = Toggle("Open when the camera turns on", v => _s.AutoOpenOnCamera = v);
            _captureItem = Toggle("Hide from screen recording and sharing", v =>
            {
                _s.ExcludeFromCapture = v;
                _captureOverride = false;
                _island.ClearCaptureOverride();
                ApplyCapture();
            });
            _idleItem = Toggle("Hide the notch when idle", v => { _s.HideNotchWhenIdle = v; _island.ApplyIdleVisibility(); });
            _centerItem = Toggle("Center the text", v => { _s.CenterText = v; _island.RefreshLayout(); });
            _startupItem = Toggle("Start with Windows", v => { _s.StartWithWindows = v; AutoStart.Set(v); });
            menu.Items.AddRange(new WF.ToolStripItem[] { _countdownItem, _cameraItem, _captureItem, _idleItem, _centerItem, _startupItem });
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add(Item("Keyboard shortcuts", null, ShowShortcuts));
            menu.Items.Add(Item("Quit NotchPrompter", null, Exit));

            menu.Opening += (o, e) =>
            {
                _playItem.Text = _island.IsPlaying ? "Pause" : "Play";
                _countdownItem.Checked = _s.Countdown;
                _cameraItem.Checked = _s.AutoOpenOnCamera;
                _captureItem.Checked = _s.ExcludeFromCapture;
                _idleItem.Checked = _s.HideNotchWhenIdle;
                _centerItem.Checked = _s.CenterText;
                _startupItem.Checked = _s.StartWithWindows;
            };
            // The menu is its own window; keep it out of recordings as well.
            menu.Opened += (o, e) => CaptureGuard.Apply(menu.Handle, HideFromCapture());

            _tray = new WF.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "NotchPrompter",
                ContextMenuStrip = menu,
                Visible = true,
            };
            _tray.MouseClick += (o, e) =>
            {
                if (e.Button == WF.MouseButtons.Left) _island.Toggle();
            };
        }

        WF.ToolStripMenuItem Item(string text, string shortcut, Action action)
        {
            var item = new WF.ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut, ForeColor = SD.Color.White };
            item.Click += (o, e) => action();
            return item;
        }

        WF.ToolStripMenuItem Toggle(string text, Action<bool> apply)
        {
            var item = new WF.ToolStripMenuItem(text) { ForeColor = SD.Color.White };
            item.Click += (o, e) =>
            {
                item.Checked = !item.Checked;
                apply(item.Checked);
                ScheduleSave();
            };
            return item;
        }

        void UpdateTrayText()
        {
            if (_tray == null) return;
            string state = _island.CaptureAffinity == Native.WDA_EXCLUDEFROMCAPTURE ? "hidden from recordings"
                : _island.CaptureAffinity == Native.WDA_MONITOR ? "blacked out in recordings"
                : "visible in recordings";
            _tray.Text = "NotchPrompter - " + state;
        }

        void Balloon(string title, string text)
        {
            if (_opts.AutoStart || _tray == null) return;
            _tray.ShowBalloonTip(6000, title, text, WF.ToolTipIcon.None);
        }

        static void ShowShortcuts()
        {
            MessageBox.Show(
                "Ctrl + Alt + T\t\tShow / hide the teleprompter\n" +
                "Ctrl + Alt + Space\tPlay / pause\n" +
                "Ctrl + Alt + R\t\tBack to the top\n" +
                "Ctrl + Alt + =  /  -\tFaster / slower\n" +
                "Ctrl + Alt + ]  /  [\tBigger / smaller text\n" +
                "Ctrl + Alt + PgUp / PgDn\tJump back / forward\n" +
                "Ctrl + Alt + E\t\tEdit the script\n\n" +
                "On the island: click the text to play or pause, scroll to move through the script, " +
                "Ctrl + scroll for text size, Shift + scroll for speed, and drag the little handle at the " +
                "bottom to resize it.",
                "NotchPrompter shortcuts");
        }

        static SD.Icon LoadTrayIcon()
        {
            try
            {
                using var stream = typeof(AppController).Assembly.GetManifestResourceStream("NotchPrompter.app.ico");
                if (stream != null) return new SD.Icon(stream, WF.SystemInformation.SmallIconSize);
            }
            catch (Exception ex)
            {
                Log.Write("tray icon load failed: " + ex.Message);
            }
            return SD.SystemIcons.Application;
        }
    }

    /// <summary>Dark tray menu to match the island.</summary>
    internal sealed class DarkMenuRenderer : WF.ToolStripProfessionalRenderer
    {
        static readonly SD.Color TextColor = SD.Color.FromArgb(242, 242, 247);
        static readonly SD.Color ShortcutColor = SD.Color.FromArgb(142, 142, 147);
        static readonly SD.Color Accent = SD.Color.FromArgb(10, 132, 255);

        public DarkMenuRenderer() : base(new DarkMenuColors()) => RoundedEdges = false;

        protected override void OnRenderItemText(WF.ToolStripItemTextRenderEventArgs e)
        {
            bool isShortcut = e.Item is WF.ToolStripMenuItem mi && mi.ShortcutKeyDisplayString != null && e.Text == mi.ShortcutKeyDisplayString;
            e.TextColor = !e.Item.Enabled ? ShortcutColor : isShortcut ? ShortcutColor : TextColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(WF.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = ShortcutColor;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(WF.ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SD.Drawing2D.SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            float u = Math.Min(r.Width, r.Height) / 16f, cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using var pen = new SD.Pen(Accent, 2f * u)
            {
                StartCap = SD.Drawing2D.LineCap.Round,
                EndCap = SD.Drawing2D.LineCap.Round,
                LineJoin = SD.Drawing2D.LineJoin.Round,
            };
            g.DrawLines(pen, new[] { new SD.PointF(cx - 4.5f * u, cy), new SD.PointF(cx - 1.2f * u, cy + 3.3f * u), new SD.PointF(cx + 4.8f * u, cy - 3.6f * u) });
        }
    }

    internal sealed class DarkMenuColors : WF.ProfessionalColorTable
    {
        static readonly SD.Color Bg = SD.Color.FromArgb(36, 36, 38);
        static readonly SD.Color Hover = SD.Color.FromArgb(58, 58, 62);
        static readonly SD.Color Line = SD.Color.FromArgb(62, 62, 66);

        public override SD.Color ToolStripDropDownBackground => Bg;
        public override SD.Color ImageMarginGradientBegin => Bg;
        public override SD.Color ImageMarginGradientMiddle => Bg;
        public override SD.Color ImageMarginGradientEnd => Bg;
        public override SD.Color MenuBorder => Line;
        public override SD.Color MenuItemBorder => Hover;
        public override SD.Color MenuItemSelected => Hover;
        public override SD.Color MenuItemSelectedGradientBegin => Hover;
        public override SD.Color MenuItemSelectedGradientEnd => Hover;
        public override SD.Color MenuItemPressedGradientBegin => Hover;
        public override SD.Color MenuItemPressedGradientEnd => Hover;
        public override SD.Color SeparatorDark => Line;
        public override SD.Color SeparatorLight => Bg;
        public override SD.Color CheckBackground => Bg;
        public override SD.Color CheckSelectedBackground => Hover;
        public override SD.Color CheckPressedBackground => Hover;
    }
}
