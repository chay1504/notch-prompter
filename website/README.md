# NotchPrompter website

A static landing page (no build step) for NotchPrompter on Windows and Mac.

The Mac beta link downloads the universal app ZIP from the versioned GitHub prerelease. Update that link and the release-notes link in `index.html` when publishing a new Mac release. Keep the unnotarized-app and recording disclosures visible. The current video demonstrates Windows behavior.

The primary Chrome extension ZIP is served from `downloads/`. From the repository root, run `python tools/package-extension.py` after changing the extension; this also updates its checksum and size metadata. Update the version shown in the website and ZIP URL query when releasing a new extension version. Developer-mode installation instructions live in the `#extension` section; native Mac installation options live in `#mac-install`.

```
index.html      the page
styles.css      light + dark theme, mobile-first
app.js          interactive notch demo (same shapes and springs as the app), download toast, video autoplay
downloads/      NotchPrompter.exe  ← the file the Download buttons serve
media/          notchprompter-how-to.mp4 (45 s explainer) + poster.jpg
assets/         favicon + app icon
vercel.json     forces the .exe to download (Content-Disposition: attachment) and caches media
```

## Preview locally

```bash
python -m http.server 8643 --bind 127.0.0.1 --directory website
```

From the repository root, then open http://127.0.0.1:8643.

## Updating the app

1. Rebuild in the repository root (`.\build.ps1`).
2. Copy `dist\NotchPrompter.exe` into `downloads/`.
3. Update the size, version and SHA-256 shown near the bottom of `index.html`:
   `Get-FileHash downloads\NotchPrompter.exe -Algorithm SHA256`.

## Deploying

Any static host works. On Vercel, deploy this folder as-is; `vercel.json` already sets the download headers.
Because the exe isn't code-signed, Windows SmartScreen warns on first run. The page tells visitors to click
**More info → Run anyway**. Signing the exe with a code-signing certificate removes that warning over time.
