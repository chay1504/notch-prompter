import AppKit
import PrompterCore

final class IslandPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class IslandView: NSView {
    enum Mode { case collapsed, peek, expanded }
    var preferences: Preferences
    var script: String { didSet { rebuildText(); offset = 0; needsDisplay = true } }
    var changed: (() -> Void)?
    var edit: (() -> Void)?
    var mode: Mode = .collapsed
    var cameraActive = false { didSet { needsDisplay = true } }
    var offset: Double = 0
    private(set) var playing = false
    private var countdown: Double = 0
    private var clock: Timer?
    private var lastTime: TimeInterval = 0
    private var tracking: NSTrackingArea?
    private var dragStart: NSPoint?
    private var dragSize = NSSize.zero
    private var hovered = false
    private var autoOpened = false
    private var words = 0
    private var textHeight: CGFloat = 0
    private let storage = NSTextStorage()
    private let layout = NSLayoutManager()
    private let container = NSTextContainer(size: NSSize(width: 560, height: CGFloat.greatestFiniteMagnitude))
    override var isFlipped: Bool { true }
    private var viewport: NSRect { NSRect(x: 28, y: 12, width: max(1, bounds.width - 56), height: max(1, bounds.height - 64)) }
    // Let the last line reach the top reading position before stopping.
    private var maxOffset: Double { max(0, Double(textHeight - preferences.fontSize * 1.2)) }

    init(preferences: Preferences, script: String) {
        self.preferences = preferences; self.script = script
        super.init(frame: NSRect(x: 0, y: 0, width: 184, height: 28))
        container.lineFragmentPadding = 0
        layout.addTextContainer(container); storage.addLayoutManager(layout)
        rebuildText(); offset = min(maxOffset, preferences.offset)
        setAccessibilityElement(true)
        setAccessibilityLabel("NotchPrompter teleprompter. Use Control Option E to edit; Control Option Space to play.")
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let tracking = tracking { removeTrackingArea(tracking) }
        tracking = NSTrackingArea(rect: .zero, options: [.activeAlways, .mouseEnteredAndExited, .inVisibleRect], owner: self)
        addTrackingArea(tracking!)
    }
    override func mouseEntered(with event: NSEvent) {
        hovered = true
        if mode == .collapsed { setMode(.peek) }; needsDisplay = true
    }
    override func mouseExited(with event: NSEvent) {
        hovered = false
        if mode == .peek { setMode(.collapsed) }; needsDisplay = true
    }
    func setMode(_ next: Mode, animate: Bool = true) {
        if next != .expanded { pause() }
        mode = next
        reposition(animate: animate)
    }
    func reposition(animate: Bool = false) {
        guard let panel = window, let screen = NSScreen.screens.first else { return }
        let safe = screen.safeAreaInsets.top
        let notchWidth: CGFloat
        if let left = screen.auxiliaryTopLeftArea, let right = screen.auxiliaryTopRightArea {
            notchWidth = max(184, right.minX - left.maxX)
        } else { notchWidth = 184 }
        let target: NSSize
        switch mode {
        case .collapsed: target = NSSize(width: notchWidth, height: 28)
        case .peek: target = NSSize(width: max(300, notchWidth), height: 40)
        case .expanded:
            target = NSSize(width: min(preferences.width, screen.frame.width - 40),
                height: min(preferences.height, screen.frame.height - safe - 60))
        }
        let frame = NSRect(x: screen.frame.midX - target.width / 2,
            y: screen.frame.maxY - safe - target.height, width: target.width, height: target.height)
        if animate {
            NSAnimationContext.runAnimationGroup { context in
                context.duration = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion ? 0 : 0.2
                panel.animator().setFrame(frame, display: true)
            }
        } else { panel.setFrame(frame, display: true) }
        rebuildText(); needsDisplay = true
    }
    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        rebuildText(); needsDisplay = true
    }
    func rebuildText() {
        let result = NSMutableAttributedString(string: "")
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = preferences.centerText ? .center : .left
        paragraph.lineSpacing = preferences.fontSize * 0.22
        let normal: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: preferences.fontSize, weight: .medium),
            .foregroundColor: NSColor.white, .paragraphStyle: paragraph]
        for (index, line) in Script.lines(script).enumerated() {
            if index > 0 { result.append(NSAttributedString(string: "\n", attributes: normal)) }
            if line.isHeading {
                var style = normal
                style[.font] = NSFont.systemFont(ofSize: max(12, preferences.fontSize * 0.5), weight: .bold)
                style[.foregroundColor] = NSColor.systemCyan
                result.append(NSAttributedString(string: line.text.uppercased(), attributes: style))
            } else {
                for (part, text) in line.text.components(separatedBy: "*").enumerated() {
                    var style = normal
                    if part % 2 == 1 { style[.foregroundColor] = NSColor.systemYellow }
                    result.append(NSAttributedString(string: text, attributes: style))
                }
            }
        }
        storage.setAttributedString(result)
        container.containerSize = NSSize(width: max(1, viewport.width), height: CGFloat.greatestFiniteMagnitude)
        layout.ensureLayout(for: container)
        textHeight = layout.usedRect(for: container).height
        words = Script.wordCount(script)
        offset = min(offset, maxOffset)
    }
    private func label(_ text: String, rect: NSRect, size: CGFloat, color: NSColor = .white) {
        let paragraph = NSMutableParagraphStyle(); paragraph.alignment = .center
        (text as NSString).draw(in: rect, withAttributes: [.font: NSFont.systemFont(ofSize: size, weight: .medium),
            .foregroundColor: color, .paragraphStyle: paragraph])
    }
    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        NSColor.black.setFill()
        NSBezierPath(roundedRect: bounds, xRadius: mode == .expanded ? 26 : 12,
            yRadius: mode == .expanded ? 26 : 12).fill()
        // Square the upper edge so the island joins the bezel / physical notch.
        NSRect(x: 0, y: 0, width: bounds.width, height: min(12, bounds.height)).fill()
        if cameraActive {
            NSColor.systemGreen.setFill()
            NSBezierPath(ovalIn: NSRect(x: bounds.width - 21, y: 10, width: 6, height: 6)).fill()
        }
        guard mode == .expanded else {
            label(mode == .peek ? "Teleprompter   ·   ⌃⌥T" : "···", rect: bounds.insetBy(dx: 12, dy: 7), size: 13)
            return
        }
        NSGraphicsContext.saveGraphicsState()
        viewport.clip()
        let origin = NSPoint(x: viewport.minX, y: viewport.minY - offset)
        let range = layout.glyphRange(for: container)
        layout.drawBackground(forGlyphRange: range, at: origin)
        layout.drawGlyphs(forGlyphRange: range, at: origin)
        NSGraphicsContext.restoreGraphicsState()
        if countdown > 0 {
            NSColor.black.withAlphaComponent(0.85).setFill(); viewport.fill()
            label(String(Int(ceil(countdown))), rect: viewport.insetBy(dx: 0, dy: max(0, viewport.height / 2 - 30)), size: 48)
        }
        let y = bounds.height - 44
        let remaining = max(0, Double(words) / preferences.wpm * 60 * (1 - offset / max(1, maxOffset)))
        label("\(Int(preferences.wpm)) wpm  ·  \(Int(ceil(remaining)))s left", rect: NSRect(x: 22, y: y, width: 190, height: 22), size: 12, color: .lightGray)
        if hovered {
            for (i, title) in ["↺", "−", "+", playing || countdown > 0 ? "Ⅱ" : "▶", "Edit", "⌃"].enumerated() {
                label(title, rect: buttonRect(i), size: 16)
            }
        }
        NSColor.darkGray.setFill(); NSRect(x: 30, y: bounds.height - 12, width: bounds.width - 60, height: 2).fill()
        NSColor.systemCyan.setFill()
        NSRect(x: 30, y: bounds.height - 12, width: (bounds.width - 60) * offset / max(1, maxOffset), height: 2).fill()
        NSColor.gray.setFill(); NSRect(x: bounds.midX - 18, y: bounds.height - 5, width: 36, height: 2).fill()
    }
    private func buttonRect(_ index: Int) -> NSRect {
        NSRect(x: bounds.width - 228 + CGFloat(index) * 34, y: bounds.height - 46, width: 32, height: 30)
    }
    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)
        if mode == .expanded && point.y > bounds.height - 9 {
            dragStart = NSEvent.mouseLocation; dragSize = bounds.size; return
        }
        if mode != .expanded { autoOpened = false; setMode(.expanded); return }
        if hovered, let index = (0..<6).first(where: { buttonRect($0).contains(point) }) {
            switch index {
            case 0: restart()
            case 1: adjustSpeed(-10)
            case 2: adjustSpeed(10)
            case 3: togglePlay()
            case 4: edit?()
            default: autoOpened = false; setMode(.collapsed)
            }
        } else { togglePlay() }
    }
    override func mouseDragged(with event: NSEvent) {
        guard let start = dragStart else { return }
        let now = NSEvent.mouseLocation
        preferences.width = Double(dragSize.width + 2 * (now.x - start.x))
        preferences.height = Double(dragSize.height - (now.y - start.y))
        preferences.normalize(); reposition(); changed?()
    }
    override func mouseUp(with event: NSEvent) { dragStart = nil }
    override func scrollWheel(with event: NSEvent) {
        guard mode == .expanded else { return }
        if event.modifierFlags.contains(.control) { adjustFont(event.scrollingDeltaY > 0 ? 1 : -1) }
        else if event.modifierFlags.contains(.shift) { adjustSpeed(event.scrollingDeltaY > 0 ? 5 : -5) }
        else { seek(Double(-event.scrollingDeltaY * (event.hasPreciseScrollingDeltas ? 1 : 12))) }
    }
    func seek(_ delta: Double) {
        offset = min(maxOffset, max(0, offset + delta)); changed?(); needsDisplay = true
    }
    func adjustSpeed(_ delta: Double) {
        preferences.wpm += delta; preferences.normalize(); changed?(); needsDisplay = true
    }
    func adjustFont(_ delta: Double) {
        let progress = offset / max(1, maxOffset)
        preferences.fontSize += delta; preferences.normalize(); rebuildText()
        offset = progress * maxOffset; changed?(); needsDisplay = true
    }
    func restart() { pause(); offset = 0; changed?(); needsDisplay = true }
    func pause() {
        playing = false; countdown = 0; clock?.invalidate(); clock = nil
        changed?(); needsDisplay = true
    }
    func togglePlay() {
        if playing || countdown > 0 { pause(); return }
        setMode(.expanded)
        guard words > 0 else { return }
        if offset >= maxOffset { offset = 0 }
        countdown = preferences.countdown && offset == 0 ? 3 : 0
        playing = countdown == 0; lastTime = ProcessInfo.processInfo.systemUptime
        clock = Timer(timeInterval: 1 / 60, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(clock!, forMode: .common)
    }
    private func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        let dt = min(0.1, now - lastTime); lastTime = now
        if countdown > 0 {
            countdown = max(0, countdown - dt); playing = countdown == 0
        } else if playing {
            offset = min(maxOffset, offset + Script.pixelsPerSecond(height: maxOffset, words: words, wpm: preferences.wpm) * dt)
            if offset >= maxOffset { pause() }
        }
        needsDisplay = true
    }
    func cameraChanged(_ active: Bool) {
        guard active != cameraActive else { return }
        cameraActive = active
        if active && preferences.autoCamera && mode != .expanded {
            autoOpened = true; setMode(.expanded)
        } else if !active && autoOpened {
            autoOpened = false
            // Preserve a take the user has started even if the camera briefly disconnects.
            if !playing && countdown == 0 { setMode(.collapsed) }
        }
    }
}
