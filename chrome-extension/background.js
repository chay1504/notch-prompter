"use strict";
importScripts("core.js");

function pageError(error) {
  const text = String(error?.message || error);
  if (/cannot access|chrome:\/\/|extensions gallery|missing host permission|cannot be scripted/i.test(text)) {
    return "Open a normal website first. Chrome settings, the Web Store, built-in PDF viewer and other protected pages cannot show the overlay.";
  }
  return "Could not reach this page. Reload the website, open NotchPrompter again, and choose Show on this tab.";
}

async function control(tabId, action) {
  const allowed = ["show", "toggle", "play", "pause", "restart", "hide", "state"];
  if (!Number.isInteger(tabId) || !allowed.includes(action)) throw new Error("Invalid teleprompter command");
  try {
    try { await chrome.tabs.sendMessage(tabId, {type: "notch-ping"}); }
    catch {
      await chrome.scripting.executeScript({target: {tabId}, files: ["core.js", "content.js"]});
    }
    if (["show", "toggle", "play"].includes(action)) await chrome.tabs.update(tabId, {active:true});
    return await chrome.tabs.sendMessage(tabId, {type: "notch-control", action});
  } catch (error) { throw new Error(pageError(error)); }
}

// Browser activation events also cover environments where page visibility is delayed.
chrome.tabs.onActivated.addListener(async ({tabId, windowId}) => {
  const tabs = await chrome.tabs.query({windowId});
  await Promise.allSettled(tabs.filter(tab => tab.id !== tabId).map(tab =>
    chrome.tabs.sendMessage(tab.id, {type:"notch-control", action:"pause"})));
});

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id) return false;
  if (message?.type === "notch-editor") {
    const tabId = sender.tab?.id;
    if (!Number.isInteger(tabId)) return false;
    chrome.tabs.create({url: chrome.runtime.getURL("editor.html") + "?full=1&tab=" + tabId})
      .then(() => respond({ok: true})).catch(() => respond({ok: false}));
    return true;
  }
  if (message?.type !== "notch-request") return false;
  control(message.tabId, message.action).then(state => respond({ok: true, ...state}))
    .catch(error => respond({ok: false, error: error.message}));
  return true;
});

chrome.commands.onCommand.addListener(async (command, tab) => {
  if (!["toggle", "play", "restart"].includes(command)) return;
  const active = tab || (await chrome.tabs.query({active: true, currentWindow: true}))[0];
  if (!active?.id) return;
  try {
    await control(active.id, command);
    await chrome.action.setBadgeText({tabId: active.id, text: ""});
  } catch {
    await chrome.action.setBadgeBackgroundColor({color: "#B13B2E"});
    await chrome.action.setBadgeText({tabId: active.id, text: "!"});
    await chrome.action.setTitle({tabId: active.id, title: "NotchPrompter: open a normal web page, then try again"});
  }
});
