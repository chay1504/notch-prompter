# Review — NotchPrompter how-to (45 s, 1920x1080, 30 fps)

## Creative calls
- Storyline A "Eyes on the lens" (one stage that rebuilds): the laptop's top bezel and webcam stay put and the
  island morphs through every feature, using the app's own geometry at 1.6x and its under-damped spring.
- Not built: B "Two screens" (split screen the whole way through) and C "The keyboard plays it" (giant keycaps
  cut on the beat). Both are in STORYLINES.md.
- The UI is recreated in HTML, not screen-captured, because real captures of this PC show private windows.
  The island, the editor, the tray-free look and Segoe UI match the shipped app.
- Light warm desktop ground instead of a dark void, so the pure-black island reads as the hero.

## Measured
- `hyperframes check`: passes with no errors. Kept warnings: product-UI text under 28 px (Notepad title bar,
  editor chrome, island section labels at 22 px); these are realistic UI details, not lines meant to be read.
  The island script lines trip `line_unread` because they scroll, which is the point of a teleprompter.
- Seams checked at every cut (`snapshot --review`): the island's shape and text position match across all cuts.
- Sound: placed on visible actions only (fly-up whoosh, clicks, key presses, camera pop, riser into the
  payoff impact, download click). Measured, not heard.

## Weakest shot
- The first 0.6 s of the controls scene (t≈10.4) is mostly empty ground while the cursor travels in.
  With more time I'd carry the cursor in from the end of the reveal.

## Music
- Not included. Local MusicGen (facebook/musicgen-small) ran out of RAM three times on this laptop (other apps were
  using ~13 GB), so the film ships with sound effects only. To add a bed later: drop a track at assets/bgm/track.wav and add
  `<audio id="bgm" src="assets/bgm/track.wav" data-start="0" data-duration="45" data-volume="0.5" data-fade-in="0.4" data-fade-out="1.5">`
  to index.html, then re-render.
