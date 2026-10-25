Chrome extension release, Windows teleprompter and native Mac beta installation improvements.

- **Chrome (new):** download `NotchPrompter-Chrome.zip`, extract it, open `chrome://extensions`, enable Developer mode, then **Load unpacked** the folder containing `manifest.json`. Pin the extension, open a normal website, paste a script and choose **Show on this tab**. [Setup guide](https://notch-prompter-zeta.vercel.app/#extension).
- Chrome scripts save locally; permissions are limited to temporary active-tab access, scripting and storage. No all-sites permission, uploads, camera access or remote code. The overlay is part of the chosen web page and is visible in tab/screen recordings.
- **Mac installation diagnosis:** the native beta is ad-hoc signed and remains unnotarized. Apple may block its first launch. The release ZIP is now extracted and its signature and both CPU architectures are checked again in CI. A malformed checksum path was also corrected. These packaging checks do not make the app Apple-approved.
- **Free native Mac alternative:** compile the reviewed source on your own Mac with Apple's free Command Line Tools using `bash macos/build-local.sh`. [Instructions and warning distinctions](https://github.com/chay1504/notch-prompter/blob/main/macos/INSTALL.md). No Gatekeeper-disabling or quarantine-removal script is included.

- **Windows:** download `NotchPrompter.exe` and open it. The Windows ZIP also includes the optional install/uninstall scripts.
- **Mac:** download `NotchPrompter-macOS-universal.zip`, unzip, and drag `NotchPrompter.app` to Applications. Requires macOS 13 or later; includes Apple Silicon and Intel binaries.
- The Mac build is ad-hoc signed, **not Developer ID signed or notarized**. macOS may block the first launch. Follow Apple's instructions for opening an app from an unidentified developer, only if you trust this source: https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac
- **Mac recording limitation:** the prompter can appear in display recordings and screen sharing. Share a specific app window or record webcam-only and test your exact setup. Capture exclusion is a Windows feature.
- Scripts stay on your computer. Mac storage: `~/Library/Application Support/NotchPrompter/`.
- Mac shortcuts use **Control + Option**, for example Control + Option + E to edit and Control + Option + Space to play.

GitHub Actions checks compilation for both Mac architectures, core unit tests, a native AppKit smoke test, release-ZIP signature integrity, Windows compilation, Chrome extension runtime tests, website links, and download integrity. Browser tests use a localhost-only permission fixture to exercise injection; the shipping manifest is checked separately to grant only activeTab access. These checks do not establish physical Mac camera detection, notch placement, multi-monitor behavior, Gatekeeper onboarding, or compatibility with every recorder. Those need a real-device review.

Compare downloads against `SHA256SUMS.txt` in this release.
