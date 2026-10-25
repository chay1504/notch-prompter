import AppKit
import AVFoundation
import Carbon
import ServiceManagement
import UniformTypeIdentifiers
import PrompterCore

final class AppController: NSObject, NSApplicationDelegate, NSTextViewDelegate, NSWindowDelegate {
    let smoke = CommandLine.arguments.contains("--smoke-test")
    lazy var store = Store(directory: smoke ? FileManager.default.temporaryDirectory
        .appendingPathComponent("NotchPrompter-smoke-\(UUID().uuidString)") : nil)
    var island: IslandView!
    var panel: IslandPanel!
    var statusItem: NSStatusItem!
    var editor: NSWindow?
    var textView: NSTextView?
    var stats: NSTextField?
    var hotkeys: Hotkeys?
    var saveTimer: Timer?
    var cameraTimer: Timer?
    var screenObserver: NSObjectProtocol?
    var lastSaveError: String?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        // A second .app launch raises the existing process; handle direct executable launches too.
        if !smoke, let id = Bundle.main.bundleIdentifier,
           let other = NSRunningApplication.runningApplications(withBundleIdentifier: id)
            .first(where: { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }) {
            other.activate(options: []); NSApp.terminate(nil); return
        }
        island = IslandView(preferences: store.loadPreferences(), script: store.loadScript())
        panel = IslandPanel(contentRect: island.frame, styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered, defer: false)
        panel.title = "NotchPrompter"; panel.contentView = island
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = false
        panel.level = .statusBar
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.hidesOnDeactivate = false; panel.isReleasedWhenClosed = false
        panel.isMovable = false; panel.becomesKeyOnlyIfNeeded = true
        island.edit = { [weak self] in self?.openEditor() }
        island.changed = { [weak self] in self?.scheduleSave() }
        island.reposition(); panel.orderFrontRegardless()
        screenObserver = NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification,
            object: nil, queue: .main) { [weak self] _ in self?.island.reposition() }
        buildMenu()
        if smoke {
            runSmokeTest(); return
        }
        registerHotkeys()
        // Read device state only: never create a capture session or request camera access.
        let deviceTypes: [AVCaptureDevice.DeviceType]
        if #available(macOS 14.0, *) {
            deviceTypes = [.builtInWideAngleCamera, .external, .continuityCamera]
        } else {
            deviceTypes = [.builtInWideAngleCamera, .externalUnknown]
        }
        let discovery = AVCaptureDevice.DiscoverySession(deviceTypes: deviceTypes, mediaType: .video, position: .unspecified)
        cameraTimer = Timer.scheduledTimer(withTimeInterval: 1.2, repeats: true) { [weak self] _ in
            let active = discovery.devices.contains { $0.isInUseByAnotherApplication }
            self?.island.cameraChanged(active)
        }
        if !UserDefaults.standard.bool(forKey: "CaptureNoticeSeen") {
            UserDefaults.standard.set(true, forKey: "CaptureNoticeSeen")
            DispatchQueue.main.async { [weak self] in self?.captureNotice() }
        }
        if !hotkeys!.failures.isEmpty {
            showMessage("Some shortcuts are unavailable", hotkeys!.failures.joined(separator: ", ") +
                ". Another app may already use them. The menu bar controls remain available.")
        }
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        panel.orderFrontRegardless(); island.setMode(.expanded); return false
    }
    func menuItem(_ title: String, _ action: Selector, checked: Bool? = nil) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self
        if let checked = checked { item.state = checked ? .on : .off }
        return item
    }
    func buildMenu() {
        if statusItem == nil {
            statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
            statusItem.button?.image = NSImage(systemSymbolName: "text.bubble.fill", accessibilityDescription: "NotchPrompter")
        }
        let menu = NSMenu()
        let heading = NSMenuItem(title: "NotchPrompter for Mac", action: nil, keyEquivalent: ""); menu.addItem(heading)
        menu.addItem(menuItem("Show / hide   ⌃⌥T", #selector(toggle)))
        menu.addItem(menuItem("Play / pause   ⌃⌥Space", #selector(play)))
        menu.addItem(menuItem("Back to top   ⌃⌥R", #selector(restart)))
        menu.addItem(menuItem("Edit script…   ⌃⌥E", #selector(openEditor)))
        menu.addItem(.separator())
        menu.addItem(menuItem("3-second countdown", #selector(toggleCountdown), checked: island.preferences.countdown))
        menu.addItem(menuItem("Open when camera is in use", #selector(toggleCamera), checked: island.preferences.autoCamera))
        menu.addItem(menuItem("Center script text", #selector(toggleCenter), checked: island.preferences.centerText))
        menu.addItem(menuItem("Start at login", #selector(toggleLogin), checked: SMAppService.mainApp.status == .enabled))
        menu.addItem(.separator())
        menu.addItem(menuItem("Screen recording limitations…", #selector(captureNotice)))
        menu.addItem(menuItem("Keyboard shortcuts…", #selector(shortcuts)))
        menu.addItem(menuItem("Show saved script", #selector(showFiles)))
        menu.addItem(.separator())
        menu.addItem(menuItem("Quit NotchPrompter", #selector(quit)))
        statusItem.menu = menu
    }
    func registerHotkeys() {
        let keys = Hotkeys(); hotkeys = keys
        keys.register(kVK_ANSI_T, label: "⌃⌥T") { [weak self] in self?.toggle() }
        keys.register(kVK_Space, label: "⌃⌥Space") { [weak self] in self?.play() }
        keys.register(kVK_ANSI_R, label: "⌃⌥R") { [weak self] in self?.restart() }
        keys.register(kVK_ANSI_E, label: "⌃⌥E") { [weak self] in self?.openEditor() }
        keys.register(kVK_ANSI_Equal, label: "⌃⌥=") { [weak self] in self?.island.adjustSpeed(10) }
        keys.register(kVK_ANSI_Minus, label: "⌃⌥−") { [weak self] in self?.island.adjustSpeed(-10) }
        keys.register(kVK_ANSI_RightBracket, label: "⌃⌥]") { [weak self] in self?.island.adjustFont(2) }
        keys.register(kVK_ANSI_LeftBracket, label: "⌃⌥[") { [weak self] in self?.island.adjustFont(-2) }
        keys.register(kVK_PageUp, label: "⌃⌥PageUp") { [weak self] in self?.island.seek(-100) }
        keys.register(kVK_PageDown, label: "⌃⌥PageDown") { [weak self] in self?.island.seek(100) }
    }
    @objc func toggle() {
        if panel.isVisible && island.mode == .expanded { island.setMode(.collapsed) }
        else { panel.orderFrontRegardless(); island.setMode(.expanded) }
    }
    @objc func play() { panel.orderFrontRegardless(); island.togglePlay() }
    @objc func restart() { island.restart() }
    @objc func quit() { NSApp.terminate(nil) }
    @objc func toggleCountdown() { island.preferences.countdown.toggle(); settingsChanged() }
    @objc func toggleCamera() { island.preferences.autoCamera.toggle(); settingsChanged() }
    @objc func toggleCenter() {
        island.preferences.centerText.toggle(); island.rebuildText(); settingsChanged()
    }
    func settingsChanged() { island.needsDisplay = true; scheduleSave(); buildMenu() }
    @objc func toggleLogin() {
        do {
            if SMAppService.mainApp.status == .enabled { try SMAppService.mainApp.unregister() }
            else { try SMAppService.mainApp.register() }
        } catch { showMessage("Could not change login setting", error.localizedDescription) }
        if SMAppService.mainApp.status == .requiresApproval {
            SMAppService.openSystemSettingsLoginItems()
        }
        buildMenu()
    }
    @objc func captureNotice() {
        showMessage("Mac screen recordings may include the prompter",
            "macOS does not provide a reliable public API to hide this overlay from every recorder or screen share. " +
            "Share a specific app window, or record your webcam only, and test your setup first. " +
            "This Mac version does not promise the Windows app’s capture exclusion.")
    }
    @objc func shortcuts() {
        showMessage("Keyboard shortcuts", "Control + Option + Space: play / pause\n" +
            "Control + Option + T: show / collapse\nControl + Option + E: edit script\n" +
            "Control + Option + R: back to top\nControl + Option + = / −: change speed\n" +
            "Control + Option + ] / [: text size\nControl + Option + PageUp / PageDown: seek\n\n" +
            "Click the island to open it; click the script to play. Scroll to seek. " +
            "Control + scroll changes size; Shift + scroll changes speed. Drag the bottom edge to resize.")
    }
    @objc func showFiles() {
        saveNow()
        NSWorkspace.shared.activateFileViewerSelecting([store.scriptURL])
    }
    func showMessage(_ title: String, _ message: String) {
        let alert = NSAlert(); alert.messageText = title; alert.informativeText = message
        NSApp.activate(ignoringOtherApps: true); alert.runModal()
    }
    @objc func openEditor() {
        if let editor = editor {
            editor.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); return
        }
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 780, height: 600),
            styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "NotchPrompter — Script"; window.minSize = NSSize(width: 520, height: 360)
        window.isReleasedWhenClosed = false; window.delegate = self
        window.appearance = NSAppearance(named: .darkAqua)
        let root = window.contentView!
        let scroll = NSScrollView(); scroll.translatesAutoresizingMaskIntoConstraints = false
        scroll.hasVerticalScroller = true; scroll.borderType = .noBorder
        let text = NSTextView(frame: NSRect(x: 0, y: 0, width: 740, height: 480))
        text.isRichText = false; text.isAutomaticQuoteSubstitutionEnabled = false
        text.isAutomaticDashSubstitutionEnabled = false; text.font = .systemFont(ofSize: 19)
        text.textColor = .white; text.backgroundColor = NSColor(white: 0.1, alpha: 1)
        text.textContainerInset = NSSize(width: 20, height: 20)
        text.isVerticallyResizable = true; text.isHorizontallyResizable = false
        text.minSize = NSSize(width: 0, height: 0)
        text.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        text.textContainer?.containerSize = NSSize(width: 740, height: CGFloat.greatestFiniteMagnitude)
        text.autoresizingMask = [.width]; text.textContainer?.widthTracksTextView = true
        text.string = island.script; text.delegate = self
        text.setAccessibilityLabel("Teleprompter script")
        scroll.documentView = text; root.addSubview(scroll)
        let info = NSTextField(labelWithString: ""); info.translatesAutoresizingMaskIntoConstraints = false
        info.font = .systemFont(ofSize: 12); info.textColor = .secondaryLabelColor; root.addSubview(info)
        let open = NSButton(title: "Open .txt…", target: self, action: #selector(importScript))
        let start = NSButton(title: "Start from top", target: self, action: #selector(startFromEditor))
        open.bezelStyle = .rounded; start.bezelStyle = .rounded
        open.translatesAutoresizingMaskIntoConstraints = false; start.translatesAutoresizingMaskIntoConstraints = false
        root.addSubview(open); root.addSubview(start)
        NSLayoutConstraint.activate([
            scroll.topAnchor.constraint(equalTo: root.topAnchor), scroll.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            scroll.trailingAnchor.constraint(equalTo: root.trailingAnchor), scroll.bottomAnchor.constraint(equalTo: root.bottomAnchor, constant: -68),
            info.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 18),
            info.topAnchor.constraint(equalTo: scroll.bottomAnchor, constant: 8),
            info.trailingAnchor.constraint(lessThanOrEqualTo: root.trailingAnchor, constant: -18),
            open.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 18), open.bottomAnchor.constraint(equalTo: root.bottomAnchor, constant: -8),
            start.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -18), start.bottomAnchor.constraint(equalTo: open.bottomAnchor)
        ])
        editor = window; textView = text; stats = info; updateStats()
        window.center(); window.makeKeyAndOrderFront(nil); window.makeFirstResponder(text)
        root.layoutSubtreeIfNeeded(); scroll.tile()
        text.setFrameSize(NSSize(width: scroll.contentSize.width, height: max(scroll.contentSize.height, text.frame.height)))
        NSApp.activate(ignoringOtherApps: true)
    }
    func textDidChange(_ notification: Notification) {
        island.pause(); island.script = textView?.string ?? ""; scheduleSave(); updateStats()
    }
    func updateStats() {
        let words = Script.wordCount(island.script)
        stats?.stringValue = "\(words) words · ~\(Int(ceil(Double(words) / island.preferences.wpm * 60)))s · *highlight* · # Section · saved locally"
    }
    @objc func importScript() {
        let open = NSOpenPanel(); open.allowedContentTypes = [.plainText]; open.allowsMultipleSelection = false
        guard open.runModal() == .OK, let url = open.url else { return }
        do {
            let script = try String(contentsOf: url, encoding: .utf8)
            textView?.string = script; island.pause(); island.script = script
            scheduleSave(); updateStats()
        } catch { showMessage("Could not open script", error.localizedDescription) }
    }
    @objc func startFromEditor() {
        saveNow(); editor?.orderOut(nil); island.restart(); panel.orderFrontRegardless(); island.togglePlay()
    }
    func windowWillClose(_ notification: Notification) { saveNow() }
    func scheduleSave() {
        saveTimer?.invalidate()
        saveTimer = Timer.scheduledTimer(withTimeInterval: 0.6, repeats: false) { [weak self] _ in self?.saveNow() }
    }
    func saveNow() {
        guard island != nil else { return }
        island.preferences.offset = island.offset
        do { try store.save(island.preferences, script: island.script); lastSaveError = nil }
        catch {
            lastSaveError = error.localizedDescription
            stats?.stringValue = "Save failed: \(error.localizedDescription)"
            NSLog("NotchPrompter save failed: %@", error.localizedDescription)
        }
    }
    func applicationWillTerminate(_ notification: Notification) {
        saveTimer?.invalidate(); cameraTimer?.invalidate(); island?.pause(); saveNow()
        if let observer = screenObserver { NotificationCenter.default.removeObserver(observer) }
        if smoke { try? FileManager.default.removeItem(at: store.directory) }
    }
    func runSmokeTest() {
        // Exercise real AppKit windows on a macOS runner, without camera/login changes.
        island.setMode(.expanded, animate: false); openEditor()
        assert(panel.isVisible && editor!.isVisible && !panel.canBecomeKey)
        assert(island.bounds.width >= 440)
        island.preferences.countdown = false
        island.togglePlay(); assert(island.playing)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [self] in
            assert(island.offset > 0)
            island.pause(); island.adjustSpeed(10); island.adjustFont(2)
            island.restart(); assert(island.offset == 0)
            saveNow(); assert(lastSaveError == nil)
            assert(store.loadScript() == island.script)
            assert(store.loadPreferences().wpm == 150)
            assert(textView!.textContainer!.containerSize.width <= editor!.contentView!.bounds.width)
            saveSmokeImage(island, name: "island-smoke.png")
            saveSmokeImage(editor!.contentView!, name: "editor-smoke.png")
            print("AppKit smoke test passed: panel, editor, playback, controls and local saving")
            NSApp.terminate(nil)
        }
    }
    func saveSmokeImage(_ view: NSView, name: String) {
        let output = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent("dist")
        do {
            try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
            guard let image = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { fatalError("No view bitmap") }
            view.layoutSubtreeIfNeeded(); view.displayIfNeeded()
            view.effectiveAppearance.performAsCurrentDrawingAppearance {
                view.cacheDisplay(in: view.bounds, to: image)
            }
            guard let data = image.representation(using: .png, properties: [:]) else { fatalError("No PNG data") }
            try data.write(to: output.appendingPathComponent(name))
        } catch { fatalError("Smoke image failed: \(error)") }
    }
}

let app = NSApplication.shared
let controller = AppController()
app.delegate = controller
app.run()
