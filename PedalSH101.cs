// PedalSH101.cs — Pedal SH101 main machine
//
// Faithful, efficient emulation of the Roland SH-101 voice (no GUI, no
// internal sequencer/arp). Receives notes via track 0 — drive it from a
// pattern column or from a Pedal Chord (or other) control machine.
//
// Architecture:
//
//   Note → MIDI → portamento → ┐
//                              ├→ +Range +Tune +VCO-mod ┐
//                LFO ──────────┘                        ├→ VCO ─→ VCF ─→ VCA ─→ out
//                                                       │   ↑      ↑     ↑
//   PWM:    Manual / LFO / Env ───────────────→ ───────→┘   │      │     │
//   ENV: ───────────────────────────────────────────────────┘──────┘─────┘
//   LFO: ──────────────────────────────────────────────────────────┘
//   Kbd-follow: pitch ─────────────────────────────────────────────┘
//
// All DSP runs at the host sample rate, no oversampling. The ZDF Moog
// ladder is stable to self-oscillation without it.
//
// IMPORTANT — preset stability (Build §3.3):
// Parameter declaration order in this file is the public preset format.
// Never reorder; always APPEND new parameters at the end of the global
// list, marking the version they were added in.

using System;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;

namespace PedalSH101
{
    [MachineDecl(
        Name      = "Pedal SH101",
        ShortName = "SH101",
        Author    = "WDE",
        MaxTracks = 1)]
    public class PedalSH101Machine : IBuzzMachine
    {
        readonly IBuzzMachineHost host;

        // ── DSP blocks ─────────────────────────────────────────────────
        readonly VCO        _vco    = new VCO();
        readonly MoogLadder _filter = new MoogLadder();
        readonly ADSR       _env    = new ADSR();
        readonly LFO        _lfo    = new LFO();

        // ── Voice state ────────────────────────────────────────────────
        float _currentPitchSemis = 60f;
        float _targetPitchSemis  = 60f;
        bool  _gateActive        = false;

        // Pending events from setters; consumed at the top of Work()
        bool _hasNoteOn  = false;
        bool _hasNoteOff = false;
        byte _pendingBuzzNote = 0;

        public PedalSH101Machine(IBuzzMachineHost host)
        {
            this.host = host;
        }

        // ─────────────────────────────────────────────────────────────
        //  Track parameter — Note (must be method-style to use Note type;
        //  see Core §3)
        // ─────────────────────────────────────────────────────────────

        [ParameterDecl(Name = "Note",
            IsStateless = true,
            Description = "Note (z=C-4, s=C#-4 …). Note-off = 1.")]
        public void SetNote(Note value, int track)
        {
            byte v = value.Value;
            if (v == 0) return;          // NoValue — shouldn't reach here
            if (v == Note.Off)           // 255 — release
            {
                _hasNoteOff = true;
                return;
            }
            _pendingBuzzNote = v;
            _hasNoteOn       = true;
        }

        // ─────────────────────────────────────────────────────────────
        //  Global parameters (declaration order is the preset format)
        // ─────────────────────────────────────────────────────────────

        // ── VCO ──
        [ParameterDecl(Name = "Range",
            MinValue = 0, MaxValue = 2, DefValue = 1,
            Description = "VCO octave",
            ValueDescriptions = new[] { "16'", "8'", "4'" })]
        public int Range { get; set; } = 1;

        [ParameterDecl(Name = "VCO Mod",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "LFO → VCO pitch (vibrato)")]
        public int VcoMod { get; set; } = 0;

        [ParameterDecl(Name = "PWM Source",
            MinValue = 0, MaxValue = 2, DefValue = 0,
            Description = "Pulse-width modulation source",
            ValueDescriptions = new[] { "Manual", "LFO", "Env" })]
        public int PwmSource { get; set; } = 0;

        [ParameterDecl(Name = "PWM",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Manual: 0=square..127=narrow. LFO/Env: mod depth.")]
        public int PwmAmount { get; set; } = 0;

        // ── Mixer ──
        [ParameterDecl(Name = "Pulse Lvl",
            MinValue = 0, MaxValue = 127, DefValue = 100)]
        public int PulseLevel { get; set; } = 100;

        [ParameterDecl(Name = "Saw Lvl",
            MinValue = 0, MaxValue = 127, DefValue = 0)]
        public int SawLevel { get; set; } = 0;

        [ParameterDecl(Name = "Sub Lvl",
            MinValue = 0, MaxValue = 127, DefValue = 0)]
        public int SubLevel { get; set; } = 0;

        [ParameterDecl(Name = "Noise Lvl",
            MinValue = 0, MaxValue = 127, DefValue = 0)]
        public int NoiseLevel { get; set; } = 0;

        [ParameterDecl(Name = "Sub Type",
            MinValue = 0, MaxValue = 2, DefValue = 0,
            Description = "Sub-oscillator waveform",
            ValueDescriptions = new[] { "Sq -1oct", "Sq -2oct", "Pulse -2oct" })]
        public int SubType { get; set; } = 0;

        // ── VCF ──
        [ParameterDecl(Name = "Cutoff",
            MinValue = 0, MaxValue = 127, DefValue = 90,
            Description = "Filter cutoff (~20 Hz to ~20 kHz, log)")]
        public int Cutoff { get; set; } = 90;

        [ParameterDecl(Name = "Resonance",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Filter resonance (self-oscillates near max)")]
        public int Resonance { get; set; } = 0;

        // Bipolar Env Amount stored as offset (PedalComp §2):
        // 0..128, with 64 = 0; lower = inverted, higher = positive.
        [ParameterDecl(Name = "Env Amt",
            MinValue = 0, MaxValue = 128, DefValue = 64,
            Description = "Filter envelope depth (64 = 0; <64 inverted)")]
        public int EnvAmount { get; set; } = 64;

        [ParameterDecl(Name = "VCF Mod",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "LFO → VCF cutoff depth")]
        public int VcfMod { get; set; } = 0;

        [ParameterDecl(Name = "Kbd Follow",
            MinValue = 0, MaxValue = 2, DefValue = 0,
            Description = "Cutoff tracks pitch",
            ValueDescriptions = new[] { "Off", "1/2", "Full" })]
        public int KbdFollow { get; set; } = 0;

        // ── VCA ──
        [ParameterDecl(Name = "VCA Mode",
            MinValue = 0, MaxValue = 1, DefValue = 1,
            Description = "VCA control source",
            ValueDescriptions = new[] { "Gate", "Env" })]
        public int VcaMode { get; set; } = 1;

        // ── Envelope (ADSR) ──
        [ParameterDecl(Name = "Attack",  MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Envelope attack time (~1 ms .. 5 s)")]
        public int Attack { get; set; } = 0;

        [ParameterDecl(Name = "Decay",   MinValue = 0, MaxValue = 127, DefValue = 64,
            Description = "Envelope decay time (~1 ms .. 10 s)")]
        public int Decay { get; set; } = 64;

        [ParameterDecl(Name = "Sustain", MinValue = 0, MaxValue = 127, DefValue = 100,
            Description = "Envelope sustain level")]
        public int Sustain { get; set; } = 100;

        [ParameterDecl(Name = "Release", MinValue = 0, MaxValue = 127, DefValue = 32,
            Description = "Envelope release time (~1 ms .. 10 s)")]
        public int Release { get; set; } = 32;

        // ── LFO ──
        [ParameterDecl(Name = "LFO Rate",
            MinValue = 0, MaxValue = 127, DefValue = 64,
            Description = "LFO rate (~0.1 Hz .. ~30 Hz, log)")]
        public int LfoRate { get; set; } = 64;

        [ParameterDecl(Name = "LFO Wave",
            MinValue = 0, MaxValue = 3, DefValue = 0,
            Description = "LFO waveform",
            ValueDescriptions = new[] { "Triangle", "Square", "S&H", "Noise" })]
        public int LfoWave { get; set; } = 0;

        [ParameterDecl(Name = "LFO Delay",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "LFO onset delay after note-on (0 .. 2 s)")]
        public int LfoDelay { get; set; } = 0;

        // ── Pitch ──
        [ParameterDecl(Name = "Glide",
            MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Portamento time (0 = off / instant)")]
        public int Glide { get; set; } = 0;

        // Bipolar Tune stored as offset (50 = center, ±50 cents).
        [ParameterDecl(Name = "Tune",
            MinValue = 0, MaxValue = 100, DefValue = 50,
            Description = "Master fine tune (50 = 0¢, 0 = -50¢, 100 = +50¢)")]
        public int Tune { get; set; } = 50;

        // ── Master ──
        [ParameterDecl(Name = "Volume",
            MinValue = 0, MaxValue = 127, DefValue = 96,
            Description = "Output level")]
        public int Volume { get; set; } = 96;

        // ── New in v1.x — APPEND any future parameters BELOW this line so
        // existing presets keep working (Build §3.3). ─────────────────────


        // ─────────────────────────────────────────────────────────────
        //  Coefficient cache for things that depend on (parameter, sr)
        //  but aren't owned by an inner DSP class. (PedalComp §6)
        // ─────────────────────────────────────────────────────────────
        int   _cSr           = 0;
        int   _cGlide        = -1;
        float _glideCoef     = 0f;
        int   _cLfoRate      = -1;
        float _lfoRateHz     = 1f;
        int   _cLfoDelay     = -1;
        int   _lfoDelaySamps = 0;

        void UpdateCoefs(int sr)
        {
            bool srChanged = sr != _cSr;
            if (srChanged) _cSr = sr;

            if (srChanged || Glide != _cGlide)
            {
                _cGlide = Glide;
                if (Glide == 0)
                {
                    _glideCoef = 0f;       // instant
                }
                else
                {
                    // Log map 1..127 → 1 ms .. 2 s
                    float t = (Glide - 1) / 126f;
                    float ms = MathF.Pow(2000f, t);
                    float n  = ms * 0.001f * sr;
                    if (n < 1f) n = 1f;
                    _glideCoef = MathF.Exp(-1f / n);
                }
            }

            if (LfoRate != _cLfoRate)
            {
                _cLfoRate = LfoRate;
                // 0.1 Hz to 30 Hz, log
                _lfoRateHz = 0.1f * MathF.Pow(300f, LfoRate / 127f);
            }

            if (srChanged || LfoDelay != _cLfoDelay)
            {
                _cLfoDelay = LfoDelay;
                float secs = (LfoDelay / 127f) * 2f;     // 0..2 s
                _lfoDelaySamps = (int)(secs * sr);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Note triggering
        // ─────────────────────────────────────────────────────────────
        void TriggerNote(byte buzzNote)
        {
            // Buzz byte → MIDI (Core §7)
            int oct  = (buzzNote >> 4);
            int semi = (buzzNote & 0xF) - 1;
            int midi = oct * 12 + semi;

            float pitch = midi;

            bool wasIdle = !_env.IsActive;

            _targetPitchSemis = pitch;
            // Glide off, or coming from Idle (first note) → snap immediately.
            if (Glide == 0 || wasIdle)
                _currentPitchSemis = pitch;

            _env.NoteOn();
            _gateActive = true;
            _lfo.NoteOn(_lfoDelaySamps);
        }

        // ─────────────────────────────────────────────────────────────
        //  Audio process
        // ─────────────────────────────────────────────────────────────
        public bool Work(Sample[] output, int n, WorkModes mode)
        {
            int sr = host.MasterInfo.SamplesPerSec;
            UpdateCoefs(sr);

            // Drain pending events. Process note-on before note-off so a
            // pattern row containing both retriggers cleanly.
            if (_hasNoteOn)  { TriggerNote(_pendingBuzzNote); _hasNoteOn = false; }
            if (_hasNoteOff) { _env.NoteOff(); _gateActive = false; _hasNoteOff = false; }

            // Idle fast-path: zero output, return false so ReBuzz can prune.
            if (!_env.IsActive && !_gateActive)
            {
                for (int i = 0; i < n; i++) output[i] = new Sample(0f, 0f);
                return false;
            }

            // ── Hoist parameter conversions out of the per-sample loop ──
            float rangeOffsetSemi = (Range == 0) ? -12f : ((Range == 2) ? 12f : 0f);
            float fineSemi        = (Tune - 50) * 0.01f;
            float vcoModSemi      = (VcoMod / 127f) * 12f;            // up to ±1 oct vibrato
            float pulseLvl        = PulseLevel / 127f;
            float sawLvl          = SawLevel   / 127f;
            float subLvl          = SubLevel   / 127f;
            float noiseLvl        = NoiseLevel / 127f;
            int   subType         = SubType;
            int   pwmSrc          = PwmSource;
            float pwmAmt          = PwmAmount  / 127f;
            float baseCutoffHz    = 20f * MathF.Pow(1000f, Cutoff / 127f); // 20..20k Hz log
            float resN            = Resonance  / 127f;                // 0..1
            float envAmtOctaves   = ((EnvAmount - 64) / 64f) * 5f;     // ±5 oct
            float lfoFiltOctaves  = (VcfMod / 127f) * 4f;              // up to 4 oct
            int   kbdFollowMode   = KbdFollow;
            int   vcaMode         = VcaMode;
            float volume          = Volume / 127f;
            float glideCoef       = _glideCoef;
            float currentPitch    = _currentPitchSemis;
            float targetPitch     = _targetPitchSemis;

            // Push live knob values to inner DSP blocks.
            _env.Attack  = Attack;
            _env.Decay   = Decay;
            _env.Sustain = Sustain;
            _env.Release = Release;
            _lfo.Waveform = (LFO.Wave)LfoWave;
            _lfo.Rate     = _lfoRateHz;

            float fcMaxHz = sr * 0.49f;

            for (int i = 0; i < n; i++)
            {
                // 1. Portamento (one-pole approach to target)
                if (glideCoef == 0f)
                    currentPitch = targetPitch;
                else
                    currentPitch = targetPitch + (currentPitch - targetPitch) * glideCoef;

                // 2. LFO and envelope
                float lfo = _lfo.Process(sr);
                float env = _env.Process(sr);

                // 3. Final VCO pitch and frequency
                float pitchSemis = currentPitch
                                 + rangeOffsetSemi
                                 + fineSemi
                                 + lfo * vcoModSemi;
                float freq = 440f * MathF.Pow(2f, (pitchSemis - 69f) * (1f / 12f));
                if (freq < 1f) freq = 1f;

                // 4. PWM
                float pwm;
                switch (pwmSrc)
                {
                    case 1: // LFO modulates around 0.5
                        pwm = 0.5f + lfo * pwmAmt * 0.4f;
                        break;
                    case 2: // Env modulates around 0.5 (downward, since env >= 0)
                        pwm = 0.5f - env * pwmAmt * 0.4f;
                        break;
                    default: // Manual: 0 = square, 127 = narrow (10%)
                        pwm = 0.5f - pwmAmt * 0.4f;
                        break;
                }
                if      (pwm < 0.05f) pwm = 0.05f;
                else if (pwm > 0.95f) pwm = 0.95f;

                // 5. VCO
                float oscOut = _vco.Process(
                    freq, pwm, subType,
                    pulseLvl, sawLvl, subLvl, noiseLvl, sr);

                // 6. VCF cutoff (in octaves above base)
                float kbdOctaves = 0f;
                if      (kbdFollowMode == 1) kbdOctaves = (currentPitch - 60f) / 24f; // half
                else if (kbdFollowMode == 2) kbdOctaves = (currentPitch - 60f) / 12f; // full

                float fcOctMod = envAmtOctaves * env
                               + lfoFiltOctaves * lfo
                               + kbdOctaves;
                float fc = baseCutoffHz * MathF.Pow(2f, fcOctMod);
                if      (fc < 20f)     fc = 20f;
                else if (fc > fcMaxHz) fc = fcMaxHz;

                // 7. VCF
                float filtered = _filter.Process(oscOut, fc, resN, sr);

                // 8. VCA
                float gain = (vcaMode == 1) ? env : (_gateActive ? 1f : 0f);
                float sample = filtered * gain * volume;

                // 9. Output (mono → both channels). Generators write directly
                //    at ±32768 scale (PedalComp §1).
                float s32 = sample * 32768f;
                output[i] = new Sample(s32, s32);
            }

            // Persist pitch glide state across buffers.
            _currentPitchSemis = currentPitch;

            return true;   // produced audio
        }
    }
}
