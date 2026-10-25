# NotchPrompter website

A static landing page (no build step) for NotchPrompter on Windows and Mac.

The Mac button downloads the universal app ZIP from the versioned GitHub prerelease. When publishing a new Mac release, update both `data-mac-download` links and the release-notes link in `index.html`. Mac capture exclusion is not guaranteed; keep the recording disclosure visible. The current explainer video demonstrates Windows behavior.

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
