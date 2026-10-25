(async function () {
  "use strict";
  const Core = NotchPrompterCore;
  const $ = id => document.getElementById(id);
  let preferences = Core.normalize(await chrome.storage.local.get(Core.defaults));
  let saveTimer;
  let saveChain = Promise.resolve();
  const query = new URLSearchParams(location.search);
  if (query.get("full") === "1") document.body.classList.add("full");
  const requestedTab = Number(query.get("tab"));
  const active = (await chrome.tabs.query({active: true, currentWindow: true}))[0];
  const tabId = query.has("tab") && Number.isInteger(requestedTab) ? requestedTab : active?.id;
  $("script").value = preferences.script;
  $("wpm").value = preferences.wpm;
  $("font").value = preferences.fontSize;
  $("countdown").checked = preferences.countdown;
  $("center").checked = preferences.center;
  function status(text, error = false) {
    $("status").textContent = text; $("status").classList.toggle("error", error);
  }
  function labels() {
    const words = Core.wordCount($("script").value);
    $("stats").textContent = words + " words · ~" + Math.ceil(words / Number($("wpm").value) * 60) + "s";
    $("wpm-value").textContent = $("wpm").value + " wpm";
    $("font-value").textContent = $("font").value + " px";
  }
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local") return;
    const controls = {script:"script", wpm:"wpm", fontSize:"font", countdown:"countdown", center:"center"};
    for (const [key, id] of Object.entries(controls)) {
      const input = $(id);
      if (!(key in changes) || document.activeElement === input) continue;
      const value = changes[key].newValue ?? Core.defaults[key];
      if (input.type === "checkbox") input.checked = value;
      else input.value = value;
    }
    labels();
  });
  function save() {
    clearTimeout(saveTimer);
    // Only write fields this editor owns: preserve the overlay's saved width and height.
    const values = {script: $("script").value, wpm: Number($("wpm").value),
      fontSize: Number($("font").value), countdown: $("countdown").checked, center: $("center").checked};
    preferences = Core.normalize({...preferences, ...values});
    saveChain = saveChain.catch(() => {}).then(() => chrome.storage.local.set(values));
    return saveChain.then(() => status("Saved on this computer."))
      .catch(error => { status("Could not save: " + error.message, true); throw error; });
  }
  for (const id of ["script", "wpm", "font", "countdown", "center"]) {
    $(id).addEventListener("input", () => {
      labels(); status("Saving…"); clearTimeout(saveTimer);
      // A popup can close at any moment. Save each edit rather than relying on unload.
      save().catch(() => {});
    });
  }
  async function request(action) {
    try {
      await save();
      const response = await chrome.runtime.sendMessage({type: "notch-request", tabId, action});
      if (!response?.ok) throw new Error(response?.error || "Open a normal website and try again.");
      status(action === "hide" ? "Hidden on this tab." : response.playing ? "Playing on your selected tab." : "Ready on your selected tab.");
    } catch (error) { status(error.message, true); }
  }
  for (const action of ["show", "play", "restart", "hide"]) $(action).addEventListener("click", () => request(action));
  $("full").addEventListener("click", async () => {
    try {
      await save();
      await chrome.tabs.create({url: chrome.runtime.getURL("editor.html") + "?full=1&tab=" + tabId});
    } catch (error) { status(error.message, true); }
  });
  $("import").addEventListener("click", () => $("file").click());
  $("file").addEventListener("change", async () => {
    const file = $("file").files[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) { status("Choose a text file smaller than 2 MB.", true); return; }
    try { $("script").value = await file.text(); labels(); await save(); }
    catch (error) { status("Could not import: " + error.message, true); }
    $("file").value = "";
  });
  labels();
  status("Saved on this computer.");
  $("editor-controls").disabled = false;
  const unassigned = (await chrome.commands.getAll().catch(() => [])).filter(command => command.name !== "_execute_action" && !command.shortcut);
  if (unassigned.length) {
    $("shortcuts-notice").hidden = false;
    $("shortcuts-notice").textContent = "Some shortcuts are unavailable. Assign them at chrome://extensions/shortcuts. The on-screen controls still work.";
  }
})().catch(error => {
  const message = document.getElementById("status");
  message.textContent = "Could not load the editor. Reopen the extension and try again. " + error.message;
  message.classList.add("error");
});
