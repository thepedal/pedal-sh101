// LFO.cs — Pedal SH101 low-frequency oscillator
//
// Phase-accumulator LFO with four waveforms (Triangle, Square, Sample &
// Hold, Noise). Output is in [-1, 1].
//
// On the SH-101, the LFO has a "Delay" parameter that holds the LFO output
// at zero for a time after note-on, then fades it in. We approximate this
// with a hard zero-output countdown — simpler than a fade-in and acoustically
// close enough at typical mod depths.
//
// Phase is free-running across notes (authentic to the SH-101). NoteOn()
// only resets the delay countdown, not the phase.
//
// LFO values are computed once per audio sample. They could be downsampled
// for CPU savings, but at 44.1 kHz the cost of the inner switch is in the
// noise even with thousands of voices.

using System;

namespace PedalSH101
{
    public sealed class LFO
    {
        public enum Wave { Triangle = 0, Square = 1, SampleHold = 2, Noise = 3 }

        public Wave  Waveform     { get; set; } = Wave.Triangle;
        public float Rate         { get; set; } = 1f;     // Hz

        float _phase   = 0f;
        float _shValue = 0f;
        int   _delayCountdown = 0;

        // Independent RNG so noise/S&H aren't synced to the VCO's noise.
        uint _rng = 0xDEADBEEFu;

        public void NoteOn(int delaySamples)
        {
            _delayCountdown = delaySamples;
        }

        public void Reset()
        {
            _phase = 0f;
            _shValue = 0f;
            _delayCountdown = 0;
        }

        public float Process(int sr)
        {
            if (_delayCountdown > 0) { _delayCountdown--; return 0f; }

            float dt = Rate / sr;
            // Clamp absurd rates so we don't lose phase precision.
            if (dt > 0.49f) dt = 0.49f;

            bool wrapped = false;
            _phase += dt;
            if (_phase >= 1f) { _phase -= 1f; wrapped = true; }

            float val;
            switch (Waveform)
            {
                case Wave.Triangle:
                    // 0..0.5 ramps up -1 → +1, 0.5..1.0 ramps back +1 → -1
                    val = (_phase < 0.5f) ? (_phase * 4f - 1f)
                                          : (3f - _phase * 4f);
                    break;
                case Wave.Square:
                    val = (_phase < 0.5f) ? 1f : -1f;
                    break;
                case Wave.SampleHold:
                    if (wrapped) _shValue = NextRand();
                    val = _shValue;
                    break;
                case Wave.Noise:
                    val = NextRand();
                    break;
                default:
                    val = 0f;
                    break;
            }
            return val;
        }

        float NextRand()
        {
            uint r = _rng;
            r ^= r << 13;
            r ^= r >> 17;
            r ^= r << 5;
            _rng = r;
            return unchecked((int)r) * (1f / 2147483648f);
        }
    }
}
