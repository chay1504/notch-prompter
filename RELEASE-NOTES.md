Windows teleprompter and first native Mac beta.

- **Windows:** download `NotchPrompter.exe` and open it. The Windows ZIP also includes the optional install/uninstall scripts.
- **Mac:** download `NotchPrompter-macOS-universal.zip`, unzip, and drag `NotchPrompter.app` to Applications. Requires macOS 13 or later; includes Apple Silicon and Intel binaries.
- The Mac build is ad-hoc signed, **not Developer ID signed or notarized**. macOS may block the first launch. Follow Apple's instructions for opening an app from an unidentified developer, only if you trust this source: https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac
- **Mac recording limitation:** the prompter can appear in display recordings and screen sharing. Share a specific app window or record webcam-only and test your exact setup. Capture exclusion is a Windows feature.
- Scripts stay on your computer. Mac storage: `~/Library/Application Support/NotchPrompter/`.
- Mac shortcuts use **Control + Option**, for example Control + Option + E to edit and Control + Option + Space to play.

GitHub Actions checks compilation for both Mac architectures, core unit tests, a native AppKit smoke test, Windows compilation, website links, and download integrity. These checks do not establish physical Mac camera detection, notch placement, multi-monitor behavior, Gatekeeper onboarding, or compatibility with every recorder. Those need a real-device review.

Compare downloads against `SHA256SUMS.txt` in this release.
