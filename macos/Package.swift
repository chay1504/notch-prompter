// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "NotchPrompter",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "NotchPrompter", targets: ["NotchPrompter"])],
    targets: [
        .target(name: "PrompterCore"),
        .executableTarget(name: "NotchPrompter", dependencies: ["PrompterCore"],
            linkerSettings: [.linkedFramework("AppKit"), .linkedFramework("Carbon"),
                             .linkedFramework("AVFoundation"), .linkedFramework("ServiceManagement")]),
        .testTarget(name: "PrompterCoreTests", dependencies: ["PrompterCore"])
    ]
)
