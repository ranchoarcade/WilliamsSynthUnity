namespace WilliamsSynth
{
    /// <summary>
    /// Implements the RADIO synthesis routine from VSNDRM1.SRC lines 430–452.
    ///
    /// Handles command $17 (RADIO — warbling radio/static sweep effect).
    ///
    /// ── Algorithm ────────────────────────────────────────────────────────────────
    ///
    ///   16-bit phase accumulator TEMPA:B (A = high byte, B = low byte).
    ///   TEMPX = 16-bit sweep frequency, starts at 100 ($0064).
    ///
    ///   Each iteration (RADIO1):
    ///     B     += TEMPX_lo              (8-bit, sets carry)
    ///     TEMPA += TEMPX_hi + carry_B    (8-bit, sets carry A)
    ///     if carry_A: TEMPX++            (RADIO2: raise frequency)
    ///       if TEMPX == 0: terminate     (BEQ RADIO4: TEMPX wrapped $FFFF→0)
    ///     index = TEMPA & $0F            (lower nibble → RADSND table index 0–15)
    ///     SOUND = RADSND[index]
    ///
    ///   Timing-equalization: BCS/BCC paths padded to same cycle count
    ///   (RADIO2: INX + BEQ vs RADIO3: BRA *+2 + BRA RADIO3).
    ///
    /// ── RADSND waveform (16 bytes) ────────────────────────────────────────────
    ///   $8C $5B $B6 $40 $BF $49 $A4 $73 $73 $A4 $49 $BF $40 $B6 $5B $8C
    ///   Symmetric, sine-like; produces the radio tuning texture.
    ///
    /// ── Sweep behaviour ───────────────────────────────────────────────────────
    ///   TEMPX accelerates over time: each carry from the 16-bit add increments
    ///   TEMPX, causing TEMPA to advance faster → the RADSND table scans faster
    ///   → frequency rises. Terminates when TEMPX overflows $FFFF to 0.
    ///
    /// ── Timing ────────────────────────────────────────────────────────────────
    ///   ≈ 69 CPU cycles per RADIO1 iteration (ADDB + ADCA + LDX + equalized BCS
    ///     path + STX + ANDA + ADDA + STAA + LDX + LDAA + STAA + BRA)
    /// </summary>
    public sealed class RadioGenerator : ISoundGenerator
    {
        // ── Phase accumulator ────────────────────────────────────────────────
        private ushort _tempx;   // 16-bit sweep frequency counter
        private byte   _tempa;   // high byte of 16-bit phase (used as RADSND index)
        private byte   _b;       // low byte of 16-bit phase (B register)

        // ── Output / state ────────────────────────────────────────────────────
        private byte   _sound;
        private bool   _active;
        private double _cycleAccum;

        // ~69 CPU cycles per RADIO1 loop iteration
        private const double CyclesPerStep = 69.0;

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            _tempx      = 100;   // LDX #100 = $0064
            _tempa      = 0;
            _b          = 0;
            _sound      = 0x80;
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
                    StepRadio();
                }
            }
        }

        // ── One RADIO1 loop iteration ─────────────────────────────────────────
        private void StepRadio()
        {
            // ADDB TEMPX_lo — low-byte phase advance; carry feeds ADCA
            int bNew   = _b + (byte)_tempx;
            bool carry = bNew > 0xFF;
            _b         = (byte)bNew;

            // ADCA TEMPX_hi — high-byte phase advance using carry from ADDB
            int aNew    = _tempa + (byte)(_tempx >> 8) + (carry ? 1 : 0);
            bool carryA = aNew > 0xFF;
            _tempa      = (byte)aNew;

            // BCS RADIO2: carry from ADCA → increment TEMPX (raise frequency)
            if (carryA)
            {
                _tempx++;
                if (_tempx == 0)   // BEQ RADIO4: TEMPX wrapped $FFFF→0 → done
                {
                    _active = false;
                    _sound  = 0x80;
                    return;
                }
            }

            // ANDA #$F; index RADSND table by lower nibble of TEMPA
            _sound = WaveformTables.RADSND[_tempa & 0x0F];
        }
    }
}
