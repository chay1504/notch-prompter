using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace NotchPrompter
{
    /// <summary>
    /// User preferences, stored as key=value lines in %APPDATA%\NotchPrompter\settings.ini.
    /// Every public instance field is persisted automatically.
    /// </summary>
    internal sealed class Settings
    {
        public static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotchPrompter");
        public static string FilePath => Path.Combine(Dir, "settings.ini");
        public static string ScriptPath => Path.Combine(Dir, "script.txt");
        public static string LogPath => Path.Combine(Dir, "log.txt");

        public double Wpm = 140;          // reading pace; scroll speed is derived from it
        public double FontSize = 28;
        public double Width = 620;        // expanded island size, in DIPs
        public double Height = 200;
        public double Offset = 0;         // where you left off in the script
        public bool Countdown = true;
        public bool AutoOpenOnCamera = true;
        public bool ExcludeFromCapture = true;
        public bool HideNotchWhenIdle = false;
        public bool CenterText = true;
        public bool StartWithWindows = true;
        public bool FirstRunDone = false;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        var field = typeof(Settings).GetField(line.Substring(0, eq).Trim(), BindingFlags.Public | BindingFlags.Instance);
                        if (field == null) continue;
                        string value = line.Substring(eq + 1).Trim();
                        if (field.FieldType == typeof(bool) && bool.TryParse(value, out var b))
                            field.SetValue(s, b);
                        else if (field.FieldType == typeof(double) &&
                                 double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            field.SetValue(s, d);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("settings load failed: " + ex.Message);
            }
            s.Wpm = Clamp(s.Wpm, 40, 400);
            s.FontSize = Clamp(s.FontSize, 16, 72);
            s.Width = Clamp(s.Width, IslandWindow.MinW, 1400);
            s.Height = Clamp(s.Height, IslandWindow.MinH, 700);
            s.Offset = Math.Max(0, s.Offset);
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                foreach (var field in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    sb.Append(field.Name).Append('=')
                      .Append(Convert.ToString(field.GetValue(this), CultureInfo.InvariantCulture)).Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Write("settings save failed: " + ex.Message);
            }
        }

        static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>The script lives in a plain UTF-8 text file next to the settings.</summary>
    internal static class ScriptStore
    {
        public static string Load()
        {
            try
            {
                if (File.Exists(Settings.ScriptPath)) return File.ReadAllText(Settings.ScriptPath);
            }
            catch (Exception ex)
            {
                Log.Write("script load failed: " + ex.Message);
            }
            return DefaultScript;
        }

        public static void Save(string text)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.WriteAllText(Settings.ScriptPath, text ?? "", new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Write("script save failed: " + ex.Message);
            }
        }

        public const string DefaultScript =
"""
# Welcome
Hi! I'm your teleprompter, living right under the webcam.
Keep your eyes up here and you'll be looking straight into the camera.

# Controls
Press *Ctrl + Alt + Space* to play or pause.
*Ctrl + Alt + =* and *Ctrl + Alt + -* change the reading speed.
*Ctrl + Alt + E* opens the editor, so you can paste your reel script.

# Recording
Screen recorders and screen sharing can't see me, so I never end up in your videos.
When your camera turns on, I open by myself.

Wrap words in asterisks to *highlight* them, and start a line with # to add a section label.
Have a great shoot!
""";
    }

    /// <summary>Starts the app at sign-in through the per-user Run key (no admin rights needed).</summary>
    internal static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "NotchPrompter";

        public static string ExePath => Assembly.GetEntryAssembly().Location;

        public static void Set(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled) key.SetValue(ValueName, "\"" + ExePath + "\" --autostart");
                else key.DeleteValue(ValueName, false);
            }
            catch (Exception ex)
            {
                Log.Write("autostart update failed: " + ex.Message);
            }
        }
    }

    internal static class Log
    {
        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.AppendAllText(Settings.LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n");
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
