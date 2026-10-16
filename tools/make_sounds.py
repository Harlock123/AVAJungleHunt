#!/usr/bin/env python3
"""Synthesises AVAJungleHunt's sound effects and tunes into ../src/AVAJungleHunt/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAJungleHunt", "Sounds")
RNG = random.Random(1982)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out




def bongos(bpm, bars):
    """Hand drums: a pattern of high and low bongos across each bar of four."""
    beat = 60.0 / bpm
    pattern = [(0, "lo"), (0.5, "hi"), (1, "hi"), (1.5, "lo"), (2, "lo"), (2.75, "hi"), (3, "hi"), (3.5, "hi")]
    n = int(RATE * beat * 4 * bars)
    out = [0.0] * n
    for b in range(bars):
        for at, which in pattern:
            hit = tone(0.12, 420 if which == "hi" else 220, 300 if which == "hi" else 150, "sine", attack=0.001, release=2.2, gain=0.8)
            start = int(RATE * beat * (b * 4 + at))
            for i, v in enumerate(hit):
                if start + i < n:
                    out[start + i] += v
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    write("Jump.wav", tone(0.18, 260, 700, "tri", attack=0.004, release=0.8), 0.26)
    # A vine caught: a rustle and a slap of hands.
    write("Grab.wav", mix(tone(0.2, 1800, 600, "noise", attack=0.01, release=0.7, gain=0.6), tone(0.06, 500, 300, "square", release=1)), 0.3)
    write("Swing.wav", tone(0.4, 300, 900, "noise", attack=0.1, release=0.8), 0.14)
    write("Splash.wav", mix(tone(0.4, 2500, 300, "noise", attack=0.005, release=0.5), tone(0.3, 300, 100, "sine", release=0.6, gain=0.5)), 0.36)
    write("Stab.wav", cat(tone(0.04, 1800, 2400, "square", release=1), tone(0.08, 3000, 1500, "noise", release=1, gain=0.5)), 0.24)
    write("CrocDown.wav", cat(tone(0.25, 300, 80, "saw", release=0.5), *[tone(0.08, hz(n), kind="pulse", release=0.4) for n in ["E5", "G5", "C6"]]), 0.38)
    write("Gulp.wav", tone(0.18, 200, 500, "tri", attack=0.01, release=0.6), 0.3)
    write("AirLow.wav", cat(tone(0.08, 1000, 1000, "square", release=0.3), silence(0.06), tone(0.08, 1000, 1000, "square", release=0.3)), 0.2)
    write("Rumble.wav", mix(tone(0.8, 120, 60, "noise", attack=0.05, release=0.4), tone(0.7, 55, 40, "saw", attack=0.05, release=0.4, gain=0.6)), 0.4)
    write("Dodge.wav", cat(tone(0.06, hz("C6"), kind="pulse", release=0.3), tone(0.1, hz("G6"), kind="pulse", release=0.5)), 0.26)
    write("Hop.wav", cat(*[tone(0.06, hz(n), kind="pulse", release=0.3) for n in ["G5", "B5", "D6"]]), 0.3)
    write("Rescue.wav", tune(150, [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 1), ("A5", 0.5), ("C6", 1), ("E6", 2)], [("C3", 1), ("G2", 1), ("C3", 1), ("F2", 1), ("C3", 2)], kind="pulse"), 0.44)
    write("Death.wav", cat(tone(0.5, 800, 120, "tri", attack=0.01, release=0.5), *[tone(0.14, hz(n), kind="pulse", release=0.4) for n in ["A4", "F4", "D4", "A3"]]), 0.44)
    write("SceneStart.wav", cat(*[tone(0.09, hz(n), kind="pulse", release=0.3) for n in ["A4", "C5", "E5", "A5"]]), 0.3)
    write("SceneClear.wav", cat(*[tone(0.08, hz(n), kind="pulse", release=0.4) for n in ["A5", "C6", "E6", "A6", "E6", "A6"]]), 0.36)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="pulse", release=0.3) for n in ["A5", "E6", "A5", "E6", "A6"]]), 0.38)

    # The jungle music: a marimba-like tune in A minor over bongos (an original composition), a whole number
    # of beats long so it loops cleanly.
    lead = [("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("A5", 0.5), ("G5", 0.5), ("E5", 0.5), ("D5", 1),
            ("C5", 0.5), ("D5", 0.5), ("E5", 0.5), ("G5", 0.5), ("E5", 1), ("A4", 1),
            ("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("A5", 0.5), ("B5", 0.5), ("A5", 0.5), ("G5", 1),
            ("E5", 0.5), ("D5", 0.5), ("C5", 0.5), ("B4", 0.5), ("A4", 2)]
    bass = [("A2", 1), ("E3", 1), ("A2", 1), ("E3", 1), ("C3", 1), ("G3", 1), ("A2", 1), ("E3", 1),
            ("A2", 1), ("E3", 1), ("G2", 1), ("D3", 1), ("E2", 1), ("B2", 1), ("A2", 2)]
    melody = tune(126, lead, bass, kind="tri")
    write("Music.wav", mix([v * 0.7 for v in melody], bongos(126, 4)), 0.3)
    title_lead = lead + [("E5", 1), ("A5", 1), ("G5", 0.5), ("E5", 0.5), ("D5", 1), ("C5", 1), ("E5", 1), ("A4", 2)]
    title_bass = bass + [("A2", 2), ("C3", 2), ("E2", 2), ("A2", 2)]
    write("TitleTune.wav", cat(mix(tune(120, title_lead, title_bass, kind="tri"), bongos(120, 6)), silence(1.0)), 0.42)
    write("StartTune.wav", tune(160, [("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("A5", 1), ("E5", 0.5), ("A5", 1)], [("A2", 1), ("E2", 1), ("A2", 2)], kind="pulse"), 0.45)
    write("GameOver.wav", tune(66, [("E5", 1), ("C5", 1), ("B4", 1), ("G#4", 1), ("A4", 4)], [("A2", 2), ("F2", 2), ("E2", 2), ("A1", 2)], kind="tri"), 0.45)


if __name__ == "__main__":
    main()
