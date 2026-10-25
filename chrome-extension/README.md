# NotchPrompter for Chrome

Local Manifest V3 extension for desktop Chrome on Mac, Windows and Linux. It displays a teleprompter at the top of the web page you choose. This is an unpacked developer-mode release, not a Chrome Web Store listing, and it does not install a native Mac app.

## Install

1. Download `NotchPrompter-Chrome.zip` from the [website](https://notch-prompter-zeta.vercel.app/#extension).
2. Extract the ZIP and keep the `NotchPrompter-Chrome` folder somewhere permanent, such as Documents. Do not load the ZIP itself or delete the extracted folder afterwards.
3. Enter `chrome://extensions` in Chrome's address bar.
4. Turn **Developer mode** on, click **Load unpacked**, and select the folder containing `manifest.json`.
5. Pin NotchPrompter using Chrome's puzzle-piece Extensions menu. Open a normal website, click NotchPrompter, paste your script, and choose **Show on this tab**.

Chrome can display developer-mode notices. A managed work/school browser may prohibit unpacked extensions; an administrator would need to allow installation. No Apple Developer account or macOS app installer is needed for the extension. [Google's official installation instructions](https://developer.chrome.com/docs/extensions/get-started/tutorial/hello-world#load-unpacked).

## Use

- Click the extension icon to edit a script. Edits and preferences save on this computer in `chrome.storage.local`; they are not synced or sent to a server.
- **Full editor** opens more writing space, still controlling the original website tab. Return to that tab to read.
- `*words*` highlight in yellow; `# Heading` creates a small cyan cue. Unicode scripts, including Telugu, work with the system's available fonts.
- **Show on this tab** places the overlay at top center, below Chrome's toolbar. Click the script to play/pause. The first play counts down 3–2–1 if enabled. It automatically pauses when the tab goes into the background.
- Scroll over the island to seek. Control-scroll changes font size; Shift-scroll changes speed. Drag the bottom handle to resize. Arrow keys on the focused handle resize vertically; Shift + arrows resize horizontally.
- The × button hides it. Use the extension's **Show on this tab** button to bring it back. Click the collapsed notch to reopen.
- Alt + Shift + Y shows/collapses, Alt + Shift + P plays/pauses, Alt + Shift + R restarts. On Mac, Alt is Option. Change conflicting shortcuts at `chrome://extensions/shortcuts`. These defaults work while Chrome has focus.

## Limits and privacy

The overlay stays **inside the current web page**. It cannot float over desktop apps, Chrome's toolbar, browser settings, the Chrome Web Store or other protected pages. Some built-in viewers and full-screen video surfaces also prevent overlays. After a page reload or navigation, invoke the extension again. It does not monitor your camera.

The overlay **is visible in recordings and screen shares of its tab/display**. It cannot inherit the Windows desktop app's capture exclusion. Use a separate unshared window for scripts or record webcam-only when appropriate, and test your recording setup.

Permissions are limited to `activeTab`, `scripting`, and `storage`. It has no permanent all-sites access, network requests, analytics, remote scripts, camera/microphone permission, or access to your browsing history. You grant temporary access to the page when you invoke it. The script is inserted into that page's DOM so it can be displayed; do not treat it as hidden from a website you don't trust. Browser-local storage is not encrypted. Removing the extension removes its saved data, so keep a separate copy of important scripts.

## Update / remove

Replace the files in the installed folder with the new extracted release, preserving the folder path. Click **Reload** on NotchPrompter's card at `chrome://extensions`, then reload the website tab and invoke the extension again. To uninstall, click **Remove** on the extension's card.

## Development

From the repository root: `python tools/package-extension.py`. This creates the downloadable ZIP plus its checksum. Runtime code has no build step or external dependencies. Browser integration tests live in `tests/` and run in isolated Chromium, not your personal Chrome profile. The test fixture grants access only to its localhost test page; the shipping manifest continues to use temporary `activeTab` access.
