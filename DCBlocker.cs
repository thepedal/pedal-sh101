// DCBlocker.cs — one-pole DC-blocking high-pass
//
// The SH-101 VCO carries a steady DC offset whenever the pulse or sub is
// run at a narrow duty: a pulse of duty d has an intrinsic mean of 2d-1,
// so a ~26% pulse sits at about -0.5 of full scale. The Moog ladder is a
// low-pass (unity gain at DC), so that offset passes straight through to
// the output. Real SH-101 hardware AC-couples the output and never lets
// this reach the converters — this class is the software equivalent.
//
// Placement (see PedalSH101.Work): applied to the VCF output, BEFORE the
// VCA, on purpose. The offset is a property of the tone, not of the
// amplitude envelope, so blocking it pre-VCA removes a constant DC from a
// constant-DC signal — the blocker stays at steady state across
// attack/decay/sustain/release and there is no envelope-scaled DC ramp to
// thump at note-on. Blocking post-VCA would make the blocker chase the
// enveloped DC (0 → -0.5·gain and back) and click on every note.
//
// The state is intentionally NOT reset per note. Like the AC-coupling cap
// it models, it holds its charge between notes: successive notes carry a
// similar DC, so the running state already has it removed and there is no
// transient. (Resetting to zero at note-on would instead pass one full-DC
// sample that then settles over the corner's time constant — audible as a
// note-on "whoomp" under a fast attack. So we don't.)
//
// Difference equation:  y[n] = x[n] - x[n-1] + R * y[n-1]
//   zero at DC (z = 1), pole at z = R, corner ≈ (1-R)·sr / 2π.

using System;

namespace PedalSH101
{
    public sealed class DCBlocker
    {
        float _xPrev;
        float _yPrev;
        float _r = 0.9999f;   // sane default until SetCorner runs

        // R = exp(-2π·fc/sr). Call when the sample rate changes
        // (Core §29 — done from UpdateCoefs on srChanged).
        public void SetCorner(float fcHz, int sr)
        {
            _r = MathF.Exp(-2f * MathF.PI * fcHz / sr);
        }

        // Wipe history. Not called per note (see header); available as a
        // panic/host-reset hook.
        public void Reset()
        {
            _xPrev = 0f;
            _yPrev = 0f;
        }

        public float Process(float x)
        {
            float y = x - _xPrev + _r * _yPrev;
            _xPrev = x;

            // Denormal flush (Core §30). The pole at R < 1 makes y decay
            // toward zero as the input quietens; left alone the tail can
            // slip into denormal range and trap to microcode. An explicit
            // compare-flush is more robust than the ±1e-25 trick under the
            // JIT. 1e-20 is far below audibility and far above the denormal
            // boundary.
            if (y > -1e-20f && y < 1e-20f) y = 0f;
            _yPrev = y;

            return y;
        }
    }
}
