// VCO.cs — Pedal SH101 oscillator block
//
// Single VCO with four mixable sources:
//   - Pulse (with PWM)
//   - Sawtooth
//   - Sub-oscillator (1 oct sq / 2 oct sq / 2 oct narrow pulse)
//   - White noise
//
// Saw and pulse are band-limited with PolyBLEP. The sub-osc tracks the
// main oscillator's frequency divided by 2 or 4; on the SH-101 this is
// implemented with a CMOS flip-flop, but a phase counter at the divided
// rate is functionally equivalent and lets us reuse PolyBLEP for the
// sub edges as well.
//
// Phases are free-running across notes — that's authentic to the SH-101
// (no per-note phase reset).

using System;

namespace PedalSH101
{
    public sealed class VCO
    {
        // Phase accumulators in [0, 1)
        float _phase    = 0f;
        float _subPhase = 0f;

        // Noise RNG (xorshift32 state — must be non-zero)
        uint _rng = 0x9E3779B9u;

        public void Reset()
        {
            _phase    = 0f;
            _subPhase = 0f;
        }

        // ── PolyBLEP — polynomial approximation of a band-limited step ──────
        // t  = current phase (0..1)
        // dt = normalised frequency (freq/sr), the phase increment per sample
        // Adds a correction near the discontinuity that smooths the step
        // over ~2 samples, removing audible aliasing.
        static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        // ── Main process ─────────────────────────────────────────────────────
        // Returns the mixed VCO output (≈ ±1 per active source). Caller is
        // responsible for headroom — full pulse + saw + sub + noise can
        // theoretically exceed ±1, but the VCF + master volume stages handle
        // that comfortably.
        public float Process(
            float freq,        // Hz
            float pwm,         // pulse width 0..1 (clamp upstream to 0.05..0.95)
            int   subType,     // 0 = 1-oct sq, 1 = 2-oct sq, 2 = 2-oct narrow pulse
            float pulseLevel,  // 0..1
            float sawLevel,    // 0..1
            float subLevel,    // 0..1
            float noiseLevel,  // 0..1
            int   sr)
        {
            float dt = freq / sr;
            // Hard-cap dt below Nyquist's-worth so PolyBLEP windows don't
            // overlap the whole period at insane pitches.
            if (dt > 0.45f) dt = 0.45f;
            else if (dt < 0f) dt = 0f;

            // ── Saw ───────────────────────────────────────────────────────
            // Naive saw: 2*t - 1 ramps from -1 to +1; PolyBLEP smooths the
            // wrap discontinuity at t=0.
            float saw = 2f * _phase - 1f;
            saw -= PolyBlep(_phase, dt);

            // ── Pulse (with PWM) ──────────────────────────────────────────
            // Two discontinuities per period:
            //   rising edge at phase = 0  (low → high)
            //   falling edge at phase = pwm (high → low)
            float pulse = (_phase < pwm) ? 1f : -1f;
            pulse += PolyBlep(_phase, dt);
            float pulseFallEdge = _phase + 1f - pwm;
            if (pulseFallEdge >= 1f) pulseFallEdge -= 1f;
            pulse -= PolyBlep(pulseFallEdge, dt);

            // Remove the pulse's intrinsic DC at the source (Core §43.2a). A
            // pulse of width pwm has mean 2·pwm − 1, so a narrow PWM setting
            // sits far off zero (pwm = 0.26 → −0.48). Subtracting it makes the
            // pulse zero-mean by construction — exactly and instantly at any
            // PWM rate, so LFO→PWM sweeps carry no wandering offset either.
            pulse -= (2f * pwm - 1f);

            // ── Sub-oscillator ────────────────────────────────────────────
            // 1-oct down → freq/2, 2-oct down → freq/4. For the narrow pulse
            // variant we use the same phase but a 25% duty cycle.
            int   subDiv   = (subType == 0) ? 2 : 4;
            float subDt    = dt / subDiv;
            float subWidth = (subType == 2) ? 0.25f : 0.5f;

            float sub = (_subPhase < subWidth) ? 1f : -1f;
            sub += PolyBlep(_subPhase, subDt);
            float subFallEdge = _subPhase + 1f - subWidth;
            if (subFallEdge >= 1f) subFallEdge -= 1f;
            sub -= PolyBlep(subFallEdge, subDt);

            // Same treatment for the sub: mean is 2·subWidth − 1. That's 0 for
            // the 50% square variants, but −0.5 for the 25% narrow pulse — a
            // large standing offset whenever subType == 2.
            sub -= (2f * subWidth - 1f);

            // ── Noise (xorshift32, mapped to ±1) ──────────────────────────
            uint r = _rng;
            r ^= r << 13;
            r ^= r >> 17;
            r ^= r << 5;
            _rng = r;
            // Cast keeps the bit pattern; mul by 1/2^31 maps to ~[-1, 1).
            float noise = unchecked((int)r) * (1f / 2147483648f);

            // ── Mix ───────────────────────────────────────────────────────
            float mixed = pulse * pulseLevel
                        + saw   * sawLevel
                        + sub   * subLevel
                        + noise * noiseLevel;

            // ── Advance phases ────────────────────────────────────────────
            _phase += dt;
            if (_phase >= 1f) _phase -= 1f;
            _subPhase += subDt;
            if (_subPhase >= 1f) _subPhase -= 1f;

            return mixed;
        }
    }
}
