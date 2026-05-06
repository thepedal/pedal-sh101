# Pedal SH101

A faithful, efficient managed-C# emulation of the Roland SH-101 voice for
ReBuzz. Synth voice only — no internal sequencer/arpeggiator, no GUI. Drive
it from a pattern column or from a Pedal Chord (or other) control machine.

## Architecture

```
Note ─┐
      ├→ Glide ─→ +Range +Tune +VCO-mod ─┐
      │                                  │
LFO ──┼──────────────────────────────────┘─→ ┌─────┐
      │                                      │ VCO │ → ┌─────┐    ┌─────┐
LFO/Env/Manual → PWM ───────────────────────→│     │   │ VCF │ →  │ VCA │ → out
      │                                      └─────┘   └─────┘    └─────┘
ENV ──┼─────────────────────────────────────────────→ cutoff ─────→ gain
LFO ──┴─────────────────────────────────────────────→ cutoff
Pitch ────────────────────────────────────────────── → cutoff (Kbd Follow)
```

- **VCO** — band-limited Pulse (PWM) + Saw + Sub-osc + Noise via PolyBLEP.
  Sub-osc supports 1-oct square, 2-oct square, and 2-oct narrow pulse.
- **VCF** — 4-pole ZDF (zero-delay-feedback) Moog ladder, closed-form
  feedback resolution. Stable to self-oscillation without oversampling.
- **ADSR** — exponential one-pole stages with cached coefficients.
- **LFO** — Triangle / Square / S&H / Noise with note-onset delay.
- **Portamento** — exponential one-pole glide on pitch CV.

All DSP runs at the host sample rate. Coefficient caching follows
PedalComp §6 — Pow/Tan/Exp calls only fire when an input changes.

## Build

```
dotnet build -c Release
```

The project deploys `Pedal SH101.NET.dll` to
`C:\Program Files\ReBuzz\Gear\Generators` automatically. Override the
path by setting `ReBuzzPath` on the command line:

```
dotnet build -c Release -p:ReBuzzPath="D:\ReBuzz"
```

Build hygiene per `Build §1.2` / `§2` / `§4`:
- AssemblyName ends in `.NET` (managed loader routing)
- `DebugType=none`, `DebugSymbols=false`, `GenerateDependencyFile=false`
- `MSB3277` warnings suppressed (harmless ReBuzz/.NET10 conflicts)

## Parameters

Single track, mono. Note-off = 1 (Buzz `Note.Off` = 0xFF).

### VCO
| Name         | Range | Default | Notes |
|--------------|------:|--------:|-------|
| Range        | 0–2   | 1 (8')  | 16' / 8' / 4'                       |
| VCO Mod      | 0–127 | 0       | LFO → pitch (vibrato), up to ±1 oct |
| PWM Source   | 0–2   | 0       | Manual / LFO / Env                  |
| PWM          | 0–127 | 0       | Manual: 0=square..127=narrow. LFO/Env: depth |

### Mixer
| Name        | Range | Default |
|-------------|------:|--------:|
| Pulse Lvl   | 0–127 | 100     |
| Saw Lvl     | 0–127 | 0       |
| Sub Lvl     | 0–127 | 0       |
| Noise Lvl   | 0–127 | 0       |
| Sub Type    | 0–2   | 0       | Sq -1oct / Sq -2oct / Pulse -2oct |

### VCF
| Name       | Range | Default | Notes |
|------------|------:|--------:|-------|
| Cutoff     | 0–127 | 90      | ~20 Hz to ~20 kHz, log     |
| Resonance  | 0–127 | 0       | Self-oscillates near max   |
| Env Amt    | 0–128 | 64      | 64 = 0; <64 inverted; ±5 oct max |
| VCF Mod    | 0–127 | 0       | LFO → cutoff, up to 4 oct  |
| Kbd Follow | 0–2   | 0       | Off / Half / Full          |

### VCA / Envelope
| Name     | Range | Default | Notes                          |
|----------|------:|--------:|--------------------------------|
| VCA Mode | 0–1   | 1 (Env) | Gate or Env                    |
| Attack   | 0–127 | 0       | ~1 ms .. 5 s, log              |
| Decay    | 0–127 | 64      | ~1 ms .. 10 s, log             |
| Sustain  | 0–127 | 100     | Level (0..1)                   |
| Release  | 0–127 | 32      | ~1 ms .. 10 s, log             |

### LFO
| Name      | Range | Default | Notes                              |
|-----------|------:|--------:|------------------------------------|
| LFO Rate  | 0–127 | 64      | ~0.1 Hz .. 30 Hz, log              |
| LFO Wave  | 0–3   | 0       | Triangle / Square / S&H / Noise    |
| LFO Delay | 0–127 | 0       | Onset delay after note-on, 0..2 s  |

### Pitch / Master
| Name   | Range  | Default | Notes                                    |
|--------|-------:|--------:|------------------------------------------|
| Glide  | 0–127  | 0       | 0 = off / instant; else log 1 ms .. 2 s |
| Tune   | 0–100  | 50      | Master fine tune; 50 = 0¢, ±50¢          |
| Volume | 0–127  | 96      | Output level                             |

## Adding parameters in future versions

Always APPEND new parameters at the end of the global declaration list.
Re-ordering breaks every saved preset (Build §3.3 — presets reference
parameters by index, not name).

There's a clearly-marked "New in v1.x" comment near the bottom of
`PedalSH101.cs` for this purpose.
