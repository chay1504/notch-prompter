GOAL:    A Windows creator understands they can read their reel script from a notch right under the webcam,
         drive it with shortcuts, and that it never shows in a recording — then wants the download.
OUTPUT:  HyperFrames, 1920 x 1080, 30 fps
TIME:    45 s x 30 = 1350 frames. Music: local MusicGen bed, ~108 bpm (prompted), 30 s seed looped to 46 s.

LOOK:    ground = Windows desktop, flat #E9E6E1 with one radial lift #F5F3EF at top-centre (r 1100 px), no grain.
         bezel = matte black band #0D0D0F, 92 px tall, 1 px #26262A lower edge; webcam lens at (960,46):
         ring 36 px #1D1D21, glass 20 px #050507, glint 6 px #44506A at (-4,-4); LED 6 px #2A2A2E (on: #F2F2F2).
         island = the app's own geometry at 1.6x: pure #000, concave ears, spring morphs (ζ 0.68).
           collapsed 294x45 r19 e11 · peek 480x61 r27 e12 · expanded 992x320 r48 e16, top edge at y 92.
         ink #111111, secondary #55555B; accent #FFD60A only on the highlighted script word and one title
         word per shot; #30D158 only for the camera dot; #FF3B30 only for REC; #64D2FF only for script
         section labels (as in the app). Editor UI uses the app's own colours (#161618 / #1F1F22 / #0A84FF).
         type = Segoe UI (local, the product's own face): titles 700 76 px tracking -1.5 %, sub-lines 600 40 px,
         island text 600 45 px / 58 px line, labels in the island 600 20 px.
         depth: windows cast 0 30px 70px rgba(0,0,0,.20); keycaps have a 6 px #CFCBC4 under-edge.
OBJECTS: laptop top bezel with a real-looking webcam (ring, glass, glint, LED); the island; a Notepad-style
         script window (title bar "reel-script.txt - Notepad"); the NotchPrompter editor window; a Windows
         arrow cursor; physical keycaps (Ctrl, Alt, Space, =, V); a recorder window with a REC badge and timer;
         the app icon (dark tile, notch with green dot, one yellow + two grey text lines).
MOTION:  island = closed-form spring ease ζ 0.68 over 0.8 s (product behaviour); titles: per-word blur 6px→0 +
         opacity in 0.13 s, 0.067 s apart, no translate; exits blur-out 0.2 s; cursor moves power3.inOut 0.7–1.0 s;
         windows enter scale 0.06→1 expo.out 0.6 s from the control that opened them, exit power3.in 0.35 s;
         camera pull-back power2.inOut 0.9 s; keycap press = 0.06 s down 6 px, 0.12 s up.
SHOTS:   f0–156    hook: lens + script window far below, dashed sight-line lens→window, "Reading your script on
                   camera?" then "Your eyes leave the lens" (accent "leave"); window flies up into the notch (whoosh).
         f156–312  reveal: notch gulps, springs open with the script; "NotchPrompter" per letter, "Your script, right
                   under the camera"; collapses.
         f312–624  controls: cursor hovers → peek "Teleprompter Ctrl+Alt+T" ("Hover to peek"), click → opens
                   ("Click to open"), pencil → editor grows out of it, Ctrl+V paste, typing — island updates live
                   ("Paste your script. It updates live."); editor returns into the pencil.
         f624–978  play: Ctrl+Alt+Space keys, 3-2-1 in the island, text scrolls ("Press play"); Ctrl+Alt+= twice,
                   HUD "150 → 160 words per minute" ("Set your pace"); island closes, LED + green dot, it reopens
                   by itself ("Camera on? It opens itself").
         f978–1200 payoff: pull back into "Your screen" pane; "Your recording" pane beside it with REC 00:12 and no
                   island; riser → impact; "On your screen. Never in your recording."
         f1200–1350 end card: app icon, "NotchPrompter", "Starts with Windows · Free for Windows 10 and 11",
                   black "Download for Windows" button; holds 2.6 s; music fades 1.5 s.
BANNED:  dark void ground; glow/bloom/particles; purple or blue gradients; glass cards; cream+serif+mono labels;
         numbered step labels; everything centred; every element fading and sliding up; titles moving after they
         land; three cards in a row; docked logo; click rings; corner HUD decoration.
QA:      t=0.2 (thumbnail: bezel, window, sight-line visible) · 3.0 · 6.8 (island open + title) · 12.2 (peek) ·
         16.6 (editor + live island) · 24.0 (countdown) · 27.0 (HUD) · 30.5 (green dot reopen) · 36.0 (two panes) ·
         43.5 (end card complete).
