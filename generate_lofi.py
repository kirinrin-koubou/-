#!/usr/bin/env python3
"""Japanese Lo-fi BGM Generator"""

import mido
from mido import MidiFile, MidiTrack, Message, MetaMessage
import random

random.seed(42)

def create_japanese_lofi():
    bpm = 80
    tpb = 480
    tempo = mido.bpm2tempo(bpm)

    mid = MidiFile(ticks_per_beat=tpb, type=1)

    beat = tpb
    eighth = tpb // 2
    half = tpb * 2
    quarter = tpb
    swing_offset = eighth // 5  # Lo-fiらしいスウィング感

    bars = 8

    # --- Meta ---
    meta = MidiTrack()
    mid.tracks.append(meta)
    meta.append(MetaMessage('set_tempo', tempo=tempo, time=0))
    meta.append(MetaMessage('time_signature', numerator=4, denominator=4, time=0))
    meta.append(MetaMessage('track_name', name='Master', time=0))

    # --- Piano (旋律) ---
    # Aマイナーペンタ + 和風音階: A, C, D, E, G
    piano = MidiTrack()
    mid.tracks.append(piano)
    piano.append(MetaMessage('track_name', name='Piano', time=0))
    piano.append(Message('program_change', channel=0, program=0, time=0))

    melody = [
        # Bar 1
        (69, quarter, 70),   # A4
        (72, eighth, 65),    # C5
        (74, eighth, 62),    # D5
        (72, quarter, 68),   # C5
        (69, quarter, 65),   # A4
        # Bar 2
        (67, quarter, 62),   # G4
        (69, eighth, 60),    # A4
        (67, eighth, 58),    # G4
        (64, half, 68),      # E4
        # Bar 3
        (72, quarter, 70),   # C5
        (74, eighth, 65),    # D5
        (72, eighth, 62),    # C5
        (69, quarter, 68),   # A4
        (67, quarter, 62),   # G4
        # Bar 4
        (64, quarter, 65),   # E4
        (62, eighth, 60),    # D4
        (60, eighth, 58),    # C4
        (62, half, 66),      # D4 (resolve)
    ]

    for _ in range(2):  # 4bar x2 = 8bars
        for note, dur, vel in melody:
            piano.append(Message('note_on', channel=0, note=note,
                                 velocity=vel + random.randint(-4, 4), time=0))
            piano.append(Message('note_off', channel=0, note=note, velocity=0, time=dur))

    # --- Koto (琴風アルペジオ) ---
    koto = MidiTrack()
    mid.tracks.append(koto)
    koto.append(MetaMessage('track_name', name='Koto', time=0))
    koto.append(Message('program_change', channel=1, program=107, time=0))  # GM: Koto

    # Am → G の2小節コードアルペジオ
    koto_pattern = [
        (57, eighth, 52),  # A3
        (64, eighth, 48),  # E4
        (69, eighth, 50),  # A4
        (64, eighth, 46),  # E4
        (57, eighth, 52),  # A3
        (64, eighth, 48),  # E4
        (69, eighth, 50),  # A4
        (64, eighth, 46),  # E4
        (55, eighth, 52),  # G3
        (62, eighth, 48),  # D4
        (67, eighth, 50),  # G4
        (62, eighth, 46),  # D4
        (55, eighth, 52),  # G3
        (62, eighth, 48),  # D4
        (67, eighth, 50),  # G4
        (62, eighth, 46),  # D4
    ]

    for _ in range(4):
        for note, dur, vel in koto_pattern:
            koto.append(Message('note_on', channel=1, note=note,
                                velocity=vel + random.randint(-3, 3), time=0))
            koto.append(Message('note_off', channel=1, note=note, velocity=0, time=dur))

    # --- Bass ---
    bass = MidiTrack()
    mid.tracks.append(bass)
    bass.append(MetaMessage('track_name', name='Bass', time=0))
    bass.append(Message('program_change', channel=2, program=32, time=0))  # Acoustic Bass

    bass_pattern = [
        (45, half, 58),     # A2
        (45, quarter, 54),  # A2
        (43, quarter, 50),  # G2
        (43, half, 56),     # G2
        (40, half, 54),     # E2
        (45, half, 58),     # A2
        (45, quarter, 54),  # A2
        (43, quarter, 50),  # G2
        (36, half, 56),     # C2
        (40, half, 54),     # E2
    ]

    for _ in range(2):
        for note, dur, vel in bass_pattern:
            bass.append(Message('note_on', channel=2, note=note,
                                velocity=vel + random.randint(-3, 3), time=0))
            bass.append(Message('note_off', channel=2, note=note, velocity=0, time=dur))

    # --- Drums (Lo-fiビート) ---
    drums = MidiTrack()
    mid.tracks.append(drums)
    drums.append(MetaMessage('track_name', name='Drums', time=0))

    # Kick=36, Snare=38, Closed HH=42
    events = []  # (abs_time, note, velocity, is_on)

    for bar in range(bars):
        b = bar * 4 * beat

        # ハイハット: 8分音符 (スウィングあり)
        for i in range(8):
            sw = swing_offset if i % 2 == 1 else 0
            t = b + i * eighth + sw
            vel = 42 + random.randint(-5, 5)
            events.append((t, 42, vel, True))
            events.append((t + 20, 42, 0, False))

        # キック: 1拍目と3拍目
        events.append((b, 36, 68 + random.randint(-4, 4), True))
        events.append((b + 15, 36, 0, False))
        events.append((b + beat * 2, 36, 63 + random.randint(-4, 4), True))
        events.append((b + beat * 2 + 15, 36, 0, False))

        # スネア: 2拍目と4拍目
        events.append((b + beat, 38, 56 + random.randint(-4, 4), True))
        events.append((b + beat + 15, 38, 0, False))
        events.append((b + beat * 3, 38, 54 + random.randint(-4, 4), True))
        events.append((b + beat * 3 + 15, 38, 0, False))

    events.sort(key=lambda x: x[0])
    prev_t = 0
    for abs_t, note, vel, is_on in events:
        delta = abs_t - prev_t
        msg_type = 'note_on' if is_on else 'note_off'
        drums.append(Message(msg_type, channel=9, note=note, velocity=vel, time=delta))
        prev_t = abs_t

    out_path = '/home/user/-/japanese_lofi_bgm.mid'
    mid.save(out_path)
    print(f"Saved: {out_path}")
    print(f"  BPM: {bpm}")
    print(f"  Bars: {bars}")
    print(f"  Tracks: Piano, Koto, Bass, Drums")

create_japanese_lofi()
