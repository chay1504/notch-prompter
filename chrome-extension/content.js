(function () {
  "use strict";
  if (globalThis.__notchPrompterInstalled) return;
  globalThis.__notchPrompterInstalled = true;
  const Core = NotchPrompterCore;
  const host = document.createElement("notch-prompter-overlay");
  host.setAttribute("aria-label", "NotchPrompter");
  host.hidden = true;
  for (const [property, value] of Object.entries({all: "initial", position: "fixed", top: "0", left: "50%",
    transform: "translateX(-50%)", zIndex: "2147483647", width: "max-content", maxWidth: "100vw"})) {
    host.style.setProperty(property.replace(/[A-Z]/g, letter => "-" + letter.toLowerCase()), value, "important");
  }
  const shadow = host.attachShadow({mode: "open"});
  const style = document.createElement("style");
  style.textContent = `
    :host([hidden]){display:none!important}
    *,*::before,*::after{box-sizing:border-box}
    .island{position:relative;width:min(var(--width,620px),calc(100vw - 24px));height:var(--height,220px);background:#000;color:#F5F5F7;border-radius:0 0 25px 25px;box-shadow:0 10px 28px #0004;overflow:hidden;font:14px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;color-scheme:dark;isolation:isolate}
    .island[data-mode=collapsed]{width:184px;height:28px;border-radius:0 0 12px 12px;cursor:pointer}
    .island[data-mode=collapsed]:hover,.island[data-mode=collapsed]:focus-within{width:300px;height:38px}
    .peek{display:none;width:100%;height:100%;border:0;color:#EEE;background:none;font:600 12px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;cursor:pointer}
    .island[data-mode=collapsed] .peek{display:block}
    .island[data-mode=collapsed] .viewport,.island[data-mode=collapsed] .toolbar,.island[data-mode=collapsed] .grip,.island[data-mode=collapsed] .progress{display:none}
    .viewport{position:absolute;inset:12px 26px 58px;overflow:hidden;cursor:pointer;outline-offset:-2px}
    .script{font-size:var(--font,28px);font-weight:600;line-height:1.3;text-align:var(--align,center);overflow-wrap:anywhere;will-change:transform;padding-bottom:2px}
    .line{min-height:1.3em;white-space:pre-wrap}
    .heading{color:#64D2FF;text-transform:uppercase;font-size:.5em;letter-spacing:.06em;line-height:2}
    mark{background:none;color:#FFD60A;font:inherit}
    .toolbar{position:absolute;inset:auto 12px 15px;display:flex;align-items:center;gap:3px;justify-content:center;height:32px}
    button{font:500 14px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;cursor:pointer;min-width:30px;height:30px;padding:0 8px;border:0;border-radius:8px;background:transparent;color:#F5F5F7}
    button:hover{background:#FFFFFF24}
    button:focus-visible,.viewport:focus-visible{outline:2px solid #64D2FF;outline-offset:1px}
    .play{background:#F5F5F7;color:#000;border-radius:15px}
    .play:hover{background:#D9D9DD}
    .speed{font-size:11px;color:#AAA;min-width:65px;text-align:center;font-variant-numeric:tabular-nums}
    .progress{position:absolute;height:2px;background:#64D2FF;bottom:9px;left:26px;width:0;max-width:calc(100% - 52px)}
    .grip{position:absolute;bottom:0;left:calc(50% - 22px);width:44px;min-width:44px;height:9px;cursor:nwse-resize;padding:0;touch-action:none}
    .grip::after{content:'';position:absolute;top:3px;left:6px;width:32px;height:2px;background:#888;border-radius:2px}
    .counter{position:absolute;inset:0;background:#000D;display:flex;align-items:center;justify-content:center;font-size:52px;font-weight:700;pointer-events:none}
    .counter[hidden]{display:none}
    @media (prefers-reduced-motion:reduce){.script{will-change:auto}}
  `;
  const sheet = new CSSStyleSheet(); sheet.replaceSync(style.textContent);
  shadow.adoptedStyleSheets = [sheet];
  const island = document.createElement("section");
  island.className = "island"; island.dataset.mode = "collapsed";
  island.setAttribute("aria-label", "Teleprompter");
  // DOM methods avoid HTML parsing and support pages enforcing Trusted Types.
  function element(tag, attributes = {}, text = "") {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    node.textContent = text; return node;
  }
  const peek = element("button", {class:"peek", "aria-label":"Open teleprompter"}, "Teleprompter · click to open");
  const prompt = element("div", {class:"viewport", role:"button", tabindex:"0", "aria-label":"Play or pause script", "aria-pressed":"false"});
  prompt.append(element("div", {class:"script"}));
  const toolbar = element("div", {class:"toolbar", "aria-label":"Teleprompter controls"});
  for (const [action, label, glyph] of [["restart","Back to top","↺"],["slower","Slower","−"],
    ["faster","Faster","+"],["play","Play or pause","▶"],["edit","Edit script","Edit"],
    ["collapse","Collapse teleprompter","⌃"],["hide","Hide teleprompter","×"]]) {
    if (action === "faster") toolbar.append(element("span", {class:"speed"}));
    toolbar.append(element("button", {"data-action":action,"aria-label":label,...(action === "play" ? {class:"play"} : {})}, glyph));
  }
  island.append(peek, prompt, toolbar, element("div", {class:"progress"}),
    element("button", {class:"grip", "aria-label":"Resize teleprompter. Use arrow keys; Shift plus arrows changes width."}),
    element("div", {class:"counter", "aria-live":"polite", hidden:""}));
  shadow.append(island); document.documentElement.append(host);
  const viewport = shadow.querySelector(".viewport");
  const text = shadow.querySelector(".script");
  const counter = shadow.querySelector(".counter");
  let preferences = Core.normalize();
  let speakingWords = 0;
  let offset = 0, playing = false, countdown = 0, frame = 0, last = 0, resize = null;
  let saveTimer;
  function maxOffset() { return Math.max(0, text.scrollHeight - preferences.fontSize * 1.3); }
  function state() { return {visible: !host.hidden, expanded: island.dataset.mode === "expanded", playing: playing || countdown > 0, offset}; }
  function display() {
    text.style.transform = "translateY(" + -offset + "px)";
    island.dataset.offset = String(offset);
    island.dataset.playing = String(playing || countdown > 0);
    viewport.setAttribute("aria-pressed", String(playing || countdown > 0));
    shadow.querySelector(".play").textContent = playing || countdown > 0 ? "Ⅱ" : "▶";
    shadow.querySelector(".speed").textContent = preferences.wpm + " wpm";
    shadow.querySelector(".progress").style.width = "calc((100% - 52px) * " + offset / Math.max(1, maxOffset()) + ")";
    counter.hidden = countdown <= 0;
    const number = String(Math.ceil(countdown));
    if (!counter.hidden && counter.textContent !== number) counter.textContent = number;
  }
  function pause() {
    playing = false; countdown = 0; cancelAnimationFrame(frame); frame = 0; display();
  }
  function open() { host.hidden = false; island.dataset.mode = "expanded"; display(); }
  function collapse() { pause(); host.hidden = false; island.dataset.mode = "collapsed"; }
  function restart() { pause(); offset = 0; display(); }
  function tick(now) {
    const delta = last ? Math.min(.1, (now - last) / 1000) : 0; last = now;
    if (countdown > 0) { countdown = Math.max(0, countdown - delta); playing = countdown === 0; }
    else if (playing) {
      offset = Math.min(maxOffset(), offset + (speakingWords ? maxOffset() * preferences.wpm / speakingWords / 60 : 0) * delta);
      if (offset >= maxOffset()) { pause(); return; }
    }
    display(); frame = requestAnimationFrame(tick);
  }
  function play() {
    if (playing || countdown > 0) { pause(); return; }
    open(); if (!speakingWords) return;
    if (offset >= maxOffset()) offset = 0;
    countdown = preferences.countdown && offset === 0 ? 3 : 0;
    playing = countdown === 0; last = 0; display(); frame = requestAnimationFrame(tick);
  }
  function apply(raw, resetScript = false) {
    const old = preferences;
    const progress = offset / Math.max(1, maxOffset());
    preferences = Core.normalize(raw);
    island.style.setProperty("--width", preferences.width + "px");
    island.style.setProperty("--height", preferences.height + "px");
    island.style.setProperty("--font", preferences.fontSize + "px");
    island.style.setProperty("--align", preferences.center ? "center" : "left");
    if (resetScript || old.script !== preferences.script) {
      speakingWords = Core.wordCount(preferences.script);
      pause(); text.replaceChildren();
      for (const line of Core.lines(preferences.script)) {
        const block = document.createElement("div"); block.className = line.heading ? "line heading" : "line";
        for (const [index, part] of line.text.split("*").entries()) {
          const run = document.createElement(index % 2 && !line.heading ? "mark" : "span");
          run.textContent = part; block.append(run);
        }
        text.append(block);
      }
      offset = 0;
    } else if (old.width !== preferences.width || old.fontSize !== preferences.fontSize) {
      offset = progress * maxOffset();
    }
    display();
  }
  function persist(values) {
    chrome.storage.local.set(values).catch(() => { /* The editor surfaces storage errors. */ });
  }
  const ready = chrome.storage.local.get(Core.defaults).then(values => apply(values, true));
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local") return;
    const update = {...preferences};
    for (const key of Object.keys(Core.defaults)) if (key in changes) {
      update[key] = changes[key].newValue ?? Core.defaults[key];
    }
    apply(update);
  });
  chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (sender.id !== chrome.runtime.id) return false;
    if (message?.type === "notch-ping") { respond({ok: true}); return false; }
    if (message?.type !== "notch-control") return false;
    ready.then(() => {
      switch (message.action) {
      case "show": open(); break;
      case "toggle": state().expanded && !host.hidden ? collapse() : open(); break;
      case "play": play(); break;
      case "restart": restart(); break;
      case "hide": pause(); host.hidden = true; break;
      }
      respond(state());
    });
    return true;
  });
  shadow.querySelector(".peek").addEventListener("click", open);
  viewport.addEventListener("click", play);
  viewport.addEventListener("keydown", event => {
    if ([" ", "Enter"].includes(event.key)) { event.preventDefault(); play(); }
  });
  shadow.querySelector(".toolbar").addEventListener("click", event => {
    const action = event.target.closest("button")?.dataset.action;
    if (action === "play") play();
    else if (action === "restart") restart();
    else if (action === "collapse") collapse();
    else if (action === "hide") { pause(); host.hidden = true; }
    else if (action === "edit") chrome.runtime.sendMessage({type: "notch-editor"}).catch(() => {});
    else if (["faster", "slower"].includes(action)) {
      const next = Core.normalize({...preferences, wpm: preferences.wpm + (action === "faster" ? 10 : -10)});
      apply(next); persist({wpm: preferences.wpm});
    }
  });
  island.addEventListener("keydown", event => { event.stopPropagation(); if (event.key === "Escape") collapse(); });
  island.addEventListener("wheel", event => {
    if (island.dataset.mode !== "expanded") return;
    event.preventDefault(); event.stopPropagation();
    if (event.ctrlKey || event.shiftKey) {
      const key = event.ctrlKey ? "fontSize" : "wpm";
      apply({...preferences, [key]: preferences[key] + (event.deltaY < 0 ? 1 : -1) * (event.ctrlKey ? 1 : 5)});
      clearTimeout(saveTimer); saveTimer = setTimeout(() => persist({[key]: preferences[key]}), 120);
    } else { offset = Math.min(maxOffset(), Math.max(0, offset + event.deltaY)); display(); }
  }, {passive: false});
  const grip = shadow.querySelector(".grip");
  grip.addEventListener("pointerdown", event => {
    resize = {x: event.clientX, y: event.clientY, width: island.clientWidth, height: island.clientHeight};
    grip.setPointerCapture(event.pointerId); event.preventDefault();
  });
  grip.addEventListener("pointermove", event => {
    if (!resize) return;
    apply({...preferences, width: resize.width + 2 * (event.clientX - resize.x), height: resize.height + event.clientY - resize.y});
  });
  function endResize() { if (resize) persist({width: preferences.width, height: preferences.height}); resize = null; }
  grip.addEventListener("pointerup", endResize); grip.addEventListener("pointercancel", endResize);
  grip.addEventListener("keydown", event => {
    if (!["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)) return;
    event.preventDefault();
    const key = event.shiftKey || ["ArrowLeft", "ArrowRight"].includes(event.key) ? "width" : "height";
    apply({...preferences, [key]: preferences[key] + (["ArrowDown", "ArrowRight"].includes(event.key) ? 20 : -20)});
    persist({[key]: preferences[key]});
  });
  document.addEventListener("visibilitychange", () => { if (document.hidden) pause(); });
  window.addEventListener("resize", () => { offset = Math.min(offset, maxOffset()); display(); });
})();
