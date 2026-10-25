
import math, os, sys, traceback
from pathlib import Path
import numpy as np
import soundfile as sf
from transformers import MusicgenForConditionalGeneration, AutoProcessor

prompt = "uplifting minimal electronic, soft plucked synth arpeggio, warm pads, light clean percussion, bright modern tech product demo, 108 bpm, steady energy, no vocals"
out_path = "C:/Users/hp/Downloads/xyz/notch-prompter-video/assets/bgm/track.wav"
target_s = float(46.000)
seed_s = float(14.000)
token_rate = 50
crossfade_s = 0.6

def apply_fade(arr, sr, fade_in_s=0.08, fade_out_s=0.5):
    n_in = min(int(round(fade_in_s * sr)), arr.shape[0] // 2)
    n_out = min(int(round(fade_out_s * sr)), arr.shape[0] // 2)
    if n_in > 1: arr[:n_in] *= np.linspace(0.0, 1.0, n_in, dtype="float32")
    if n_out > 1: arr[-n_out:] *= np.linspace(1.0, 0.0, n_out, dtype="float32")
    return arr

def loop_crossfade(seed, target_len, xf):
    if seed.shape[0] >= target_len: return seed[:target_len]
    xf = min(xf, seed.shape[0] // 2)
    if xf < 1:
        reps = int(math.ceil(target_len / seed.shape[0]))
        return np.tile(seed, reps)[:target_len]
    t = np.linspace(0.0, 1.0, xf, dtype="float32")
    fade_out = np.cos(t * (math.pi / 2)); fade_in = np.sin(t * (math.pi / 2))
    out = seed.copy()
    while out.shape[0] < target_len:
        tail = out[-xf:] * fade_out; head = seed[:xf] * fade_in
        out = np.concatenate([out[:-xf], tail + head, seed[xf:]])
    return out[:target_len]

try:
    Path(os.path.dirname(out_path)).mkdir(parents=True, exist_ok=True)
    processor = AutoProcessor.from_pretrained("facebook/musicgen-small")
    model = MusicgenForConditionalGeneration.from_pretrained("facebook/musicgen-small")
    model.eval()
    sr = int(model.config.audio_encoder.sampling_rate)
    gen_s = min(seed_s, target_s)
    tokens = max(1, int(math.ceil(gen_s * token_rate)))
    print(f"[musicgen] seed dur={gen_s:.2f}s tokens={tokens}", flush=True)
    inputs = processor(text=[prompt], padding=True, return_tensors="pt")
    import torch
    torch.set_num_threads(4)
    with torch.inference_mode():
        audio = model.generate(**inputs, max_new_tokens=tokens)
    seed = audio[0, 0].detach().cpu().numpy().astype("float32")
    peak = float(np.max(np.abs(seed)))
    if peak > 1e-6: seed = seed * (0.89 / peak)
    want = max(1, int(round(target_s * sr)))
    if seed.shape[0] >= want:
        final = seed[:want].copy()
    else:
        final = loop_crossfade(seed, want, int(round(crossfade_s * sr)))
    if final.shape[0] < want: final = np.pad(final, (0, want - final.shape[0]))
    else: final = final[:want]
    final = apply_fade(final, sr)
    peak = float(np.max(np.abs(final)))
    if peak > 1.0: final = final / peak
    sf.write(out_path, final, sr)
    print(f"[musicgen] wrote {out_path} samples={final.shape[0]} sr={sr}", flush=True)
except Exception:
    traceback.print_exc(); sys.exit(1)
