import AppKit
import Carbon

final class Hotkeys {
    private var handler: EventHandlerRef?
    private var references: [EventHotKeyRef] = []
    private var actions: [UInt32: () -> Void] = [:]
    private(set) var failures: [String] = []
    init() {
        var event = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let callback: EventHandlerUPP = { _, event, context in
            guard let event = event, let context = context else { return OSStatus(eventNotHandledErr) }
            var key = EventHotKeyID()
            let status = GetEventParameter(event, EventParamName(kEventParamDirectObject),
                EventParamType(typeEventHotKeyID), nil, MemoryLayout<EventHotKeyID>.size, nil, &key)
            guard status == noErr else { return status }
            let owner = Unmanaged<Hotkeys>.fromOpaque(context).takeUnretainedValue()
            owner.actions[key.id]?()
            return noErr
        }
        InstallEventHandler(GetApplicationEventTarget(), callback, 1, &event,
            Unmanaged.passUnretained(self).toOpaque(), &handler)
    }
    func register(_ code: Int, label: String, action: @escaping () -> Void) {
        let id = UInt32(actions.count + failures.count + 1)
        var reference: EventHotKeyRef?
        let status = RegisterEventHotKey(UInt32(code), UInt32(controlKey | optionKey),
            EventHotKeyID(signature: 0x4E50524D, id: id), GetApplicationEventTarget(), 0, &reference)
        if status == noErr, let reference = reference {
            references.append(reference); actions[id] = action
        } else { failures.append(label) }
    }
    deinit {
        references.forEach { UnregisterEventHotKey($0) }
        if let handler = handler { RemoveEventHandler(handler) }
    }
}
