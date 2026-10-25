using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NotchPrompter
{
    /// <summary>System-wide shortcuts via RegisterHotKey on a hidden message-only window.</summary>
    internal sealed class HotkeyManager : IDisposable
    {
        readonly HwndSource _source;
        readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        int _nextId = 0x5A00;

        public List<string> Failed { get; } = new List<string>();

        public HotkeyManager()
        {
            var p = new HwndSourceParameters("NotchPrompterHotkeys") { ParentWindow = Native.HWND_MESSAGE, WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        /// <param name="repeat">Fire repeatedly while the keys are held (used for speed and size).</param>
        public void Register(string label, uint modifiers, Key key, Action action, bool repeat = false)
        {
            int id = _nextId++;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (!repeat) modifiers |= Native.MOD_NOREPEAT;
            if (Native.RegisterHotKey(_source.Handle, id, modifiers, vk)) _actions[id] = action;
            else Failed.Add(label);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
            {
                handled = true;
                try { action(); }
                catch (Exception ex) { Log.Write("hotkey action failed: " + ex); }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            foreach (var id in _actions.Keys) Native.UnregisterHotKey(_source.Handle, id);
            _actions.Clear();
            _source.Dispose();
        }
    }

    /// <summary>
    /// Detects when any app is using the webcam. Windows records camera use per app under the
    /// CapabilityAccessManager consent store; a LastUsedTimeStop of 0 means "in use right now".
    /// </summary>
    internal sealed class CameraWatcher : IDisposable
    {
        const string ConsentStore = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";
        readonly DispatcherTimer _timer;

        public bool IsActive { get; private set; }
        public event Action<bool> Changed;

        public CameraWatcher()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1.2) };
            _timer.Tick += (o, e) => Poll();
        }

        public void Start()
        {
            Poll();
            _timer.Start();
        }

        void Poll()
        {
            bool active = false;
            try { active = AnyInUse(Registry.CurrentUser) || AnyInUse(Registry.LocalMachine); }
            catch (Exception ex) { Log.Write("camera poll failed: " + ex.Message); }
            if (active == IsActive) return;
            IsActive = active;
            Changed?.Invoke(active);
        }

        static bool AnyInUse(RegistryKey hive)
        {
            using var root = hive.OpenSubKey(ConsentStore);
            if (root == null) return false;
            foreach (var name in root.GetSubKeyNames())
            {
                using var app = root.OpenSubKey(name);
                if (app == null) continue;
                if (InUse(app)) return true;
                if (name != "NonPackaged") continue;
                // Desktop apps (OBS, Chrome, Zoom...) are listed one level deeper, keyed by exe path.
                foreach (var exe in app.GetSubKeyNames())
                {
                    using var sub = app.OpenSubKey(exe);
                    if (sub != null && InUse(sub)) return true;
                }
            }
            return false;
        }

        static bool InUse(RegistryKey key)
        {
            object start = key.GetValue("LastUsedTimeStart"), stop = key.GetValue("LastUsedTimeStop");
            return start != null && stop != null && Convert.ToInt64(start) != 0 && Convert.ToInt64(stop) == 0;
        }

        public void Dispose() => _timer.Stop();
    }

    /// <summary>
    /// Turns script text into styled runs: "*word*" is highlighted and lines starting with "#" become
    /// small section labels (handy for "# Hook", "# CTA").
    /// </summary>
    internal static class ScriptFormatter
    {
        static readonly Regex Word = new Regex(@"[^\s*]+", RegexOptions.Compiled);
        static readonly Brush Highlight = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x0A)));
        static readonly Brush Heading = Freeze(new SolidColorBrush(Color.FromRgb(0x64, 0xD2, 0xFF)));

        public static int CountWords(string text)
        {
            int n = 0;
            foreach (var line in Lines(text))
            {
                if (line.TrimStart().StartsWith("#")) continue;
                n += Word.Matches(line).Count;
            }
            return n;
        }

        public static void Fill(TextBlock target, string text)
        {
            var inlines = target.Inlines;
            inlines.Clear();
            bool first = true;
            foreach (var line in Lines(text))
            {
                if (!first) inlines.Add(new LineBreak());
                first = false;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#"))
                {
                    inlines.Add(new Run(trimmed.TrimStart('#').Trim().ToUpperInvariant())
                    {
                        Foreground = Heading,
                        FontSize = Math.Max(11, target.FontSize * 0.5),
                        FontWeight = FontWeights.Bold,
                    });
                    continue;
                }
                bool on = false;
                int start = 0;
                for (int i = 0; i <= line.Length; i++)
                {
                    if (i < line.Length && line[i] != '*') continue;
                    if (i > start)
                    {
                        var run = new Run(line.Substring(start, i - start));
                        if (on) { run.Foreground = Highlight; run.FontWeight = FontWeights.Bold; }
                        inlines.Add(run);
                    }
                    on = !on;
                    start = i + 1;
                }
            }
        }

        static IEnumerable<string> Lines(string text) =>
            (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        static Brush Freeze(Brush b) { b.Freeze(); return b; }
    }

    /// <summary>Shared dark styles, parsed from XAML at startup so no XAML compilation is needed.</summary>
    internal static class Theme
    {
        public static void Install()
        {
            var dict = (ResourceDictionary)XamlReader.Parse(Xaml);
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        public static Style Get(string key) => (Style)Application.Current.FindResource(key);

        const string Xaml =
"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <Style x:Key="IconButton" TargetType="Button">
    <Setter Property="Foreground" Value="#EBFFFFFF"/>
    <Setter Property="Background" Value="#00FFFFFF"/>
    <Setter Property="FontFamily" Value="Segoe Fluent Icons, Segoe MDL2 Assets"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Width" Value="34"/>
    <Setter Property="Height" Value="30"/>
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border Background="{TemplateBinding Background}" CornerRadius="15">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#2EFFFFFF"/></Trigger>
      <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#4DFFFFFF"/></Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key="PlayButton" TargetType="Button">
    <Setter Property="Foreground" Value="Black"/>
    <Setter Property="Background" Value="White"/>
    <Setter Property="FontFamily" Value="Segoe Fluent Icons, Segoe MDL2 Assets"/>
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="Width" Value="36"/>
    <Setter Property="Height" Value="36"/>
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border Background="{TemplateBinding Background}" CornerRadius="18">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#E5E5EA"/></Trigger>
      <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#C7C7CC"/></Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key="PillButton" TargetType="Button">
    <Setter Property="Foreground" Value="#F2F2F7"/>
    <Setter Property="Background" Value="#2C2C2E"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Padding" Value="14,7"/>
    <Setter Property="Margin" Value="8,0,0,0"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#3A3A3C"/></Trigger>
      <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#48484A"/></Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource PillButton}">
    <Setter Property="Background" Value="#0A84FF"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#3395FF"/></Trigger>
      <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#0070E0"/></Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType="ScrollBar">
    <Setter Property="Width" Value="10"/>
    <Setter Property="MinWidth" Value="10"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="{TemplateBinding Orientation}">
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType="Thumb">
                    <Border CornerRadius="3" Background="#4DFFFFFF" Margin="2,4"/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
""";
    }
}
