/* NotchPrompter landing page.
   The hero island reuses the desktop app's behaviour: same shapes (collapsed, peek, open), the same
   under-damped springs, and pacing in words per minute. */
(function () {
  "use strict";

  var SCRIPT = [
    "# Hook",
    "Three things I wish I knew",
    "before my first 100 reels.",
    "",
    "# One",
    "Your first second is the *whole* video.",
    "Open on the payoff, not the intro.",
    "",
    "# Two",
    "Most people watch on mute,",
    "so put every line on screen.",
    "",
    "# Three",
    "Post, learn, repeat.",
    "See you in the next one!"
  ];

  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var isTouch = window.matchMedia && window.matchMedia("(hover: none)").matches;

  var screen = document.getElementById("screen");
  var island = document.getElementById("island");
  var peek = document.getElementById("peek");
  var prompt = document.getElementById("prompt");
  var promptText = document.getElementById("promptText");
  var countdownEl = document.getElementById("countdown");
  var statusEl = document.getElementById("status");
  var dock = document.getElementById("dock");
  var wpmEl = document.getElementById("wpm");
  var camDot = document.getElementById("camDot");
  var led = document.getElementById("led");
  var tryHint = document.getElementById("tryHint");
  var camToggle = document.getElementById("camToggle");

  // ---------------------------------------------------------------- springs
  function Spring(x, k, zeta) { this.x = x; this.v = 0; this.t = x; this.k = k; this.c = 2 * zeta * Math.sqrt(k); }
  Spring.prototype.step = function (dt) {
    if (this.x === this.t && this.v === 0) return false;
    this.v += (-this.k * (this.x - this.t) - this.c * this.v) * dt;
    this.x += this.v * dt;
    if (Math.abs(this.x - this.t) < 0.05 && Math.abs(this.v) < 0.6) { this.x = this.t; this.v = 0; return false; }
    return true;
  };
  function approach(cur, target, dt, rate) {
    if (Math.abs(cur - target) < 0.002) return target;
    return cur + (target - cur) * (1 - Math.exp(-rate * dt));
  }
  function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }
  function smooth(t) { return t * t * (3 - 2 * t); }

  // ---------------------------------------------------------------- state
  var S = { state: "collapsed", hover: false, playing: false, ended: false, countdown: 0, offset: 0, wpm: 140,
            camOn: false, autoOpened: false, labelA: 0, contentA: 0, opened: false, flash: null, flashUntil: 0 };
  var W = new Spring(184, 230, 0.68), H = new Spring(28, 210, 0.78), R = new Spring(12, 260, 0.9);
  var geo = {};   // sizes for the current screen width
  var words = 0, textH = 0, lineH = 30;

  function measure() {
    var sw = screen.clientWidth, sh = screen.clientHeight;
    var s = clamp(sw / 1000, 0.55, 1.08);
    geo = {
      sw: sw, sh: sh, s: s, cx: sw / 2,
      collapsed: [184 * s, 28 * s, 12 * s],
      peek: [300 * s, 38 * s, 17 * s],
      open: [Math.min(620 * s, sw - 24), Math.min(Math.max(200 * s, 170), sh - 96), 30 * s]
    };
    var fs = Math.max(15, 26 * s);
    lineH = Math.round(fs * 1.3);
    var ow = geo.open[0], oh = geo.open[1], left = geo.cx - ow / 2;
    prompt.style.left = (left + 26 * s) + "px";
    prompt.style.width = (ow - 52 * s) + "px";
    prompt.style.top = "0px";
    prompt.style.height = (oh - 6) + "px";
    promptText.style.fontSize = fs + "px";
    promptText.style.lineHeight = lineH + "px";
    Array.prototype.forEach.call(promptText.querySelectorAll(".lbl"), function (el) { el.style.fontSize = Math.max(10, fs * 0.5) + "px"; });
    countdownEl.style.fontSize = Math.max(40, 64 * s) + "px";
    countdownEl.style.top = (oh / 2 - Math.max(40, 64 * s) * 0.62) + "px";
    countdownEl.style.left = left + "px"; countdownEl.style.width = ow + "px"; countdownEl.style.right = "auto";
    statusEl.style.top = (oh - 56) + "px";
    statusEl.style.left = left + "px"; statusEl.style.width = ow + "px"; statusEl.style.right = "auto";
    dock.style.left = left + "px"; dock.style.width = ow + "px"; dock.style.top = (oh - 64) + "px";
    peek.style.height = geo.peek[1] + "px";
    textH = promptText.offsetHeight;
    retarget(true);
    S.offset = clamp(S.offset, 0, maxOffset());
  }

  function buildText() {
    var html = SCRIPT.map(function (line) {
      if (line.indexOf("#") === 0) return '<span class="lbl">' + esc(line.replace(/^#+\s*/, "")) + "</span>";
      if (!line.trim()) return '<span style="display:block;height:0.5em"></span>';
      return "<span style=\"display:block\">" + esc(line).replace(/\*(.+?)\*/g, "<em>$1</em>") + "</span>";
    }).join("");
    promptText.innerHTML = html;
    words = SCRIPT.filter(function (l) { return l.indexOf("#") !== 0; }).join(" ").split(/\s+/).filter(Boolean).length;
  }
  function esc(t) { return t.replace(/[&<>]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;" }[c]; }); }

  function maxOffset() { return Math.max(0, textH - lineH * 1.5); }
  function pps() { return words ? S.wpm / 60 * textH / words : 0; }

  function retarget(snap) {
    var t = geo[S.state === "open" ? "open" : S.state];
    W.t = t[0]; H.t = t[1]; R.t = t[2];
    if (snap && !ticking) { W.x = W.t; H.x = H.t; R.x = R.t; W.v = H.v = R.v = 0; draw(); }
  }

  function setState(st) {
    if (S.state === st) return;
    S.state = st;
    retarget(false);
    island.classList.toggle("show-dock", st === "open" && S.hover && !isTouch);
    if (st === "open" && !S.opened) { S.opened = true; tryHint.classList.add("hidden"); }
    labels();
    tick();
  }

  // ---------------------------------------------------------------- actions
  function expand(auto) { if (S.state !== "open") { S.autoOpened = !!auto; setState("open"); } }
  function collapse() { pause(); S.autoOpened = false; setState(S.hover && !isTouch ? "peek" : "collapsed"); }
  function togglePlay() {
    if (S.state !== "open") expand(false);
    if (S.countdown > 0) { S.countdown = 0; labels(); tick(); return; }
    if (S.playing) { pause(); return; }
    if (S.ended || S.offset >= maxOffset() - 0.5) { S.offset = 0; S.ended = false; }
    if (S.offset < 1 && !reduceMotion) S.countdown = 3; else S.playing = true;
    labels(); tick();
  }
  function pause() { if (!S.playing && S.countdown <= 0) return; S.playing = false; S.countdown = 0; labels(); tick(); }
  function restart() { S.offset = 0; S.playing = false; S.countdown = 0; S.ended = false; flash("Back to the top"); }
  function changeWpm(d) { S.wpm = clamp(S.wpm + d, 60, 300); flash(S.wpm + " words per minute"); }
  function flash(msg) { S.flash = msg; S.flashUntil = performance.now() + 1400; labels(); tick(); }

  function labels() {
    island.classList.toggle("playing", S.playing || S.countdown > 0);
    var status = S.flash || (S.countdown > 0 ? "" : S.ended ? "End of script · click to start again"
      : !S.playing ? "Paused · click the text or press Space" : "");
    statusEl.textContent = status;
    statusEl.style.opacity = status && !(S.hover && !isTouch) ? 1 : 0;
    updateWpm();
  }
  function updateWpm() {
    var p = pps(), txt = S.wpm + " wpm";
    if (p > 0) { var secs = Math.max(0, (maxOffset() - S.offset) / p); txt += " · " + Math.floor(secs / 60) + ":" + ("0" + Math.floor(secs % 60)).slice(-2); }
    if (wpmEl.textContent !== txt) wpmEl.textContent = txt;
  }

  // ---------------------------------------------------------------- frame loop
  var ticking = false, last = 0;
  function tick() { if (!ticking) { ticking = true; last = 0; requestAnimationFrame(frame); } }
  function frame(now) {
    var dt = last ? Math.min(0.05, (now - last) / 1000) : 1 / 60;
    last = now;
    var busy = false, steps = Math.max(1, Math.ceil(dt * 240)), sdt = dt / steps;
    for (var i = 0; i < steps; i++) { busy = W.step(sdt) | busy; busy = H.step(sdt) | busy; busy = R.step(sdt) | busy; }
    if (reduceMotion) { W.x = W.t; H.x = H.t; R.x = R.t; }

    if (S.flash && now > S.flashUntil) { S.flash = null; labels(); }
    busy = busy || !!S.flash;

    var la = approach(S.labelA, S.state === "peek" ? 1 : 0, dt, 18);
    var ca = approach(S.contentA, S.state === "open" ? 1 : 0, dt, S.state === "open" ? 9 : 24);
    busy = busy || la !== S.labelA || ca !== S.contentA;
    S.labelA = la; S.contentA = ca;

    if (S.countdown > 0) {
      busy = true; S.countdown -= dt;
      if (S.countdown <= 0) { S.countdown = 0; S.playing = true; labels(); }
    } else if (S.playing) {
      busy = true; S.offset += pps() * dt;
      if (S.offset >= maxOffset()) { S.offset = maxOffset(); S.playing = false; S.ended = true; labels(); }
      updateWpm();
    }
    draw();
    if (busy) requestAnimationFrame(frame); else ticking = false;
  }

  function path(w, h, r, e) {
    var cx = geo.cx, l = cx - w / 2, rt = cx + w / 2;
    r = clamp(r, 0, Math.min(h - 1, w / 2)); e = clamp(e, 0, h - r);
    return 'path("M ' + (l - e) + " 0 A " + e + " " + e + " 0 0 1 " + l + " " + e + " L " + l + " " + (h - r) +
      " A " + r + " " + r + " 0 0 0 " + (l + r) + " " + h + " L " + (rt - r) + " " + h + " A " + r + " " + r + " 0 0 0 " + rt + " " + (h - r) +
      " L " + rt + " " + e + " A " + e + " " + e + " 0 0 1 " + (rt + e) + ' 0 Z")';
  }

  function draw() {
    var w = Math.max(W.x, 30), h = Math.max(H.x, 10), s = geo.s;
    var p = clamp((h - geo.peek[1]) / Math.max(1, geo.open[1] - geo.peek[1]), 0, 1);
    island.style.clipPath = path(w, h, R.x, (7 + 3 * p) * s);
    peek.style.opacity = S.labelA * (1 - p);
    var show = S.contentA * smooth(p);
    prompt.style.opacity = show * (S.countdown > 0 ? 0.25 : 1);
    countdownEl.style.opacity = 0;
    if (S.countdown > 0) {
      var n = Math.ceil(S.countdown), t = 1 - (S.countdown - (n - 1));
      countdownEl.textContent = n;
      countdownEl.style.opacity = clamp(t * 6, 0, 1) * clamp((1 - t) * 4, 0, 1) * show;
      countdownEl.style.transform = "scale(" + (1 + 0.35 * Math.pow(1 - clamp(t * 2.5, 0, 1), 3)) + ")";
    }
    promptText.style.transform = "translateY(" + (-Math.round(S.offset)) + "px)";
    dock.style.visibility = show > 0.5 ? "visible" : "hidden";
    statusEl.style.visibility = show > 0.5 ? "visible" : "hidden";
    camDot.style.left = (geo.cx + w / 2 - (15 + 6 * p) * s - 3.5) + "px";
    camDot.style.top = (Math.min(h / 2, 19 * s) - 3.5) + "px";
  }

  // ---------------------------------------------------------------- input
  island.addEventListener("mouseenter", function () {
    if (isTouch) return;
    S.hover = true;
    if (S.state === "collapsed") setState("peek");
    island.classList.toggle("show-dock", S.state === "open");
    labels();
  });
  island.addEventListener("mouseleave", function () {
    if (isTouch) return;
    S.hover = false;
    if (S.state === "peek") setState("collapsed");
    island.classList.remove("show-dock");
    labels();
  });
  island.addEventListener("click", function (e) {
    var btn = e.target.closest("[data-act]");
    if (btn) {
      e.stopPropagation();
      var act = btn.getAttribute("data-act");
      if (act === "play") togglePlay();
      else if (act === "restart") restart();
      else if (act === "faster") changeWpm(10);
      else if (act === "slower") changeWpm(-10);
      else if (act === "collapse") collapse();
      return;
    }
    if (S.state !== "open") { expand(false); if (isTouch) togglePlay(); } else togglePlay();
  });
  island.addEventListener("keydown", function (e) {
    if (e.key === " " || e.key === "Enter") { e.preventDefault(); if (S.state !== "open") expand(false); else togglePlay(); }
    else if (e.key === "Escape") collapse();
  });
  island.addEventListener("wheel", function (e) {
    if (S.state !== "open") return;
    e.preventDefault();
    S.offset = clamp(S.offset + e.deltaY * 0.5, 0, maxOffset());
    if (S.offset < maxOffset()) S.ended = false;
    updateWpm(); draw();
  }, { passive: false });

  camToggle.addEventListener("click", function () {
    S.camOn = !S.camOn;
    camToggle.setAttribute("aria-pressed", String(S.camOn));
    led.classList.toggle("on", S.camOn);
    camDot.classList.toggle("on", S.camOn);
    if (S.camOn) expand(true); else if (S.autoOpened) collapse();
  });

  window.addEventListener("resize", function () { measure(); draw(); });

  if (isTouch) tryHint.innerHTML = "<span>&#8593;</span> Tap the notch to open it";

  buildText();
  measure();
  labels();
  draw();
  if ("fonts" in document) document.fonts.ready.then(function () { measure(); draw(); });

  // ---------------------------------------------------------------- downloads + video
  var isWindows = /Windows/i.test(navigator.userAgent);
  if (!isWindows) {
    var note = document.getElementById("os-note");
    if (note) note.textContent = "your scripts stay on your computer";
  }
  var toast = document.getElementById("toast"), toastTimer = 0;
  function showToast(msg) {
    toast.textContent = msg; toast.classList.add("show");
    clearTimeout(toastTimer); toastTimer = setTimeout(function () { toast.classList.remove("show"); }, 7000);
  }
  Array.prototype.forEach.call(document.querySelectorAll("[data-download]"), function (a) {
    a.addEventListener("click", function () {
      showToast(isWindows
        ? "Downloading NotchPrompter.exe. If Windows SmartScreen appears, click More info, then Run anyway."
        : "Downloading NotchPrompter.exe. It runs on Windows 10 and 11, so copy it to your Windows PC.");
    });
  });
  Array.prototype.forEach.call(document.querySelectorAll("[data-mac-download]"), function (a) {
    a.addEventListener("click", function () {
      showToast("Downloading the Mac beta. Unzip and drag NotchPrompter.app to Applications. macOS may block this unnotarized beta. Screen recording invisibility is not guaranteed.");
    });
  });

  var video = document.getElementById("howto"), userTouchedVideo = false;
  if (video) {
    ["play", "pause", "seeking", "volumechange"].forEach(function (ev) {
      video.addEventListener(ev, function (e) { if (e.isTrusted && document.activeElement === video) userTouchedVideo = true; });
    });
    if ("IntersectionObserver" in window && !reduceMotion) {
      new IntersectionObserver(function (entries) {
        entries.forEach(function (en) {
          if (userTouchedVideo) return;
          if (en.isIntersecting && en.intersectionRatio > 0.55) { var pr = video.play(); if (pr && pr.catch) pr.catch(function () {}); }
          else if (!video.paused) video.pause();
        });
      }, { threshold: [0, 0.55, 1] }).observe(video);
    }
  }
})();
