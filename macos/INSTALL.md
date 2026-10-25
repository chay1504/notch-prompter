# Mac installation: what is and is not fixed

NotchPrompter's native Mac beta is **ad-hoc signed**, not signed with an Apple Developer ID or notarized. The build checks both CPU architectures and verifies the signature again after extracting the downloadable ZIP. A valid ad-hoc signature detects changes to the app; it does not identify an Apple-approved publisher and does not remove Gatekeeper's unidentified-developer warning.

The `.app` suffix only identifies a Mac application bundle. Another `.app` opening on the same Mac does not establish that this one will: the other app may be notarized, already approved, built locally, or downloaded without the same quarantine metadata. We have not inspected the other app or the exact warning on your Mac.

## Easiest option without an Apple Developer account

Use the [Chrome extension](https://notch-prompter-zeta.vercel.app/#extension). Extract the extension ZIP, then load its folder using Developer mode at `chrome://extensions`. It runs inside Chrome rather than installing a Mac app. Browser-tab and recording limitations are explained on the website.

## Build the native app locally (free)

You can compile the public source on your own Mac with Apple's free Xcode Command Line Tools. No Apple Developer subscription is required for this local development build.

1. Review/download the source from [the repository](https://github.com/chay1504/notch-prompter), or clone it with Git.
2. If Swift isn't installed, run `xcode-select --install` in Terminal and complete Apple's installation dialog.
3. In Terminal, change into the downloaded repository folder and run:

```sh
bash macos/build-local.sh
```

The script creates `macos/dist/NotchPrompter.app`. Open that locally compiled copy from Finder, or copy it to Applications. A local build has a different trust/quarantine history than a downloaded app, but managed-device policies and local security settings can still apply. The script does not strip quarantine, disable Gatekeeper, install a certificate or change system security settings.

## Understand the warning before acting

- **“Apple cannot check for malicious software” / “developer cannot be verified”**: this is expected for this unnotarized beta. If you have independently verified and trust the download, Apple's [per-app opening guidance](https://support.apple.com/en-us/102445) describes the available approval flow in System Settings → Privacy & Security. Approval is your decision; do not disable Gatekeeper globally.
- **“App is damaged”**: obtain a fresh download, compare its SHA-256 with the release, and provide the exact message so packaging/signature problems can be investigated.
- **“Will damage your computer” / malware detected**: stop using that copy. This is a different warning, and it must not be treated as a routine unidentified-developer dialog.

There is no claim here that Apple has scanned or approved this beta. A broadly trusted native download normally requires Developer ID signing plus notarization, which is not configured because the publisher chose not to join Apple's Developer Program. [Apple's notarization requirements](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution).
