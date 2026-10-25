# NotchPrompter

A MacBook-style "Dynamic Island" for Windows that works as a teleprompter. It sits at the top centre
of your screen, right under the webcam, so you look into the lens while you read your reel script.
It requests exclusion from supported Windows screen-capture APIs. Test your recording setup before use.

- Single file: `dist\NotchPrompter.exe` (~180 KB). It runs on .NET Framework 4.8, which comes with
  Windows 10 (1903+) and 11, so there is nothing else to install.
- Starts automatically every time you sign in.
- Uses ~0% CPU when idle and about 9% of one core while scrolling.

## Install / uninstall

```powershell
.\install.ps1      # copies to %LOCALAPPDATA%\Programs\NotchPrompter, adds Start menu + auto-start, launches
.\uninstall.ps1    # removes it (keeps your script; add -RemoveSettings to delete that too)
.\build.ps1        # rebuilds dist\NotchPrompter.exe (needs the .NET SDK)
```

No admin rights are needed. Auto-start uses the per-user `Run` registry key, so it also appears
(and can be switched off) under Settings > Apps > Startup.

## Using it

| | |
|---|---|
| **Hover the notch** | It peeks open |
| **Click the notch** | Opens the teleprompter |
| **Click the text** | Play / pause (the first play counts down 3-2-1) |
| **Scroll** | Move through the script by hand |
| **Ctrl + scroll / Shift + scroll** | Text size / reading speed |
| **Drag the handle at the bottom** | Resize: up/down for height, sideways for width |
| **Tray icon** | Left-click to show or hide it; right-click for settings |

When your camera turns on (Camera app, OBS, Zoom, a browser...) the island opens by itself and shows
a green dot. When the camera turns off, it closes again.

### Shortcuts (work from any app)

| Keys | Action |
|---|---|
| `Ctrl+Alt+T` | Show / hide the teleprompter |
| `Ctrl+Alt+Space` | Play / pause |
| `Ctrl+Alt+R` | Back to the top |
| `Ctrl+Alt+=` / `Ctrl+Alt+-` | Faster / slower (in words per minute; hold to repeat) |
| `Ctrl+Alt+]` / `Ctrl+Alt+[` | Bigger / smaller text |
| `Ctrl+Alt+PgUp` / `PgDn` | Jump back / forward a few lines |
| `Ctrl+Alt+E` | Open the script editor |

### Writing scripts

`Ctrl+Alt+E` opens the editor. Whatever you type shows up in the island straight away and is saved to
`%APPDATA%\NotchPrompter\script.txt`. You can also open or drop in a `.txt` file.

- `*word*` highlights words in yellow (good for emphasis or cues)
- A line starting with `#` becomes a small section label, for example `# Hook` or `# CTA`
- Speed is set in **words per minute**. The scroll rate is worked out from your script, so changing
  the text size or width doesn't change your pace. The editor shows the estimated read time.

## How the "invisible to recording" part works

Each window calls `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`, which Windows 10 (2004+)
and 11 support. The compositor then leaves the window out of every capture API: GDI/BitBlt
(PrintScreen, older recorders), DXGI Desktop Duplication (OBS display capture) and Windows.Graphics.Capture
(Snipping Tool, Xbox Game Bar, Teams/Zoom/Meet screen sharing, browser screen sharing). You still see it
on your own monitor.

I tested this on this laptop. With the island open on screen, both a GDI screenshot and an ffmpeg
`ddagrab` (Desktop Duplication) capture showed only what was behind it. With the `--capture-visible`
test flag, both showed the island.

What it can't stop: a phone or camera pointed at the screen, or a hardware capture card on an HDMI output.
The editor window and the tray menu are hidden from capture as well. Untick "Hide from screen recording
and sharing" in the tray menu if you ever want the island to show up in a recording.

## Architecture

```
NotchPrompter.exe  (WPF + a little WinForms for the tray, .NET Framework 4.8)
│
├─ Program            single instance (named mutex); a second launch signals the running copy
│                     (--quit asks it to exit, anything else asks it to open)
├─ AppController      wires everything together, owns the tray menu and settings saving
├─ IslandWindow       the island: one transparent, topmost, no-activate, capture-excluded window
│   ├─ shape          notch outline with concave "ears", rebuilt per frame while it morphs
│   ├─ springs        under-damped springs give the Dynamic-Island bounce (collapsed → peek → open)
│   ├─ prompter       clipped text viewport with a reading fade, a whole-pixel scroll driven by WPM,
│   │                 countdown, progress bar, and hover controls
│   └─ frame loop     hooked to CompositionTarget.Rendering only while something moves
├─ EditorWindow       dark script editor with live, debounced updates
├─ HotkeyManager      RegisterHotKey on a message-only window
├─ CameraWatcher      polls the CapabilityAccessManager webcam consent store (LastUsedTimeStop == 0
│                     means some app is using the camera right now)
├─ CaptureGuard       SetWindowDisplayAffinity, falling back to WDA_MONITOR on old Windows builds
└─ Settings/AutoStart %APPDATA%\NotchPrompter\settings.ini + script.txt; HKCU\...\Run for auto-start
```

Design notes:
- **Click-through where it's transparent.** The window is a per-pixel-alpha layered window, so
  only the black island takes clicks. The empty space around it passes clicks to whatever is underneath.
- **Never steals focus.** `WS_EX_NOACTIVATE` means clicking play doesn't pull focus from your camera
  or recording app. `WS_EX_TOOLWINDOW` keeps it out of the taskbar and Alt+Tab.
- **No popups on the island.** Tooltips and WPF popups are separate windows that would show up in
  recordings, so the island deliberately has none.

## Troubleshooting

- **A shortcut doesn't work.** Another app has already taken it, and a notification on start-up says
  which one. The on-island buttons and tray menu still work.
- **Antivirus.** The app is unsigned. Quick Heal and McAfee are installed on this PC and haven't flagged
  the app itself. If one ever quarantines it, add `%LOCALAPPDATA%\Programs\NotchPrompter` as an exclusion.
- **Wrong screen.** The island always goes on the *primary* display. If you use an external monitor as
  your main screen, make the laptop screen primary while recording.
- Errors, if any, are logged to `%APPDATA%\NotchPrompter\log.txt`.
