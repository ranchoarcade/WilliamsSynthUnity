namespace WilliamsSynth
{
    /// <summary>
    /// Implements the HYPER synthesis routine from VSNDRM1.SRC lines 456–471.
    ///
    /// Handles command $18 (HYPER — hyperspace entry sweep).
    ///
    /// ── Algorithm ────────────────────────────────────────────────────────────────
    ///
    ///   TEMPA: outer phase counter (0–127); exits when TEMPA bit7 = 1 (>= 128).
    ///   A:     inner step counter (0–127) per HYPER1 cycle.
    ///
    ///   Init: SOUND = 0, TEMPA = 0, A = 0.
    ///
    ///   Each A-step (one HYPER4 delay + phase check):
    ///     if A == TEMPA: COM SOUND   (phase edge — toggles at the matched step)
    ///     delay 18 iterations (HYPER4: DECB loop)
    ///     A++
    ///     if A bit7 = 1 (A >= 128):
    ///       COM SOUND               (cycle-end edge)
    ///       A = 0; TEMPA++
    ///       if TEMPA bit7 = 1: terminate
    ///
    ///   Output per A-step: current SOUND value (constant for the 18-delay window).
    ///
    /// ── Waveform ──────────────────────────────────────────────────────────────
    ///   SOUND alternates between $00 (−1.0) and $FF (+1.0):
    ///     Cycle N (TEMPA = N):  $FF for (128 − N) steps, then $00 for N steps.
    ///   Duty cycle sweeps from ~100% (TEMPA=0) down to ~0.8% (TEMPA=127),
    ///   producing the characteristic "rising sweep" hyperspace texture.
    ///
    /// ── Timing ────────────────────────────────────────────────────────────────
    ///   ≈ 122 CPU cycles per A-step (CMPA + BNE + [COM] + LDAB#18 + 18×DECB/BNE
    ///     + INCA + BPL)
    ///   128 A-steps per HYPER1 cycle ≈ 128 × 122 = 15616 cycles (~0.0175 s/cycle)
    ///   Total duration: 128 cycles × 0.0175 ≈ 2.24 seconds
    /// </summary>
    public sealed class HyperGenerator : ISoundGenerator
    {
        // ── Phase counters ────────────────────────────────────────────────────
        private byte _tempa;   // outer phase counter (0..127; terminates at 128)
        private byte _a;       // inner step counter (0..127 per HYPER1 cycle)

        // ── Output / state ────────────────────────────────────────────────────
        private byte   _sound;
        private bool   _active;
        private double _cycleAccum;

        // ~122 CPU cycles per A-step:
        // CMPA(3) + BNE(3/4) + [COM(6)] + LDAB#18(2) + HYPER4(18 iter × ~6 cycles)
        // + INCA(2) + BPL(3/4) ≈ 122
        private const double CyclesPerStep = 122.0;

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            // CLRA; STAA SOUND; STAA TEMPA — all start at 0
            _tempa      = 0;
            _a          = 0;
            _sound      = 0;
            _cycleAccum = 0.0;
            _active     = true;
        }

        public void Stop()
        {
            _active = false;
            _sound  = 0x80;
        }

        // ── FillBuffer ────────────────────────────────────────────────────────
        public void FillBuffer(float[] buffer, int offset, int count, int sampleRate)
        {
            if (!_active)
            {
                for (int i = 0; i < count; i++)
                    buffer[offset + i] = 0f;
                return;
            }

            double cyclesPerSample = 894886.0 / sampleRate;

            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = DAC1408.ToFloat(_sound);

                _cycleAccum += cyclesPerSample;
                while (_cycleAccum >= CyclesPerStep && _active)
                {
                    _cycleAccum -= CyclesPerStep;
                    StepHyper();
                }
            }
        }

        // ── One A-step ────────────────────────────────────────────────────────
        // Corresponds to: HYPER2 phase-edge check + HYPER4 delay + INCA + BPL test
        private void StepHyper()
        {
            // HYPER2: CMPA TEMPA; BNE HYPER3; COM SOUND
            if (_a == _tempa)
                _sound = (byte)~_sound;

            // HYPER4 delay modelled by CyclesPerStep; INCA
            _a++;
            if ((_a & 0x80) == 0) return;   // BPL HYPER2: A < 128, continue

            // End of HYPER1 cycle (A reached 128):
            // COM SOUND (cycle-end toggle); HYPER1: reset A; INC TEMPA; BPL HYPER1
            _sound = (byte)~_sound;
            _a     = 0;
            _tempa++;

            if ((_tempa & 0x80) != 0)   // BPL HYPER1 fails → RTS
            {
                _active = false;
                _sound  = 0x80;
            }
        }
    }
}
