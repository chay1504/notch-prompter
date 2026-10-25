import XCTest
@testable import PrompterCore

final class CoreTests: XCTestCase {
    func testHeadingsAndEmphasisDoNotInflateSpeakingTime() {
        XCTAssertEqual(Script.wordCount("# Hook\r\nHello *world*.\r\n\r\n## CTA\nTry this now."), 5)
        XCTAssertEqual(Script.wordCount("# Heading only\n  \n***"), 0)
        XCTAssertEqual(Script.wordCount("తెలుగు స్క్రిప్ట్\nHello there"), 4)
    }
    func testPaceTracksLayoutRatherThanFixedPixels() {
        XCTAssertEqual(Script.pixelsPerSecond(height: 600, words: 100, wpm: 100), 10)
        XCTAssertEqual(Script.pixelsPerSecond(height: 1200, words: 100, wpm: 100), 20)
        XCTAssertEqual(Script.pixelsPerSecond(height: 600, words: 0, wpm: 100), 0)
    }
    func testCorruptPreferencesAreBounded() {
        var p = Preferences(); p.wpm = .nan; p.width = .infinity; p.height = -1; p.offset = -.infinity
        p.normalize()
        XCTAssertEqual(p.wpm, 40); XCTAssertEqual(p.width, 440)
        XCTAssertEqual(p.height, 130); XCTAssertEqual(p.offset, 0)
    }
    func testLocalStorageRoundTripAndEmptyScript() throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: folder) }
        let store = Store(directory: folder)
        XCTAssertEqual(store.loadScript(), Script.welcome)
        var p = Preferences(); p.wpm = 195; p.offset = 73
        try store.save(p, script: "తెలుగు *script*\n# End")
        XCTAssertEqual(store.loadPreferences().wpm, 195)
        XCTAssertEqual(store.loadPreferences().offset, 73)
        XCTAssertEqual(store.loadScript(), "తెలుగు *script*\n# End")
        try store.save(p, script: "")
        XCTAssertEqual(store.loadScript(), "")
        try Data("broken json".utf8).write(to: folder.appendingPathComponent("settings.json"))
        XCTAssertEqual(store.loadPreferences().wpm, 140)
    }
}
