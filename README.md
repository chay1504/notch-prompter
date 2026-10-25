# NotchPrompter

A free, local teleprompter under your webcam so you can read a script while looking towards the lens. Windows app plus a native macOS beta, landing page and original Windows demo sources.

## Downloads

Get desktop builds and SHA-256 checksums from [GitHub Releases](https://github.com/chay1504/notch-prompter/releases).

| Platform | Download | Requirements |
| --- | --- | --- |
| Windows | `NotchPrompter.exe`, or ZIP with install/uninstall scripts | Windows 10 2004+ or Windows 11; included .NET Framework 4.8 |
| Mac beta | `NotchPrompter-macOS-universal.zip` containing `NotchPrompter.app` | macOS 13+, Apple Silicon or Intel |

**Mac recording limitation:** the overlay may appear in screen recordings and display sharing. Apple's public APIs do not reliably guarantee exclusion. Share a specific app window or record webcam-only, and test your recorder. [Apple documentation](https://developer.apple.com/documentation/appkit/nswindow/sharingtype-swift.enum/none).

Both builds currently lack a publisher certificate. Windows may show SmartScreen. The Mac app is ad-hoc signed, not Developer ID signed or notarized, so macOS may block initial opening. See [Apple's opening guidance](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac) if you trust the source.

## Use

Hover the notch to peek; click it to open. Paste your script in the editor. Click the script to play/pause, adjust words per minute, and resize from the bottom edge. `*emphasis*` highlights text; `# Section` creates a heading. Scripts stay on your computer.

| Action | Windows | Mac |
| --- | --- | --- |
| Edit script | Ctrl + Alt + E | Control + Option + E |
| Play / pause | Ctrl + Alt + Space | Control + Option + Space |
| Show / collapse | Ctrl + Alt + T | Control + Option + T |
| Restart | Ctrl + Alt + R | Control + Option + R |

[Windows guide](WINDOWS.md) · [Mac guide and manual review checklist](macos/README.md)

## Repository

```text
src/, assets/           Windows WPF app (.NET Framework 4.8)
build.ps1               Build Windows dist/NotchPrompter.exe
install.ps1             Optional per-user Windows installation
macos/                  Native Swift/AppKit app, core tests, universal bundle script
website/                Static landing page, downloadable Windows exe, 45-second Windows demo
video/                  Original Hyperframes demo sources and sound effects
.github/workflows/      Windows + Mac + website checks; tagged beta releases
tools/check-website.py  Resource, checksum and Mac-disclosure checks
```

Windows build: run `./build.ps1` in PowerShell with the .NET SDK installed.

Mac build: on a Mac with Swift 5.9+/Xcode Command Line Tools, run `cd macos`, `swift test`, then `bash build.sh`. The build creates a `.app` and ZIP containing both CPU architectures. It does not require an Apple Developer account for the ad-hoc build. A notarized version does require the publisher's Apple signing credentials.

Website preview: `python -m http.server 8643 --bind 127.0.0.1 --directory website`, then visit `http://127.0.0.1:8643`. For Vercel, select **website** as the project root; there is no build command. The Mac link points to the versioned GitHub release. The existing demo shows the Windows version and its capture exclusion, not verified Mac behavior.

Pushes and pull requests run build checks. A `v*` tag publishes a prerelease only after both desktop builds, Mac core tests, native AppKit smoke test and website checks succeed. The initial release is `v1.0.0-beta.1`.

CI verifies compilation and automated behaviors, not the experience on a physical Mac. Camera detection, physical-notch layout, Intel runtime behavior, screen sharing, login startup, full-screen Spaces and Gatekeeper need real-device acceptance testing before a stable Mac release.
