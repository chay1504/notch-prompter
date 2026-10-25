using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NotchPrompter
{
    /// <summary>Dark script editor. Edits flow to the island live (debounced) and are saved to script.txt.</summary>
    internal sealed class EditorWindow : Window
    {
        public event Action<string> ScriptChanged;
        public event Action StartRequested;

        readonly Settings _s;
        readonly Func<bool> _hideFromCapture;
        readonly TextBox _box;
        readonly TextBlock _stats;
        readonly DispatcherTimer _debounce;
        bool _dirty;

        static readonly Brush Text = Solid(0xF2, 0xF2, 0xF7);
        static readonly Brush Muted = Solid(0x8E, 0x8E, 0x93);
        static readonly Brush Faint = Solid(0x63, 0x63, 0x66);

        public EditorWindow(Settings settings, string script, Func<bool> hideFromCapture)
        {
            _s = settings;
            _hideFromCapture = hideFromCapture;
            Title = "NotchPrompter - Script";
            Width = 780;
            Height = 600;
            MinWidth = 560;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Solid(0x16, 0x16, 0x18);
            FontFamily = new FontFamily("Segoe UI");
            Icon = LoadIcon();

            var root = new DockPanel();

            var header = new Grid { Margin = new Thickness(22, 16, 22, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = "Your script", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Text });
            titles.Children.Add(new TextBlock { Text = "Changes appear in the island as you type.", FontSize = 12.5, Foreground = Muted, Margin = new Thickness(0, 2, 0, 0) });
            header.Children.Add(titles);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            buttons.Children.Add(MakeButton("Open…", "PillButton", OpenFile));
            buttons.Children.Add(MakeButton("Save as…", "PillButton", SaveAs));
            buttons.Children.Add(MakeButton("Clear", "PillButton", () => { _box.Clear(); _box.Focus(); }));
            buttons.Children.Add(MakeButton("▶  Start prompter", "AccentButton", Start));
            Grid.SetColumn(buttons, 1);
            header.Children.Add(buttons);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new Grid { Margin = new Thickness(24, 10, 24, 14) };
            _stats = new TextBlock { Foreground = Muted, FontSize = 12.5 };
            footer.Children.Add(_stats);
            footer.Children.Add(new TextBlock
            {
                Text = "*word* highlights  ·  # line = section label  ·  Ctrl+Enter starts",
                Foreground = Faint,
                FontSize = 12.5,
                HorizontalAlignment = HorizontalAlignment.Right,
            });
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            _box = new TextBox
            {
                Text = script ?? "",
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                Foreground = Text,
                CaretBrush = Brushes.White,
                SelectionBrush = Solid(0x0A, 0x84, 0xFF),
                BorderThickness = new Thickness(0),
                FontSize = 17,
                Padding = new Thickness(14, 12, 14, 12),
                AllowDrop = true,
            };
            _box.ContextMenu = BuildContextMenu();
            root.Children.Add(new Border
            {
                Background = Solid(0x1F, 0x1F, 0x22),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(22, 0, 22, 0),
                Padding = new Thickness(4),
                Child = _box,
            });
            Content = root;

            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _debounce.Tick += (o, e) => Flush();
            _box.TextChanged += (o, e) =>
            {
                _dirty = true;
                UpdateStats();
                _debounce.Stop();
                _debounce.Start();
            };
            _box.PreviewDragOver += (o, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; }
            };
            _box.PreviewDrop += (o, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    LoadFile(files[0]);
                    e.Handled = true;
                }
            };

            PreviewKeyDown += (o, e) =>
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (ctrl && e.Key == Key.Enter) { Start(); e.Handled = true; }
                else if (ctrl && e.Key == Key.O) { OpenFile(); e.Handled = true; }
                else if (ctrl && e.Key == Key.S) { Flush(); e.Handled = true; }
            };
            SourceInitialized += (o, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                Native.UseDarkTitleBar(hwnd, 0x00181616);
                ApplyCaptureSetting();
            };
            Loaded += (o, e) => _box.Focus();
            Activated += (o, e) => UpdateStats();
            Closing += (o, e) => Flush();
            UpdateStats();
        }

        public void SetText(string text)
        {
            _box.Text = text ?? "";
            Flush();
        }

        public void ApplyCaptureSetting() =>
            CaptureGuard.Apply(new WindowInteropHelper(this).Handle, _hideFromCapture());

        void Flush()
        {
            _debounce.Stop();
            if (!_dirty) return;
            _dirty = false;
            ScriptChanged?.Invoke(_box.Text);
        }

        void Start()
        {
            _dirty = true;
            Flush();
            StartRequested?.Invoke();
        }

        void UpdateStats()
        {
            int words = ScriptFormatter.CountWords(_box.Text);
            double secs = words / Math.Max(1, _s.Wpm) * 60;
            _stats.Text = $"{words} words  ·  about {(int)(secs / 60)}:{(int)(secs % 60):00} at {_s.Wpm:0} wpm";
        }

        void OpenFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Text scripts (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == true) LoadFile(dlg.FileName);
        }

        void LoadFile(string path)
        {
            try { SetText(File.ReadAllText(path)); }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't open that file:\n" + ex.Message, "NotchPrompter"); }
        }

        void SaveAs()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "script.txt" };
            if (dlg.ShowDialog(this) != true) return;
            try { File.WriteAllText(dlg.FileName, _box.Text); }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save:\n" + ex.Message, "NotchPrompter"); }
        }

        ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut });
            menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy });
            menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Select all", Command = ApplicationCommands.SelectAll });
            // Menus are separate popup windows; hide them from capture too.
            menu.Opened += (o, e) =>
            {
                if (PresentationSource.FromVisual(menu) is HwndSource src) CaptureGuard.Apply(src.Handle, _hideFromCapture());
            };
            return menu;
        }

        static Button MakeButton(string label, string style, Action click)
        {
            var b = new Button { Content = label, Style = Theme.Get(style) };
            b.Click += (o, e) => click();
            return b;
        }

        internal static ImageSource LoadIcon()
        {
            try
            {
                var stream = typeof(EditorWindow).Assembly.GetManifestResourceStream("NotchPrompter.app.ico");
                return stream == null ? null : BitmapFrame.Create(stream);
            }
            catch
            {
                return null;
            }
        }

        static Brush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
