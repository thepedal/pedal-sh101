// ADSR.cs — Pedal SH101 envelope generator
//
// Standard 4-stage ADSR with exponential time constants (one-pole approach
// to a target value per stage). Time constants are cached against the
// host's Attack/Decay/Sustain/Release values and only recomputed when one
// of them changes (PedalComp §6).
//
// Retrigger semantics: NoteOn() does NOT reset _level — the envelope
// re-enters the Attack stage from wherever it currently sits. This
// avoids audible clicks when retriggering during the release tail of a
// previous note.
//
// Time mapping: 0..127 maps log-uniformly to:
//   Attack          2 ms ..  8 s
//   Decay / Release 2 ms .. 15 s
// Sustain is a level (0..1), not a time.
//
// v1.2: ranges tightened from 1ms..5/10s (v1.1) — see SH101 notes §11
// for rationale. Default values in PedalSH101.cs adjusted to match;
// existing presets reauthored at the same time.

using System;

namespace PedalSH101
{
    public sealed class ADSR
    {
        public enum Stage { Idle, Attack, Decay, Sustain, Release }

        // ── Live state ────────────────────────────────────────────────────
        Stage _stage = Stage.Idle;
        float _level = 0f;

        // ── Public knobs (set by host before each Process call) ───────────
        public int Attack  { get; set; } = 0;     // 0..127
        public int Decay   { get; set; } = 64;
        public int Sustain { get; set; } = 100;
        public int Release { get; set; } = 32;

        // ── Coefficient cache ─────────────────────────────────────────────
        int   _cSr  = 0;
        int   _cA   = -1, _cD = -1, _cS = -1, _cR = -1;
        float _aCoef, _dCoef, _rCoef, _sLevel;

        public bool IsActive => _stage != Stage.Idle;
        public Stage CurrentStage => _stage;

        public void NoteOn()
        {
            // Don't touch _level — smooth retrigger from current value.
            _stage = Stage.Attack;
        }

        public void NoteOff()
        {
            if (_stage != Stage.Idle) _stage = Stage.Release;
        }

        public void HardReset()
        {
            _stage = Stage.Idle;
            _level = 0f;
        }

        static float TimeToCoef(int val, float minMs, float maxMs, int sr)
        {
            // Log-map 0..127 → minMs..maxMs.
            float t  = val / 127f;
            float ms = minMs * MathF.Pow(maxMs / minMs, t);
            float n  = ms * 0.001f * sr;
            if (n < 1f) n = 1f;          // never zero — avoids /0 and 1-coef==1 edge
            return MathF.Exp(-1f / n);   // per-sample multiplier
        }

        void UpdateCoefs(int sr)
        {
            if (sr == _cSr && Attack == _cA && Decay == _cD &&
                Sustain == _cS && Release == _cR) return;
            _cSr = sr; _cA = Attack; _cD = Decay; _cS = Sustain; _cR = Release;

            _aCoef  = TimeToCoef(Attack,  2f,  8000f, sr);
            _dCoef  = TimeToCoef(Decay,   2f, 15000f, sr);
            _rCoef  = TimeToCoef(Release, 2f, 15000f, sr);
            _sLevel = Sustain / 127f;
        }

        public float Process(int sr)
        {
            UpdateCoefs(sr);

            switch (_stage)
            {
                case Stage.Attack:
                {
                    // Slight overshoot target makes the attack curve hit 1.0
                    // in finite time and gives a snappier feel than asymptotic
                    // approach to exactly 1.0.
                    const float aTarget = 1.05f;
                    _level = aTarget + (_level - aTarget) * _aCoef;
                    if (_level >= 1f) { _level = 1f; _stage = Stage.Decay; }
                    break;
                }
                case Stage.Decay:
                    _level = _sLevel + (_level - _sLevel) * _dCoef;
                    if (MathF.Abs(_level - _sLevel) < 1e-4f)
                    {
                        _level = _sLevel;
                        _stage = Stage.Sustain;
                    }
                    break;
                case Stage.Sustain:
                    _level = _sLevel;
                    break;
                case Stage.Release:
                    _level *= _rCoef;
                    if (_level < 1e-5f) { _level = 0f; _stage = Stage.Idle; }
                    break;
                default:
                    _level = 0f;
                    break;
            }
            return _level;
        }
    }
}
