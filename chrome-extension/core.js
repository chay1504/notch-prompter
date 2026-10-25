(function () {
  "use strict";
  const defaults = Object.freeze({
    script: "# Your next take\nKeep your eyes up. Your script is right here.\n\n# Get started\nPaste your own script in the extension editor.\nUse *asterisks* to highlight words and # for section headings.\n\n# Recording\nThis browser overlay is visible in tab recordings and screen sharing.",
    wpm: 140, fontSize: 28, width: 620, height: 220, countdown: true, center: true
  });
  const clamp = (value, low, high, fallback) => Number.isFinite(Number(value))
    ? Math.min(high, Math.max(low, Number(value))) : fallback;
  function normalize(raw = {}) {
    return {
      script: typeof raw.script === "string" ? raw.script : defaults.script,
      wpm: clamp(raw.wpm, 40, 400, defaults.wpm),
      fontSize: clamp(raw.fontSize, 16, 72, defaults.fontSize),
      width: clamp(raw.width, 320, 1400, defaults.width),
      height: clamp(raw.height, 140, 700, defaults.height),
      countdown: typeof raw.countdown === "boolean" ? raw.countdown : defaults.countdown,
      center: typeof raw.center === "boolean" ? raw.center : defaults.center
    };
  }
  function lines(text) {
    return text.replace(/\r\n?/g, "\n").split("\n").map(text => {
      const heading = /^\s*#/.test(text);
      return {heading, text: heading ? text.replace(/^\s*#+\s*/, "") : text};
    });
  }
  function wordCount(text) {
    return lines(text).filter(line => !line.heading).reduce((count, line) =>
      count + line.text.replace(/\*/g, "").split(/\s+/u).filter(Boolean).length, 0);
  }
  globalThis.NotchPrompterCore = Object.freeze({defaults, normalize, lines, wordCount});
})();
