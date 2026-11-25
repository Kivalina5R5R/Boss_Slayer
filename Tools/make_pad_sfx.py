# Select Player pad cues for Boss Slayer, made in code with no external samples.
# Built the way FoodIsekaiZ builds its Ready cues (Tools/Audio/GenerateIntroSounds.py there): a few small
# instruments, short pitched cues in the key of the music under them, a room echo, and a loudness target per cue.
# Here the instruments are dungeon ones (iron bell, anvil clang, stone tap, war drum, grinding stone) and the room
# is a stone hall. The select music (bs_menu) is in E minor, so pitched notes come only from E minor.
# Run from the project root: python Tools/make_pad_sfx.py
# Writes Assets/Resources/BossFightSfx/pad_*.wav; existing .meta GUIDs are kept, new cues get a fresh .meta.

import re
import uuid
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 48000
FOLDER = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "BossFightSfx"
rng = np.random.default_rng(25112026)

# E minor, the key of bs_menu.
E2, B2 = 82.41, 123.47
E3, G3, B3 = 164.81, 196.0, 246.94
D4, E4, FS4, G4, A4, B4 = 293.66, 329.63, 369.99, 392.0, 440.0, 493.88
E5, G5, B5 = 659.26, 783.99, 987.77


def lowpass(signal, cutoff):
    return sosfilt(butter(2, cutoff, "low", fs=RATE, output="sos"), signal)


def bandpass(signal, low, high):
    return sosfilt(butter(2, [low, high], "band", fs=RATE, output="sos"), signal)


def add(out, start, piece):
    first = int(start * RATE)
    n = min(len(piece), len(out) - first)
    out[first:first + n] += piece[:n]


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def edges(t, length, attack=.003, tail=.04):
    return np.clip(t / attack, 0, 1) * np.clip((length - t) / tail, 0, 1)


# Pitched voices, each a few partials with their own decay (the FoodIsekai tone() idea, dungeon timbres):
# iron = a dark bell, clang = struck anvil (inharmonic, bright, short), tap = stone knock (almost no ring).
VOICES = {
    "iron": [(1, 1, 1), (2.4, .45, .6), (3.9, .2, .35), (5.6, .08, .2)],
    "clang": [(1, 1, 1), (1.51, .7, .7), (2.13, .5, .5), (2.74, .35, .35), (3.41, .2, .25)],
    "tap": [(1, 1, 1), (1.6, .3, .4), (2.3, .1, .25)],
}


def tone(out, start, freq, length, gain, kind="iron"):
    t = seconds(length)
    wave = sum(a * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t / (length * .27 * decay))
               for ratio, a, decay in VOICES[kind])
    add(out, start, wave * gain * edges(t, length))


def melody(out, notes, step, gain, kind, length, start=0):
    for i, freq in enumerate(notes):
        tone(out, start + i * step, freq, length, gain, kind)


# War drum: a deep sine that drops in pitch, with a thump of dark noise for the skin.
def drum(out, start, freq, length, gain):
    t = seconds(length)
    pitch = freq * (1 + 1.6 * np.exp(-t / .025))
    body = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) * np.exp(-t / (length * .3))
    skin = lowpass(rng.uniform(-1, 1, len(t)), 900) * np.exp(-t / .03)
    add(out, start, (body + skin * .6) * gain * edges(t, length, .001))


# Grinding stone: band-passed noise shaped by a swell curve (FoodIsekai's cloth rustle, made of rock).
def grind(out, start, length, gain, swell, low=120, high=1400):
    t = seconds(length)
    noise = bandpass(rng.uniform(-1, 1, len(t)), low, high)
    add(out, start, noise * gain * swell(t / length))


# Loose rubble: a scatter of tiny stone ticks.
def rubble(out, start, length, count, gain):
    grain = np.exp(-seconds(.012) / .003)
    for _ in range(count):
        at = start + rng.uniform(0, length) ** 1.6
        add(out, at, bandpass(rng.uniform(-1, 1, len(grain)), 600, 4000) * grain * gain * rng.uniform(.3, 1))


def build(name, a):
    if name == "pad_appear":
        # A stone pad knocks up out of the floor: a low stone tap, a puff of dust and a dull iron tick.
        drum(a, 0, E2, .22, .35)
        tone(a, 0, E3, .2, .25, "tap")
        tone(a, .005, B3, .25, .1, "iron")
        rubble(a, .02, .2, 6, .15)
    elif name == "pad_touch":
        # Stepping on: a boot on stone and two rising iron bells (the FoodIsekai StepOn, a dungeon key).
        drum(a, 0, E2, .25, .3)
        melody(a, [E4, B4], .07, .2, "iron", .38)
    elif name in ("pad_hold", "pad_hold_2", "pad_hold_3"):
        # The hold ticks climb E-G-B toward the lock; each is a stone tap with an iron ring an octave down.
        note = {"pad_hold": E4, "pad_hold_2": G4, "pad_hold_3": B4}[name]
        tone(a, 0, note, .16, .3, "tap")
        tone(a, 0, note / 2, .3, .16, "iron")
        drum(a, 0, B2, .14, .2)
    elif name == "pad_cancel":
        # Stepping off: a falling pair of stone taps, low and dull.
        melody(a, [B3, E3], .08, .3, "tap", .2)
        drum(a, .08, E2, .2, .2)
    elif name == "pad_ready":
        # Locked in (built like FoodIsekai's ReadyConfirm, but low and heavy): a double war drum with an anvil
        # strike, a dark iron chime rising E-G-B an octave down, then a short sword ring as the card flashes.
        drum(a, 0, E2, .45, .6)
        drum(a, .14, E2, .7, .7)
        tone(a, 0, E3, .5, .22, "clang")
        melody(a, [E3, G3, B3], .07, .3, "iron", .6, start=.14)
        tone(a, .14, E2, .8, .25, "iron")
        tone(a, .42, E5, .35, .05, "clang")
        rubble(a, .03, .35, 10, .1)
    elif name == "pad_vanish":
        # The pads sink away (FoodIsekai's CurtainClose, in stone): a grind that swells with the shrinking pads,
        # a heavy settling thud and a low iron note where they close.
        grind(a, 0, .7, .5, lambda x: (.3 + .7 * x) * np.sin(np.pi * np.minimum(1, x * 1.05)) ** .5)
        drum(a, .64, E2, .4, .5)
        tone(a, .65, E3, .4, .15, "iron")
        rubble(a, .66, .3, 10, .12)


# (seconds, short-term loudness target in dB). Ready is the loudest; the per-pad appear and the step off
# stay softer because they land on top of other cues.
CUES = dict(pad_appear=(.3, -22), pad_touch=(.45, -18), pad_hold=(.3, -20), pad_hold_2=(.3, -20),
            pad_hold_3=(.3, -20), pad_cancel=(.35, -22), pad_ready=(1.0, -16), pad_vanish=(1.15, -19))


# Loudest 400 ms RMS in dB, close to the short-term loudness FoodIsekai matches.
def loudness(signal):
    window = int(.4 * RATE)
    padded = np.pad(signal ** 2, (0, max(0, window - len(signal))))
    mean = np.convolve(padded, np.ones(window) / window, "valid")
    return 10 * np.log10(np.max(mean) + 1e-12)


def create(name):
    length, level = CUES[name]
    a = np.zeros(int(length * RATE))
    build(name, a)
    # A stone hall: a few longer, darker reflections than FoodIsekai's small tavern room.
    dry = lowpass(a, 3500)
    for delay, amount in ((.047, .18), (.093, .11), (.151, .06)):
        offset = int(delay * RATE)
        a[offset:] += dry[:-offset] * amount
    # Light retro grain: a held sample every second step, like an old console.
    a = np.repeat(a[::2], 2)[:len(a)]
    a *= 10 ** ((level - loudness(a)) / 20)
    a = np.tanh(a / .89) * .89
    a *= np.clip(np.minimum(np.arange(len(a)) / 128, (len(a) - 1 - np.arange(len(a))) / 256), 0, 1)
    return a


def main():
    template = (FOLDER / "pad_hold.wav.meta").read_text()
    for name in CUES:
        data = create(name)
        output = FOLDER / (name + ".wav")
        wavfile.write(output, RATE, np.round(data * 32767).astype(np.int16))
        meta = Path(str(output) + ".meta")
        if not meta.exists():
            meta.write_text(re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template, count=1))
        print(f"{name}: {len(data) / RATE:.2f}s peak {np.max(np.abs(data)):.2f} loudness {loudness(data):.1f} dB")


if __name__ == "__main__":
    main()
