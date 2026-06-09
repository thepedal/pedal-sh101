#!/usr/bin/env python3
"""
gen_presets.py — emit the Pedal SH101 preset bundle.

Run from the project directory:
    python gen_presets.py

Output: "Pedal SH101.prs.xml" (auto-load form per Build §3.1).

Format per Build §3.2:
  - UTF-8 with BOM
  - Each Item Key unique
  - Parameter Index matches declaration order in PedalSH101.cs
  - Parameter Value is the raw stored value (pre-MinValue offset)

Editing rule (Build §3.3): if you add a parameter to PedalSH101.cs, APPEND
it to the end of PARAM_INDEX and DEFAULTS — never insert in the middle —
or every existing preset will write to the wrong parameter.
"""

# ─── Declaration order — MUST match the property declaration order in
#     PedalSH101.cs. Indices are 0-based positions in Group 1 (globals).
PARAM_INDEX = {
    "Range":       0,
    "VCO Mod":     1,
    "PWM Source":  2,
    "PWM":         3,
    "Pulse Lvl":   4,
    "Saw Lvl":     5,
    "Sub Lvl":     6,
    "Noise Lvl":   7,
    "Sub Type":    8,
    "Cutoff":      9,
    "Resonance":  10,
    "Env Amt":    11,    # bipolar, 64 = 0
    "VCF Mod":    12,
    "Kbd Follow": 13,
    "VCA Mode":   14,
    "Attack":     15,
    "Decay":      16,
    "Sustain":    17,
    "Release":    18,
    "LFO Rate":   19,
    "LFO Wave":   20,
    "LFO Delay":  21,
    "Glide":      22,
    "Tune":       23,    # bipolar, 50 = 0¢
    "Volume":     24,
}

# ─── Machine-declared defaults — emitted where the preset doesn't override.
DEFAULTS = {
    "Range": 1, "VCO Mod": 0, "PWM Source": 0, "PWM": 0,
    "Pulse Lvl": 100, "Saw Lvl": 0, "Sub Lvl": 0, "Noise Lvl": 0,
    "Sub Type": 0,
    "Cutoff": 90, "Resonance": 0, "Env Amt": 64, "VCF Mod": 0,
    "Kbd Follow": 0, "VCA Mode": 1,
    "Attack": 0, "Decay": 64, "Sustain": 100, "Release": 32,
    "LFO Rate": 64, "LFO Wave": 0, "LFO Delay": 0,
    "Glide": 0, "Tune": 50, "Volume": 96,
}

# ─── Presets — sparse overrides. Only list params that differ from defaults.
# Categories are loosely:
#   - Bass:   Acid, Sub, Deep Sub, Reese, Wobble
#   - Lead:   Square, Vibrato, Glide, Sync-ish
#   - Pluck:  Pluck Seq, Saw Stab
#   - Pad:    Wide Pulse Pad, String Pad
#   - Sweep:  Resonant Sweep, Noise Sweep, PWM Sweep
#   - FX:     Sci-Fi Drone, S&H Robot, Filter Ping, Bell
#   - Perc:   Kick, Snare Burst
PRESETS = {

    # ─── Bass ─────────────────────────────────────────────────────────

    "Acid Bass": {
        # 303-style: saw, low cutoff, high res, env sweep, snappy decay.
        "Pulse Lvl": 0, "Saw Lvl": 100,
        "Cutoff": 40, "Resonance": 100, "Env Amt": 94,
        "Kbd Follow": 1,
        "Decay": 40, "Sustain": 0, "Release": 16,
        "Glide": 30, "Volume": 100,
    },

    "Sub Bass": {
        # Pure sub oscillator at -1 oct square. Round, fat, no filter mod.
        "Pulse Lvl": 0, "Sub Lvl": 127,
        "Cutoff": 70,
        "Release": 20,
    },

    "Deep Sub": {
        # Sub-only at -2 oct narrow pulse, transposed an octave down.
        "Range": 0,
        "Pulse Lvl": 0, "Sub Lvl": 127, "Sub Type": 2,
        "Cutoff": 50,
        "Release": 20, "Volume": 100,
    },

    "Reese-ish": {
        # Single-VCO Reese approximation: saw + sub, slow LFO on cutoff.
        "Range": 0,
        "Pulse Lvl": 0, "Saw Lvl": 100, "Sub Lvl": 60, "Sub Type": 1,
        "Cutoff": 40, "Resonance": 60, "Env Amt": 72,
        "VCF Mod": 30,
        "Decay": 80, "Release": 40,
        "LFO Rate": 25,
        "Glide": 20, "Volume": 95,
    },

    "Wobble Bass": {
        # Heavy LFO on the filter, gate-mode VCA so the LFO does the rhythm.
        "Sub Lvl": 70,
        "Cutoff": 30, "Resonance": 80,
        "VCF Mod": 80,
        "VCA Mode": 0,            # Gate — let LFO shape the level
        "Release": 30,
        "LFO Rate": 50,
        "Volume": 100,
    },

    # ─── Lead ─────────────────────────────────────────────────────────

    "Square Lead": {
        # Full square pulse, mild env on filter, no res. The classic.
        "Cutoff": 80, "Resonance": 30, "Env Amt": 79,
        "Kbd Follow": 1,
        "Attack": 2, "Decay": 50, "Sustain": 90, "Release": 30,
        "LFO Rate": 70, "Volume": 100,
    },

    "Vibrato Lead": {
        # Mid-rate LFO on VCO pitch, delayed onset so the vibrato wells in.
        "VCO Mod": 25,
        "Sub Lvl": 30,
        "Cutoff": 75, "Resonance": 30, "Env Amt": 76,
        "Kbd Follow": 1,
        "Attack": 3, "Decay": 60, "Release": 30,
        "LFO Rate": 60, "LFO Delay": 64,
        "Glide": 10, "Volume": 100,
    },

    "Glide Lead": {
        # Heavy portamento — every note slides into the next.
        "PWM": 20,
        "Sub Lvl": 50,
        "Cutoff": 75, "Resonance": 25, "Env Amt": 74,
        "Kbd Follow": 1,
        "Attack": 2, "Decay": 60, "Release": 30,
        "LFO Rate": 60,
        "Glide": 70, "Volume": 100,
    },

    "Sync-ish Lead": {
        # No hard sync (single VCO) but high res + saw + env sweep gets
        # close to the harmonic richness.
        "Pulse Lvl": 0, "Saw Lvl": 100,
        "Cutoff": 40, "Resonance": 110, "Env Amt": 119,
        "Kbd Follow": 2,
        "Decay": 70, "Sustain": 40, "Release": 30,
        "Volume": 95,
    },

    # ─── Pluck / Stab ─────────────────────────────────────────────────

    "Pluck Seq": {
        # Mid PWM, all three osc sources, big env sweep, no sustain.
        "PWM": 32,
        "Pulse Lvl": 70, "Saw Lvl": 70, "Sub Lvl": 30,
        "Cutoff": 35, "Resonance": 70, "Env Amt": 109,
        "Kbd Follow": 1,
        "Decay": 25, "Sustain": 0, "Release": 20,
        "Volume": 100,
    },

    "Saw Stab": {
        # Pure saw with a sub layer, mild filter sweep, no sustain.
        "Pulse Lvl": 0, "Saw Lvl": 127, "Sub Lvl": 30,
        "Cutoff": 65, "Resonance": 30, "Env Amt": 89,
        "Kbd Follow": 1,
        "Decay": 35, "Sustain": 0, "Release": 25,
        "Volume": 100,
    },

    # ─── Pad ──────────────────────────────────────────────────────────

    "Wide Pulse Pad": {
        # PWM driven by the LFO; pulse breathes between square and narrow.
        "PWM Source": 1, "PWM": 80,
        "Saw Lvl": 30, "Sub Lvl": 50,
        "Cutoff": 70, "Resonance": 20, "Env Amt": 74,
        "VCF Mod": 20,
        "Kbd Follow": 1,
        "Attack": 70, "Decay": 80, "Sustain": 90, "Release": 80,
        "LFO Rate": 20,
        "Volume": 90,
    },

    "String Pad": {
        # Saw + pulse mix, subtle delayed vibrato, long slow attack.
        "VCO Mod": 3,
        "PWM": 32,
        "Pulse Lvl": 90, "Saw Lvl": 90, "Sub Lvl": 40,
        "Cutoff": 60, "Resonance": 10, "Env Amt": 74,
        "VCF Mod": 15,
        "Kbd Follow": 1,
        "Attack": 65, "Decay": 80, "Release": 90,
        "LFO Rate": 40, "LFO Delay": 50,
        "Volume": 90,
    },

    # ─── Sweep ────────────────────────────────────────────────────────

    "Resonant Sweep": {
        # Pulse + saw together, low cutoff with huge upward env sweep.
        "Saw Lvl": 80, "Pulse Lvl": 80,
        "Cutoff": 30, "Resonance": 110, "Env Amt": 114,
        "Attack": 60, "Decay": 80, "Sustain": 50, "Release": 60,
        "Volume": 90,
    },

    "Noise Sweep": {
        # Filtered noise with big env mod — for risers and FX.
        "Pulse Lvl": 0, "Noise Lvl": 127,
        "Cutoff": 20, "Resonance": 80, "Env Amt": 114,
        "Decay": 80, "Sustain": 0, "Release": 40,
        "Volume": 100,
    },

    "PWM Sweep": {
        # Envelope drives PWM — the pulse widens through the note.
        "PWM Source": 2, "PWM": 100,
        "Sub Lvl": 40,
        "Cutoff": 80, "Resonance": 20,
        "Kbd Follow": 1,
        "Attack": 80, "Decay": 80, "Sustain": 70, "Release": 70,
        "Volume": 100,
    },

    # ─── FX ───────────────────────────────────────────────────────────

    "Sci-Fi Drone": {
        # Self-osc filter, gate mode, very slow LFO on cutoff. Hold a key.
        "Range": 0,
        "VCO Mod": 5,
        "PWM Source": 1, "PWM": 60,
        "Pulse Lvl": 80, "Sub Lvl": 80, "Noise Lvl": 20, "Sub Type": 1,
        "Cutoff": 50, "Resonance": 110,
        "VCF Mod": 50,
        "VCA Mode": 0,            # Gate — sustained drone
        "Attack": 60, "Release": 80,
        "LFO Rate": 15,
        "Volume": 90,
    },

    "S&H Robot": {
        # Sample-and-hold LFO on the filter — randomly stepped timbre.
        "Cutoff": 60, "Resonance": 50,
        "VCF Mod": 90,
        "Decay": 30, "Sustain": 70, "Release": 20,
        "LFO Rate": 80, "LFO Wave": 2,
        "Volume": 100,
    },

    "Filter Ping": {
        # Self-oscillating filter as the sound source. Noise just feeds it.
        # Full Kbd Follow makes it pitch-track like a tuned osc.
        "Pulse Lvl": 0, "Noise Lvl": 5,
        "Cutoff": 50, "Resonance": 120, "Env Amt": 99,
        "Kbd Follow": 2,
        "Decay": 20, "Sustain": 0, "Release": 15,
        "Volume": 90,
    },

    "Bell": {
        # High-octave pulse + sub, ringy filter, percussive env.
        "Range": 2,
        "PWM": 50,
        "Sub Lvl": 50,
        "Resonance": 70,
        "Kbd Follow": 2,
        "Decay": 50, "Sustain": 0, "Release": 60,
        "Volume": 90,
    },

    # ─── Perc ─────────────────────────────────────────────────────────

    "Kick": {
        # Sub + saw + noise click, fast filter swoop, instant attack.
        "Range": 0,
        "Pulse Lvl": 0, "Saw Lvl": 100, "Sub Lvl": 100, "Noise Lvl": 30,
        "Cutoff": 25, "Resonance": 20, "Env Amt": 114,
        "Decay": 15, "Sustain": 0, "Release": 10,
        "Volume": 110,
    },

    "Snare Burst": {
        # Pure noise, short envelope, mid-bright filter.
        "Pulse Lvl": 0, "Noise Lvl": 127,
        "Cutoff": 80, "Resonance": 20, "Env Amt": 54,
        "Decay": 18, "Sustain": 0, "Release": 12,
        "Volume": 100,
    },
}


from xml.sax.saxutils import quoteattr


def emit_preset(name, overrides):
    params = {**DEFAULTS, **overrides}
    # quoteattr returns the value already quoted and XML-escaped (handles
    # &, <, >, " etc.) — necessary for keys like "S&H Robot".
    lines = [f'  <Item Key={quoteattr(name)}>']
    lines.append('    <Preset Machine="Pedal SH101">')
    lines.append('      <Parameters>')
    # Declaration order — PARAM_INDEX preserves insertion order (py3.7+).
    for pname, idx in PARAM_INDEX.items():
        val = params[pname]
        lines.append(
            f'        <Parameter Name={quoteattr(pname)} Group="1" '
            f'Index="{idx}" Track="0" Value="{val}" />'
        )
    lines.append('      </Parameters>')
    lines.append('      <Attributes />')
    lines.append('      <Comment></Comment>')
    lines.append('    </Preset>')
    lines.append('  </Item>')
    return '\n'.join(lines)


def main():
    out_lines = ['<?xml version="1.0" encoding="utf-8"?>',
                 '<PresetDictionary>']
    for name, overrides in PRESETS.items():
        out_lines.append(emit_preset(name, overrides))
    out_lines.append('</PresetDictionary>')
    body = '\n'.join(out_lines) + '\n'

    out_path = 'Pedal SH101.prs.xml'
    with open(out_path, 'w', encoding='utf-8-sig') as f:
        f.write(body)

    # Sanity: range-check every emitted value against the parameter's
    # declared bounds, so the script catches typos before deploy.
    BOUNDS = {
        "Range": (0, 2), "VCO Mod": (0, 127), "PWM Source": (0, 2),
        "PWM": (0, 127), "Pulse Lvl": (0, 127), "Saw Lvl": (0, 127),
        "Sub Lvl": (0, 127), "Noise Lvl": (0, 127), "Sub Type": (0, 2),
        "Cutoff": (0, 127), "Resonance": (0, 127), "Env Amt": (0, 128),
        "VCF Mod": (0, 127), "Kbd Follow": (0, 2), "VCA Mode": (0, 1),
        "Attack": (0, 127), "Decay": (0, 127), "Sustain": (0, 127),
        "Release": (0, 127), "LFO Rate": (0, 127), "LFO Wave": (0, 3),
        "LFO Delay": (0, 127), "Glide": (0, 127), "Tune": (0, 100),
        "Volume": (0, 127),
    }
    errors = []
    for pname, overrides in PRESETS.items():
        merged = {**DEFAULTS, **overrides}
        for k, v in merged.items():
            lo, hi = BOUNDS[k]
            if not (lo <= v <= hi):
                errors.append(f"{pname!r}: {k}={v} outside [{lo},{hi}]")
    if errors:
        print("RANGE ERRORS:")
        for e in errors:
            print(f"  {e}")
        raise SystemExit(1)

    print(f"Wrote {len(PRESETS)} presets to {out_path}")


if __name__ == '__main__':
    main()
