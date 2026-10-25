# NotchPrompter for Mac (beta)

Native AppKit menu bar app for macOS 13+, Apple Silicon and Intel. No Electron runtime, account, server, or upload. The panel sits below the physical notch, or at top center on a display without a notch. It stays on the primary display and can join Spaces and compatible full-screen apps.

## Install

If macOS blocks this downloaded beta, read [the installation diagnosis and local-build option](INSTALL.md). The `.app` format alone does not establish trust. The publisher has chosen not to join Apple's Developer Program, so this is not a notarized download. The [Chrome extension](../chrome-extension/README.md) is another option for browser use.

Download the universal ZIP from [Releases](https://github.com/chay1504/notch-prompter/releases), unzip, drag **NotchPrompter.app** into Applications, then open it. The menu bar speech-bubble icon contains the editor, preferences and Quit. This beta is ad-hoc signed, not notarized: follow [Apple's unidentified-developer guidance](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac) if macOS blocks opening. Do not disable Gatekeeper system-wide. Start at login is optional and may need approval in System Settings → General → Login Items.

## Read and edit

Hover to peek; click to open. Click script text to play/pause. The first play counts down 3–2–1. Scroll to seek, Control-scroll for text size, Shift-scroll for speed. Drag the bottom edge to resize. Hover an open island for restart, speed, play, edit and collapse controls. `*words*` highlight in yellow; `# Heading` creates a section label. The editor accepts pasted scripts and UTF-8 `.txt` files.

| Control + Option + | Action |
| --- | --- |
| Space | Play / pause |
| T | Show / collapse |
| E | Edit script |
| R | Restart |
| = / − | Faster / slower |
| ] / [ | Bigger / smaller text |
| PageUp / PageDown | Seek |

The app registers shortcuts without requesting Accessibility permission. Shortcut conflicts are reported at launch. On laptops, Fn + Up/Down are usually PageUp/PageDown. The editor intentionally takes keyboard focus; the island does not.

Script and settings save locally to `~/Library/Application Support/NotchPrompter/script.txt` and `settings.json`. Writes use atomic file replacement. Playback position saves when pausing or quitting. The app reads AVFoundation device-use status every 1.2 seconds to detect other apps using the camera; it never starts a capture session or asks for camera access. Detection may vary by camera/driver, so automatic opening can be turned off.

## Screen recording limitation

The Mac version **does not promise capture exclusion**. Apple's [`NSWindow.SharingType.none` documentation](https://developer.apple.com/documentation/appkit/nswindow/sharingtype-swift.enum/none) says not to use that legacy value to omit content from capture. This app does not use private capture APIs or describe its overlay as invisible. Share a particular app window instead of the display, or record webcam-only, and test your setup before a take. The Windows app uses a separate Windows capture-affinity API.

## Build and check (on Mac)

Requires Xcode Command Line Tools with Swift 5.9+ and a macOS 13+ SDK.

```sh
cd macos
swift test
swift build
.build/debug/NotchPrompter --smoke-test
bash build.sh
```

Output: `dist/NotchPrompter.app` and `dist/NotchPrompter-macOS-universal.zip`. The build combines arm64 and x86_64 executables, makes an icon, applies an ad-hoc signature, verifies both architectures and the signature, then zips the `.app` preserving macOS metadata. Developer ID signing/notarization needs the publisher's own Apple credentials and is not configured.

## Real Mac acceptance review

CI exercises windows, editor construction, scrolling timer, controls and local saving. Before promoting this beta to stable, review a physical notched MacBook and an Intel/non-notched Mac: camera start/stop, global shortcuts while a camera app has focus, notch placement, full-screen/Spaces, display reconnects, editor input in Telugu, login approval, and downloaded-app Gatekeeper behavior. Make an actual recording/share test in each app you intend to use.
