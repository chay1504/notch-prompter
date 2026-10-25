using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NotchPrompter
{
    internal enum IslandState { Collapsed, Peek, Expanded }

    /// <summary>
    /// The black "dynamic island" pinned to the top centre of the primary screen, right under the webcam.
    /// Collapsed it is a small notch, hovered it peeks, expanded it is a scrolling teleprompter.
    /// Everything is drawn in one transparent, topmost window that never takes focus and is excluded
    /// from screen capture. Transparent pixels are click-through, so only the black shape is "solid".
    /// </summary>
    internal sealed class IslandWindow : Window
    {
        public event Action EditRequested;
        public event Action SettingsChanged;

        // Island shapes, in device-independent pixels.
        const double CollapsedW = 184, CollapsedH = 28, CollapsedR = 12;
        const double PeekW = 300, PeekH = 38, PeekR = 17;
        const double ExpandedR = 30;
        // Transparent room around the island so spring overshoot is never clipped.
        const double SidePad = 48, BottomPad = 20;
        // Prompter text insets. The first line sits just below the top fade, as close to the lens as possible.
        const double PadX = 30, PadTop = 10, PadBottom = 8, TopFade = 16;
        public const double MinW = 440, MinH = 110;

        static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly Brush White = Solid(0xFF, 0xF5, 0xF5, 0xF7);
        static readonly Brush Dim = Solid(0xFF, 0x8E, 0x8E, 0x93);

        readonly Settings _s;
        bool _captureOverride;   // --capture-visible: show in recordings for this session (testing)
        IntPtr _hwnd;
        double _dpi = 1;          // device pixels per DIP

        readonly Spring _w = new Spring(CollapsedW, 230, 0.68);
        readonly Spring _h = new Spring(CollapsedH, 210, 0.78);
        readonly Spring _r = new Spring(CollapsedR, 260, 0.90);
        IslandState _state = IslandState.Collapsed;
        double _labelA, _contentA, _ctrlA, _statusA, _dotA, _cdA;
        bool _hover, _dragging, _cameraOn, _autoOpened, _peekByFlash, _ticking;
        TimeSpan _lastFrame;
        double _winW, _winH;
        double _lastGeoW = -1, _lastGeoH = -1, _lastGeoR = -1;

        string _script = "";
        int _words;
        double _textH, _offset, _pending, _countdown, _wheelAccum;
        bool _playing, _ended;
        string _flash;
        DateTime _flashUntil;

        Point _dragStart;
        double _dragW, _dragH;

        readonly Canvas _surface = new Canvas();
        readonly Path _shape = new Path { Fill = Brushes.Black };
        readonly Grid _notchLayer = new Grid { IsHitTestVisible = false };
        readonly Grid _prompter = new Grid { Background = Brushes.Transparent, IsHitTestVisible = false };
        readonly Canvas _viewport = new Canvas
        {
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(PadX, PadTop, PadX, PadBottom),
        };
        readonly TextBlock _text = new TextBlock();
        readonly TranslateTransform _textShift = new TranslateTransform();
        readonly ScaleTransform _cdScale = new ScaleTransform();
        readonly Ellipse _dot = new Ellipse { Width = 7, Height = 7, IsHitTestVisible = false, Fill = Solid(0xFF, 0x30, 0xD1, 0x58) };
        TextBlock _peekTitle, _peekHint, _status, _wpmLabel, _playGlyph, _cdText;
        Border _progressTrack, _progressFill, _dockShade, _grabber;
        FrameworkElement _controls;

        public IslandWindow(Settings settings, bool captureVisible)
        {
            _s = settings;
            _captureOverride = captureVisible;

            Title = "NotchPrompter";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            FontFamily = new FontFamily("Segoe UI");

            BuildVisuals();
            Content = _surface;

            SourceInitialized += OnSourceInitialized;
            MouseEnter += (o, e) => { _hover = true; if (_state == IslandState.Collapsed) SetState(IslandState.Peek); EnsureTicking(); };
            MouseLeave += (o, e) => { _hover = false; if (_state == IslandState.Peek && _flash == null) SetState(IslandState.Collapsed); EnsureTicking(); };
            MouseLeftButtonUp += OnClick;
            PreviewMouseWheel += OnWheel;

            UpdateWindowBounds();
            UpdateLabels();
            ApplyVisuals();

            // Other topmost windows can end up above us; quietly reclaim the top spot.
            var keeper = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            keeper.Tick += (o, e) => KeepOnTop();
            keeper.Start();
        }

        // ------------------------------------------------------------------ public API

        public bool IsExpanded => _state == IslandState.Expanded;
        public bool IsPlaying => _playing || _countdown > 0;
        public uint CaptureAffinity { get; private set; }

        public void Toggle()
        {
            if (IsExpanded) Collapse();
            else Expand();
        }

        public void Expand(bool automatic = false)
        {
            if (!IsVisible) Show();
            if (IsExpanded) return;
            _autoOpened = automatic;
            SetState(IslandState.Expanded);
        }

        public void Collapse()
        {
            Pause();
            _autoOpened = false;
            SetState(_hover ? IslandState.Peek : IslandState.Collapsed);
        }

        public void TogglePlay()
        {
            if (!IsExpanded) Expand();
            if (_countdown > 0) { _countdown = 0; UpdateLabels(); EnsureTicking(); return; }
            if (_playing) { Pause(); return; }
            if (_words == 0) { Flash("Script is empty  ·  Ctrl+Alt+E"); return; }
            if (_ended || _offset >= MaxOffset - 0.5) { _offset = 0; _ended = false; }
            if (_s.Countdown && _offset < 1) _countdown = 3;
            else _playing = true;
            UpdateLabels();
            EnsureTicking();
        }

        public void Pause()
        {
            if (!_playing && _countdown <= 0) return;
            _playing = false;
            _countdown = 0;
            UpdateLabels();
            SaveState();
            EnsureTicking();
        }

        public void Restart(bool quiet = false)
        {
            _offset = 0;
            _pending = 0;
            _countdown = 0;
            _playing = false;
            _ended = false;
            UpdateLabels();
            SaveState();
            if (!quiet) Flash("Back to the top");
            EnsureTicking();
        }

        /// <summary>Scrolls by a number of lines (negative = back), smoothly.</summary>
        public void Jump(double lines)
        {
            if (!IsExpanded) Expand();
            _pending += lines * LineH;
            EnsureTicking();
        }

        public void ChangeWpm(double delta)
        {
            _s.Wpm = Clamp(Math.Round((_s.Wpm + delta) / 5) * 5, 40, 400);
            Flash($"{_s.Wpm:0} words per minute");
            SettingsChanged?.Invoke();
        }

        public void ChangeFont(double delta)
        {
            _s.FontSize = Clamp(_s.FontSize + delta, 16, 72);
            RelayoutText(true);
            Flash($"Text size {_s.FontSize:0}");
            SettingsChanged?.Invoke();
        }

        public void ApplyPreset(double width, double height, double fontSize)
        {
            _s.Width = width;
            _s.Height = height;
            _s.FontSize = fontSize;
            if (IsExpanded) { _w.Target = width; _h.Target = height; }
            UpdateWindowBounds();
            LayoutStatic();
            RelayoutText(true);
            EnsureTicking();
            SettingsChanged?.Invoke();
        }

        public void RefreshLayout()
        {
            RelayoutText(true);
            ApplyVisuals();
        }

        public void SetScript(string text, bool restoreOffset = false)
        {
            _script = text ?? "";
            _words = ScriptFormatter.CountWords(_script);
            RelayoutText(false);
            if (restoreOffset) _offset = Clamp(_s.Offset, 0, MaxOffset);
            if (_offset < MaxOffset - 0.5) _ended = false;
            UpdateLabels();
            ApplyVisuals();
        }

        public void SaveState()
        {
            _s.Offset = Math.Round(_offset, 1);
            SettingsChanged?.Invoke();
        }

        public void SetCameraActive(bool on)
        {
            _cameraOn = on;
            if (on)
            {
                if (!IsVisible) Show();
                if (_s.AutoOpenOnCamera && !IsExpanded) Expand(automatic: true);
            }
            else if (_autoOpened && IsExpanded)
            {
                Collapse();
            }
            EnsureTicking();
        }

        public void ClearCaptureOverride() => _captureOverride = false;

        public void ApplyCaptureSetting()
        {
            CaptureAffinity = CaptureGuard.Apply(_hwnd, _s.ExcludeFromCapture && !_captureOverride);
        }

        public void ApplyIdleVisibility()
        {
            bool hide = _s.HideNotchWhenIdle && _state == IslandState.Collapsed && !_cameraOn && !_ticking;
            if (hide && IsVisible) Hide();
            else if (!hide && !IsVisible) Show();
        }

        /// <summary>A short message in the island, like the volume HUD on a phone.</summary>
        public void Flash(string message)
        {
            _flash = message;
            _flashUntil = DateTime.UtcNow.AddSeconds(1.4);
            if (_state == IslandState.Collapsed)
            {
                if (!IsVisible) Show();
                SetState(IslandState.Peek);
                _peekByFlash = true;
            }
            UpdateLabels();
            EnsureTicking();
        }

        public void Reposition()
        {
            Left = Math.Round((SystemParameters.PrimaryScreenWidth - _winW) / 2);
            Top = 0;
        }

        // ------------------------------------------------------------------ building the visuals

        void BuildVisuals()
        {
            _surface.Children.Add(_shape);

            // Peek label: what the notch says when you hover it (or when a setting changes).
            _peekTitle = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = White, VerticalAlignment = VerticalAlignment.Center };
            _peekHint = new TextBlock { FontSize = 11, Foreground = Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 1, 0, 0) };
            var peekRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            peekRow.Children.Add(new TextBlock { Text = "", FontFamily = IconFont, FontSize = 12, Foreground = White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 7, 0) });
            peekRow.Children.Add(_peekTitle);
            peekRow.Children.Add(_peekHint);
            _notchLayer.Children.Add(peekRow);
            _surface.Children.Add(_notchLayer);

            // Prompter text.
            _text.TextWrapping = TextWrapping.Wrap;
            _text.Foreground = White;
            _text.FontWeight = FontWeights.SemiBold;
            _text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            _text.RenderTransform = _textShift;
            TextOptions.SetTextFormattingMode(_text, TextFormattingMode.Ideal);
            TextOptions.SetTextHintingMode(_text, TextHintingMode.Animated);
            TextOptions.SetTextRenderingMode(_text, TextRenderingMode.Grayscale);
            _viewport.Children.Add(_text);
            _prompter.Children.Add(_viewport);

            _progressFill = new Border { Height = 2, CornerRadius = new CornerRadius(1), Background = Solid(0x8C, 0xFF, 0xFF, 0xFF), HorizontalAlignment = HorizontalAlignment.Left };
            _progressTrack = new Border
            {
                Height = 2,
                CornerRadius = new CornerRadius(1),
                Background = Solid(0x1F, 0xFF, 0xFF, 0xFF),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(ExpandedR + 12, 0, ExpandedR + 12, 6),
                Child = _progressFill,
                IsHitTestVisible = false,
            };
            _prompter.Children.Add(_progressTrack);

            var shade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            shade.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 0));
            shade.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0, 0, 0), 0.45));
            shade.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0, 0, 0), 1));
            shade.Freeze();
            _dockShade = new Border { Height = 66, VerticalAlignment = VerticalAlignment.Bottom, Background = shade, IsHitTestVisible = false };
            _prompter.Children.Add(_dockShade);

            _status = new TextBlock
            {
                FontSize = 11.5,
                Foreground = Solid(0xB3, 0xFF, 0xFF, 0xFF),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 15),
                IsHitTestVisible = false,
            };
            _prompter.Children.Add(_status);

            _controls = BuildControls();
            _prompter.Children.Add(_controls);

            // iOS-style grabber: drag up/down for height, sideways for width.
            _grabber = new Border
            {
                Width = 96,
                Height = 13,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeAll,
                Child = new Border { Width = 38, Height = 4, CornerRadius = new CornerRadius(2), Background = Solid(0x66, 0xFF, 0xFF, 0xFF), VerticalAlignment = VerticalAlignment.Center },
            };
            _grabber.MouseLeftButtonDown += OnGrabberDown;
            _grabber.MouseMove += OnGrabberMove;
            _grabber.MouseLeftButtonUp += OnGrabberUp;
            _prompter.Children.Add(_grabber);

            _cdText = new TextBlock
            {
                FontSize = 64,
                FontWeight = FontWeights.Bold,
                Foreground = White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _cdScale,
                IsHitTestVisible = false,
                Opacity = 0,
            };
            _prompter.Children.Add(_cdText);
            _surface.Children.Add(_prompter);

            _surface.Children.Add(_dot);
        }

        FrameworkElement BuildControls()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 14),
            };
            row.Children.Add(IconButton("", () => Restart()));
            row.Children.Add(new Border { Width = 8 });
            row.Children.Add(IconButton("", () => ChangeWpm(-10)));
            _wpmLabel = new TextBlock { Width = 112, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = White, FontSize = 12.5 };
            row.Children.Add(_wpmLabel);
            row.Children.Add(IconButton("", () => ChangeWpm(+10)));
            row.Children.Add(new Border { Width = 8 });
            _playGlyph = new TextBlock { Text = "" };
            var play = new Button { Style = Theme.Get("PlayButton"), Content = _playGlyph };
            play.Click += (o, e) => TogglePlay();
            row.Children.Add(play);
            row.Children.Add(new Border { Width = 8 });
            row.Children.Add(IconButton("", () => ChangeFont(-2)));
            row.Children.Add(IconButton("", () => ChangeFont(+2)));
            row.Children.Add(new Border { Width = 8 });
            row.Children.Add(IconButton("", () => EditRequested?.Invoke()));
            row.Children.Add(IconButton("", Collapse));
            row.Opacity = 0;
            row.IsHitTestVisible = false;
            return row;
        }

        static Button IconButton(string glyph, Action click)
        {
            var b = new Button { Style = Theme.Get("IconButton"), Content = glyph };
            b.Click += (o, e) => click();
            return b;
        }

        // ------------------------------------------------------------------ window plumbing

        void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            // Tool window: no taskbar button, no Alt+Tab. No-activate: clicking the island never steals
            // focus from the camera or recording app.
            int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
            _dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1;
            ApplyCaptureSetting();
        }

        void KeepOnTop()
        {
            if (_hwnd == IntPtr.Zero || !IsVisible || _dragging) return;
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }

        /// <summary>
        /// Sizes the window to the largest shape it currently needs: the full prompter while expanded or
        /// animating, only the peek pill once collapsed (so the idle window stays tiny).
        /// </summary>
        void UpdateWindowBounds()
        {
            bool tall = _state == IslandState.Expanded || _h.X > PeekH + 1;
            double w = Math.Ceiling((Math.Max(Math.Max(_s.Width, _w.X), PeekW) + 2 * SidePad) / 2) * 2;
            double h = Math.Ceiling((tall ? Math.Max(_s.Height, _h.X) : PeekH) + BottomPad);
            if (w != _winW || h != _winH)
            {
                _winW = w;
                _winH = h;
                Width = w;
                Height = h;
                LayoutStatic();
            }
            Reposition();
        }

        void LayoutStatic()
        {
            _surface.Width = _winW;
            _surface.Height = _winH;
            Canvas.SetLeft(_prompter, (_winW - _s.Width) / 2);
            Canvas.SetTop(_prompter, 0);
            _prompter.Width = _s.Width;
            _prompter.Height = _s.Height;
            _viewport.Width = _s.Width - 2 * PadX;
            _viewport.Height = _s.Height - PadTop - PadBottom;
            _viewport.OpacityMask = ReadingMask(_viewport.Height);
            Canvas.SetTop(_notchLayer, 0);
            _lastGeoW = -1;
        }

        /// <summary>Text fades out at the top edge and dims toward the bottom, pulling your eyes up to the lens.</summary>
        static Brush ReadingMask(double height)
        {
            var b = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, Math.Max(1, height)),
            };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), TopFade / Math.Max(1, height)));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0.5));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x6E, 0xFF, 0xFF, 0xFF), 1));
            b.Freeze();
            return b;
        }

        // ------------------------------------------------------------------ state + animation

        void SetState(IslandState state)
        {
            if (_state == state) return;
            _state = state;
            switch (state)
            {
                case IslandState.Expanded:
                    UpdateWindowBounds();
                    _w.Target = _s.Width; _h.Target = _s.Height; _r.Target = ExpandedR;
                    break;
                case IslandState.Peek:
                    _w.Target = PeekW; _h.Target = PeekH; _r.Target = PeekR;
                    break;
                default:
                    _w.Target = CollapsedW; _h.Target = CollapsedH; _r.Target = CollapsedR;
                    break;
            }
            _prompter.IsHitTestVisible = state == IslandState.Expanded;
            UpdateLabels();
            EnsureTicking();
        }

        void EnsureTicking()
        {
            if (_ticking) return;
            _ticking = true;
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnFrame;
        }

        void StopTicking()
        {
            _ticking = false;
            CompositionTarget.Rendering -= OnFrame;
            UpdateWindowBounds();
            ApplyIdleVisibility();
        }

        /// <summary>Runs once per display frame while anything is moving; unhooked when idle so the app costs ~0% CPU.</summary>
        void OnFrame(object sender, EventArgs e)
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == _lastFrame) return;
            double dt = _lastFrame == TimeSpan.Zero ? 1 / 60.0 : (now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            if (dt <= 0 || dt > 0.1) dt = 1 / 60.0;

            bool busy = false;
            int steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
            double sdt = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                busy |= _w.Step(sdt);
                busy |= _h.Step(sdt);
                busy |= _r.Step(sdt);
            }

            if (_flash != null && DateTime.UtcNow >= _flashUntil)
            {
                _flash = null;
                if (_peekByFlash && _state == IslandState.Peek && !_hover) SetState(IslandState.Collapsed);
                _peekByFlash = false;
                UpdateLabels();
            }
            busy |= _flash != null;

            bool expanded = _state == IslandState.Expanded;
            bool showControls = expanded && (_hover || _dragging);
            busy |= Approach(ref _labelA, _state == IslandState.Peek ? 1 : 0, dt, 18);
            busy |= Approach(ref _contentA, expanded ? 1 : 0, dt, expanded ? 9 : 24);
            busy |= Approach(ref _ctrlA, showControls ? 1 : 0, dt, 14);
            busy |= Approach(ref _statusA, expanded && !showControls && _status.Text.Length > 0 ? 1 : 0, dt, 12);
            busy |= Approach(ref _dotA, _cameraOn ? 1 : 0, dt, 10);
            busy |= Approach(ref _cdA, _countdown > 0 ? 1 : 0, dt, 16);
            busy |= StepPrompter(dt);

            ApplyVisuals();
            if (!busy) StopTicking();
        }

        bool StepPrompter(double dt)
        {
            bool busy = false;
            bool wasEnded = _ended;
            if (_countdown > 0)
            {
                busy = true;
                _countdown -= dt;
                if (_countdown <= 0) { _countdown = 0; _playing = true; UpdateLabels(); }
            }
            else if (_playing)
            {
                busy = true;
                _offset += Pps * dt;
                if (_offset >= MaxOffset)
                {
                    _offset = MaxOffset;
                    _playing = false;
                    _ended = true;
                    SaveState();
                }
            }
            if (_pending != 0)
            {
                busy = true;
                double step = Math.Abs(_pending) < 0.5 ? _pending : _pending * (1 - Math.Exp(-dt * 14));
                _pending -= step;
                double raw = _offset + step;
                _offset = Clamp(raw, 0, MaxOffset);
                if (_offset != raw) _pending = 0;   // ran into the start or end
                if (_offset < MaxOffset - 0.5) _ended = false;
                if (_pending == 0 && !_playing) SaveState();
            }
            if (wasEnded != _ended) UpdateLabels();
            return busy;
        }

        void ApplyVisuals()
        {
            double w = Math.Max(_w.X, 40), h = Math.Max(_h.X, 12);
            double r = Clamp(_r.X, 0, Math.Min(h - 1, w / 2));
            double p = Clamp((h - PeekH) / Math.Max(1, _s.Height - PeekH), 0, 1);   // 0 = pill, 1 = fully open
            double ear = Clamp(7 + 3 * p, 0, h - r);
            double cx = _winW / 2;

            if (w != _lastGeoW || h != _lastGeoH || r != _lastGeoR)
            {
                _lastGeoW = w; _lastGeoH = h; _lastGeoR = r;
                _shape.Data = IslandGeometry(cx, w, h, r, ear);
                _prompter.Clip = BodyGeometry((_s.Width - w) / 2, w, h, r);
                Canvas.SetLeft(_notchLayer, cx - w / 2);
                _notchLayer.Width = w;
                _notchLayer.Height = h;
            }
            _notchLayer.Opacity = _labelA * (1 - p);

            double show = _contentA * Smooth(p);
            _prompter.Opacity = show;
            _prompter.Visibility = show > 0.003 ? Visibility.Visible : Visibility.Hidden;
            if (_prompter.Visibility == Visibility.Visible)
            {
                // Snapped to whole device pixels: text stays crisp, and WPF only redraws (and copies the
                // layered window) when something actually moved a pixel instead of on every frame.
                _textShift.Y = SnapToPixel(TopFade - _offset);
                double max = MaxOffset;
                _progressFill.Width = SnapToPixel(Math.Max(0, (_s.Width - 2 * (ExpandedR + 12)) * (max > 0 ? _offset / max : 0)));
                _progressTrack.Opacity = 1 - _ctrlA;
                _controls.Opacity = _ctrlA;
                _controls.IsHitTestVisible = _ctrlA > 0.4;
                _grabber.Opacity = _ctrlA;
                _grabber.IsHitTestVisible = _ctrlA > 0.4 || _dragging;
                _status.Opacity = _statusA * (1 - _ctrlA);
                _dockShade.Opacity = Math.Max(_ctrlA, _statusA);
                _viewport.Opacity = 1 - 0.75 * _cdA;
                if (_ctrlA > 0.01) SetText(_wpmLabel, WpmText());
                ApplyCountdown();
            }

            // Green "camera is live" dot, like the privacy indicator on a phone.
            Canvas.SetLeft(_dot, cx + w / 2 - (15 + 6 * p) - 3.5);
            Canvas.SetTop(_dot, Math.Min(h / 2, 19) - 3.5);
            _dot.Opacity = _dotA;
        }

        void ApplyCountdown()
        {
            if (_countdown <= 0) { _cdText.Opacity = 0; return; }
            int n = (int)Math.Ceiling(_countdown);
            double t = 1 - (_countdown - (n - 1));   // 0 -> 1 within each second
            SetText(_cdText, n.ToString());
            _cdText.Opacity = Clamp(t * 6, 0, 1) * Clamp((1 - t) * 4, 0, 1);
            double scale = 1 + 0.35 * Math.Pow(1 - Clamp(t * 2.5, 0, 1), 3);
            _cdScale.ScaleX = _cdScale.ScaleY = scale;
        }

        void UpdateLabels()
        {
            SetText(_peekTitle, _flash ?? "Teleprompter");
            string hint = _flash != null ? "" : IsPlaying ? "Playing" : "Ctrl+Alt+T";
            SetText(_peekHint, hint);
            _peekHint.Visibility = hint.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            SetText(_playGlyph, IsPlaying ? "" : "");
            string status = _flash
                ?? (_countdown > 0 ? ""
                : _ended ? "End of script  ·  Ctrl+Alt+R to restart"
                : !_playing ? "Paused  ·  click the text or press Ctrl+Alt+Space"
                : "");
            SetText(_status, status);
            SetText(_wpmLabel, WpmText());
        }

        string WpmText()
        {
            string s = $"{_s.Wpm:0} wpm";
            double pps = Pps;
            if (pps > 0)
            {
                double secs = Math.Max(0, (MaxOffset - _offset) / pps);
                s += $"  ·  {(int)(secs / 60)}:{(int)(secs % 60):00}";
            }
            return s;
        }

        // ------------------------------------------------------------------ prompter text

        double LineH => Math.Round(_s.FontSize * 1.3);
        double MaxOffset => Math.Max(0, _textH - LineH);

        /// <summary>
        /// Scroll speed in DIPs per second, derived from words per minute so the pace stays the same
        /// when you change the text size or island width.
        /// </summary>
        double Pps => _words > 0 ? _s.Wpm / 60.0 * _textH / _words : 0;

        void RelayoutText(bool keepProgress)
        {
            double progress = MaxOffset > 0 ? _offset / MaxOffset : 0;
            double width = Math.Max(50, _s.Width - 2 * PadX);
            _text.Width = width;
            _text.FontSize = _s.FontSize;
            _text.LineHeight = LineH;
            _text.TextAlignment = _s.CenterText ? TextAlignment.Center : TextAlignment.Left;
            if (_script.Trim().Length == 0)
            {
                _text.Inlines.Clear();
                _text.Inlines.Add(new Run("Your script is empty."));
                _text.Inlines.Add(new LineBreak());
                _text.Inlines.Add(new Run("Press Ctrl+Alt+E to write or paste one.") { Foreground = Dim });
            }
            else
            {
                ScriptFormatter.Fill(_text, _script);
            }
            _text.Measure(new Size(width, double.PositiveInfinity));
            _textH = _text.DesiredSize.Height;
            if (keepProgress) _offset = progress * MaxOffset;
            _offset = Clamp(_offset, 0, MaxOffset);
            UpdateLabels();
            ApplyVisuals();
        }

        // ------------------------------------------------------------------ input

        void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (!IsExpanded) Expand();
            else TogglePlay();
        }

        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (!IsExpanded) return;
            bool ctrl = Native.IsKeyDown(Native.VK_CONTROL), shift = Native.IsKeyDown(Native.VK_SHIFT);
            if (ctrl || shift)
            {
                // Touchpads send many small deltas; act once per full wheel notch.
                _wheelAccum += e.Delta;
                if (Math.Abs(_wheelAccum) < 120) return;
                int dir = Math.Sign(_wheelAccum);
                _wheelAccum = 0;
                if (ctrl) ChangeFont(dir * 2);
                else ChangeWpm(dir * 5);
                return;
            }
            _pending -= e.Delta / 120.0 * LineH;
            EnsureTicking();
        }

        void OnGrabberDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _dragStart = ScreenDip(e);
            _dragW = _s.Width;
            _dragH = _s.Height;
            _grabber.CaptureMouse();
            e.Handled = true;
            EnsureTicking();
        }

        void OnGrabberMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var p = ScreenDip(e);
            double maxW = Math.Min(1280, SystemParameters.PrimaryScreenWidth - 2 * SidePad);
            double maxH = Math.Min(640, SystemParameters.PrimaryScreenHeight * 0.75);
            // Width grows on both sides because the island stays centred under the camera.
            _s.Width = Math.Round(Clamp(_dragW + 2 * (p.X - _dragStart.X), MinW, maxW));
            _s.Height = Math.Round(Clamp(_dragH + (p.Y - _dragStart.Y), MinH, maxH));
            _w.Snap(_s.Width);
            _h.Snap(_s.Height);
            UpdateWindowBounds();
            LayoutStatic();
            RelayoutText(true);
        }

        void OnGrabberUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            _grabber.ReleaseMouseCapture();
            e.Handled = true;
            SettingsChanged?.Invoke();
            EnsureTicking();
        }

        Point ScreenDip(MouseEventArgs e)
        {
            var px = PointToScreen(e.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            return source?.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice.Transform(px) : px;
        }

        // ------------------------------------------------------------------ geometry + helpers

        /// <summary>
        /// The notch outline: flush with the top of the screen, rounded at the bottom, with small concave
        /// "ears" where it meets the top edge, like the MacBook notch.
        /// </summary>
        static Geometry IslandGeometry(double cx, double w, double h, double r, double ear)
        {
            double l = cx - w / 2, rt = cx + w / 2;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(l - ear, 0), true, true);
                c.ArcTo(new Point(l, ear), new Size(ear, ear), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(l, h - r), true, false);
                c.ArcTo(new Point(l + r, h), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                c.LineTo(new Point(rt - r, h), true, false);
                c.ArcTo(new Point(rt, h - r), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                c.LineTo(new Point(rt, ear), true, false);
                c.ArcTo(new Point(rt + ear, 0), new Size(ear, ear), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }

        /// <summary>The island body without ears, used to clip the prompter while it morphs.</summary>
        static Geometry BodyGeometry(double x, double w, double h, double r)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(x, -1), true, true);
                c.LineTo(new Point(x, h - r), false, false);
                c.ArcTo(new Point(x + r, h), new Size(r, r), 0, false, SweepDirection.Counterclockwise, false, false);
                c.LineTo(new Point(x + w - r, h), false, false);
                c.ArcTo(new Point(x + w, h - r), new Size(r, r), 0, false, SweepDirection.Counterclockwise, false, false);
                c.LineTo(new Point(x + w, -1), false, false);
            }
            g.Freeze();
            return g;
        }

        static bool Approach(ref double value, double target, double dt, double rate)
        {
            if (Math.Abs(value - target) < 0.002) { value = target; return false; }
            value += (target - value) * (1 - Math.Exp(-rate * dt));
            return true;
        }

        static void SetText(TextBlock tb, string text)
        {
            if (tb.Text != text) tb.Text = text;
        }

        double SnapToPixel(double dip) => Math.Round(dip * _dpi) / _dpi;
        static double Smooth(double t) => t * t * (3 - 2 * t);
        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        static Brush Solid(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>Damped spring; slightly under-damped so the island has a little bounce.</summary>
        sealed class Spring
        {
            public double X, V, Target;
            readonly double _k, _c;

            public Spring(double x, double stiffness, double damping)
            {
                X = Target = x;
                _k = stiffness;
                _c = 2 * damping * Math.Sqrt(stiffness);
            }

            public bool Step(double dt)
            {
                if (X == Target && V == 0) return false;
                V += (-_k * (X - Target) - _c * V) * dt;
                X += V * dt;
                if (Math.Abs(X - Target) < 0.05 && Math.Abs(V) < 0.6) { X = Target; V = 0; return false; }
                return true;
            }

            public void Snap(double x)
            {
                X = Target = x;
                V = 0;
            }
        }
    }
}
