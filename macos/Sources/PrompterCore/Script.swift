import Foundation

public struct ScriptLine: Equatable {
    public let text: String
    public let isHeading: Bool
}

public enum Script {
    public static func lines(_ text: String) -> [ScriptLine] {
        text.replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n").components(separatedBy: "\n").map {
                let trimmed = $0.trimmingCharacters(in: .whitespaces)
                let heading = trimmed.hasPrefix("#")
                let content = heading ? String(trimmed.drop(while: { $0 == "#" }))
                    .trimmingCharacters(in: .whitespaces) : $0
                return ScriptLine(text: content, isHeading: heading)
            }
    }
    public static func wordCount(_ text: String) -> Int {
        lines(text).filter { !$0.isHeading }.reduce(0) {
            $0 + $1.text.replacingOccurrences(of: "*", with: "")
                .split(whereSeparator: { $0.isWhitespace }).count
        }
    }
    public static func pixelsPerSecond(height: Double, words: Int, wpm: Double) -> Double {
        guard height > 0, words > 0, wpm.isFinite, wpm > 0 else { return 0 }
        return height * wpm / (Double(words) * 60)
    }
    public static let welcome = """
    # Welcome
    Your script lives right under your webcam.
    Look towards the lens and speak at your own pace.

    # Controls
    Press *Control + Option + E* to paste your script.
    Press *Control + Option + Space* to play or pause.
    Scroll to move through the script, or drag the bottom edge to resize.

    # Recording on Mac
    The Mac overlay may appear in screen recordings and screen sharing.
    Share a specific app window instead of your whole display, and make a test recording first.
    """
}

public struct Preferences: Codable {
    public var wpm: Double = 140
    public var fontSize: Double = 28
    public var width: Double = 620
    public var height: Double = 220
    public var offset: Double = 0
    public var countdown = true
    public var autoCamera = true
    public var centerText = true
    public init() {}
    public mutating func normalize() {
        func clamp(_ value: Double, _ low: Double, _ high: Double) -> Double {
            value.isFinite ? min(high, max(low, value)) : low
        }
        wpm = clamp(wpm, 40, 400); fontSize = clamp(fontSize, 16, 72)
        width = clamp(width, 440, 1400); height = clamp(height, 130, 700)
        offset = offset.isFinite ? max(0, offset) : 0
    }
}

public final class Store {
    public let directory: URL
    public var scriptURL: URL { directory.appendingPathComponent("script.txt") }
    private var preferencesURL: URL { directory.appendingPathComponent("settings.json") }
    public init(directory: URL? = nil) {
        self.directory = directory ?? FileManager.default.urls(for: .applicationSupportDirectory,
            in: .userDomainMask)[0].appendingPathComponent("NotchPrompter", isDirectory: true)
    }
    public func loadPreferences() -> Preferences {
        var result = (try? JSONDecoder().decode(Preferences.self,
            from: Data(contentsOf: preferencesURL))) ?? Preferences()
        result.normalize(); return result
    }
    public func loadScript() -> String { (try? String(contentsOf: scriptURL, encoding: .utf8)) ?? Script.welcome }
    public func save(_ preferences: Preferences, script: String) throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try script.write(to: scriptURL, atomically: true, encoding: .utf8)
        try JSONEncoder().encode(preferences).write(to: preferencesURL, options: .atomic)
    }
}
