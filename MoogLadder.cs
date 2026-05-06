// MoogLadder.cs — Pedal SH101 4-pole VCF
//
// Vadim Zavalishin's TPT (topology-preserving transform) Moog ladder:
//   - four cascaded one-pole lowpass stages, each implemented as a TPT
//     integrator (zero-delay-feedback within the stage)
//   - global feedback k*y4 around the whole ladder, resolved analytically
//     in closed form for stability up to and including self-oscillation
//
// References:
//   Zavalishin, "The Art of VA Filter Design" (free PDF)
//   Pirkle, "Designing Software Synthesizer Plug-Ins in C++"
//
// Closed-form derivation of y4 (the analytical bit that makes ZDF work):
//
//   Each TPT 1-pole LPF stage: y_k = G * x_k + (1-G) * s_k
//   Cascaded with input_with_fb = input - k*y4:
//
//     y1 = G * (input - k*y4)        + (1-G) * s1
//     y2 = G * y1                    + (1-G) * s2
//     y3 = G * y2                    + (1-G) * s3
//     y4 = G * y3                    + (1-G) * s4
//
//   Substitute through:
//     y4 = G^4 * (input - k*y4) + S
//        where S = G^3*(1-G)*s1 + G^2*(1-G)*s2 + G*(1-G)*s3 + (1-G)*s4
//
//   Solve for y4:
//     y4 * (1 + k*G^4) = G^4 * input + S
//     y4 = (G^4 * input + S) / (1 + k*G^4)
//
// We then re-compute the per-stage outputs to update the integrator states.
// This is mathematically equivalent to the closed form but lets the states
// advance correctly. Pre-warping uses bilinear: g = tan(π*fc/sr).

using System;

namespace PedalSH101
{
    public sealed class MoogLadder
    {
        // TPT integrator states (one per stage)
        float _s1, _s2, _s3, _s4;

        // ── Coefficient cache (PedalComp §6) ──────────────────────────────
        // Pow/Tan and the per-stage product chain only fire when fc, res, or
        // sr actually changes. With control-rate updates (caller updates
        // every N samples instead of per-sample) this cuts the dominant
        // MathF.Tan cost by N×.
        int   _cSr  = 0;
        float _cFc  = -1f;
        float _cRes = -1f;
        float _G, _G4, _oneMinusG, _k;
        // Pre-multiplied stage products — depend only on G, cached together
        float _G3oneMinusG, _G2oneMinusG, _GoneMinusG;

        public void Reset()
        {
            _s1 = _s2 = _s3 = _s4 = 0f;
        }

        /// <summary>
        /// Recompute filter coefficients. Call when fc, res, or sr changes —
        /// typically at control rate (every N samples) since MathF.Tan is the
        /// dominant cost in this filter.
        /// </summary>
        public void UpdateCoefs(float fc, float res, int sr)
        {
            if (fc == _cFc && res == _cRes && sr == _cSr) return;
            _cSr = sr; _cFc = fc; _cRes = res;

            // Bilinear pre-warp. Cap the argument to tan() to stay below
            // the asymptote (≈ π/2). 1.55 keeps us safely below.
            float wd = MathF.PI * fc / sr;
            if (wd > 1.55f) wd = 1.55f;
            float g  = MathF.Tan(wd);
            _G          = g / (1f + g);
            _oneMinusG  = 1f - _G;

            float G2 = _G * _G;
            float G3 = G2 * _G;
            _G4          = G2 * G2;
            _G3oneMinusG = G3 * _oneMinusG;
            _G2oneMinusG = G2 * _oneMinusG;
            _GoneMinusG  = _G * _oneMinusG;

            _k = res * 4f;     // 0..1 input → 0..4 (4 = self-osc)
        }

        /// <summary>
        /// Process one sample using the most recently updated coefficients.
        /// Caller must have called UpdateCoefs at least once before the first
        /// Process call.
        /// </summary>
        public float Process(float input)
        {
            // Closed-form ZDF resolution for y4 (see header derivation).
            // S = G³(1-G)·s1 + G²(1-G)·s2 + G(1-G)·s3 + (1-G)·s4
            //     — products precomputed in UpdateCoefs.
            float S = _G3oneMinusG * _s1
                    + _G2oneMinusG * _s2
                    + _GoneMinusG  * _s3
                    + _oneMinusG   * _s4;

            float y4cf = (_G4 * input + S) / (1f + _k * _G4);
            float inputWithFb = input - _k * y4cf;

            // Re-evaluate per stage to update integrator states.
            // y_k = G*x + (1-G)*s ; s_new = 2*y - s
            float y1 = _G * inputWithFb + _oneMinusG * _s1;
            _s1 = 2f * y1 - _s1;

            float y2 = _G * y1 + _oneMinusG * _s2;
            _s2 = 2f * y2 - _s2;

            float y3 = _G * y2 + _oneMinusG * _s3;
            _s3 = 2f * y3 - _s3;

            float y4 = _G * y3 + _oneMinusG * _s4;
            _s4 = 2f * y4 - _s4;

            // y4 here equals y4cf in exact arithmetic; in float it may
            // differ by ulps. Either is fine to return.
            return y4;
        }
    }
}
