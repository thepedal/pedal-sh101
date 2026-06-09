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
        // The Pow/Tan calls only fire when fc, res, or sr actually changes.
        int   _cSr  = 0;
        float _cFc  = -1f;
        float _cRes = -1f;
        float _G, _G4, _oneMinusG, _k;

        public void Reset()
        {
            _s1 = _s2 = _s3 = _s4 = 0f;
        }

        public float Process(float input, float fc, float res, int sr)
        {
            if (fc != _cFc || res != _cRes || sr != _cSr)
            {
                _cSr = sr; _cFc = fc; _cRes = res;

                // Bilinear pre-warp. Cap the argument to tan() to stay below
                // the asymptote (≈ π/2). 1.55 keeps us safely below.
                float wd = MathF.PI * fc / sr;
                if (wd > 1.55f) wd = 1.55f;
                float g  = MathF.Tan(wd);
                _G          = g / (1f + g);
                _G4         = _G * _G * _G * _G;
                _oneMinusG  = 1f - _G;
                _k          = res * 4f;     // 0..1 input → 0..4 (4 = self-osc)
            }

            float G          = _G;
            float oneMinusG  = _oneMinusG;
            float k          = _k;
            float G4         = _G4;
            float G2         = G * G;
            float G3         = G2 * G;

            // Closed-form ZDF resolution for y4 (see header derivation).
            float S = G3 * oneMinusG * _s1
                    + G2 * oneMinusG * _s2
                    + G  * oneMinusG * _s3
                    +      oneMinusG * _s4;

            float y4cf = (G4 * input + S) / (1f + k * G4);
            float inputWithFb = input - k * y4cf;

            // Re-evaluate per stage to update integrator states.
            // y_k = G*x + (1-G)*s ; s_new = 2*y - s
            float y1 = G * inputWithFb + oneMinusG * _s1;
            _s1 = 2f * y1 - _s1;

            float y2 = G * y1 + oneMinusG * _s2;
            _s2 = 2f * y2 - _s2;

            float y3 = G * y2 + oneMinusG * _s3;
            _s3 = 2f * y3 - _s3;

            float y4 = G * y3 + oneMinusG * _s4;
            _s4 = 2f * y4 - _s4;

            // y4 here equals y4cf in exact arithmetic; in float it may
            // differ by ulps. Either is fine to return.
            return y4;
        }
    }
}
