namespace WilliamsSynth
{
    /// <summary>
    /// Implements the LITEN and NOISE synthesis routines from VSNDRM1.SRC.
    ///
    /// Commands handled:
    ///   $11  LITE   → LITEN routine  — rising-frequency noise burst (~0.7 s)
    ///   $15  APPEAR → LITEN routine  — falling-frequency noise burst (~1.1 s)
    ///   $14  LASER  → NOISE  routine — constant-frequency decaying noise (~0.5 s)
    ///
    /// ── LITEN (LIGHTNING AND APPEAR) algorithm (VSNDRM1.SRC lines 265–289) ─────────────────────────
    ///   SOUND = $FF at start.
    ///   Inner loop (CYCNT iterations): clock LFSR; if carry=1, COM SOUND; busy-wait LFREQ.
    ///   After each inner pass: LFREQ += DFREQ; exit when LFREQ wraps to 0.
    ///
    /// ── NOISE algorithm (VSNDRM1.SRC lines 305–334) ──────────────────────────
    ///   Inner loop (CYCNT iterations): clock LFSR; output NAMP if carry=1, else $00;
    ///   busy-wait NFRQ1 iterations.
    ///   After each inner pass: NAMP -= DECAY (8-bit wrap); exit when NAMP = 0.
    ///   If NFFLG != 0: NFRQ1 is constant; if NFFLG = 0: NFRQ1++ (pitch falls).
    ///
    /// Cycle timing (6800 @ 894 886 Hz):
    ///   LITEN:  ≈ 39 + 6 × LFREQ  CPU cycles per LFSR bit
    ///   NOISE:  ≈ 44 + 8 × NFRQ1  CPU cycles per LFSR bit
    /// </summary>
    public sealed class NoiseGenerator : ISoundGenerator
    {
        // ── Mode ─────────────────────────────────────────────────────────────
        private enum NoiseMode { None, Liten, Noise }
        private NoiseMode _mode;

        // ── LFSR state (HI:LO) — standard polynomial shared by LITEN & NOISE ─
        // Same polynomial as LFSR16: feedback = ((LO>>3) XOR LO) bit 0
        private byte _hi;
        private byte _lo;

        // ── LITEN state ───────────────────────────────────────────────────────
        private byte  _lfreq;              // current delay value
        private sbyte _dfreq;              // signed per-pass delta to LFREQ
        private byte  _cycntLiten;         // total inner iterations per pass
        private byte  _cycntLitenCurrent;  // remaining inner iterations this pass

        // ── NOISE state ───────────────────────────────────────────────────────
        private byte   _namp;              // current amplitude (8-bit, wrapping decay)
        private byte   _decay;             // per-pass decrement to NAMP
        private ushort _nfrq1;             // delay counter (16-bit)
        private ushort _nfrq1Init;         // reset value (used when NFFLG set)
        private byte   _cycntNoise;        // total inner iterations per pass
        private byte   _cycntNoiseCurrent; // remaining inner iterations this pass
        private bool   _nfflg;             // if true: NFRQ1 is constant (reset each pass)

        // ── Shared output ─────────────────────────────────────────────────────
        private byte _sound;   // current DAC byte (driven into the output buffer)
        private bool _active;

        // ── Cycle accumulator ─────────────────────────────────────────────────
        private double _cycleAccum;
        private double _cyclesPerBit;   // 6800 cycles between LFSR clocks

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            _cycleAccum = 0.0;
            _hi = 0x00;
            _lo = 0x01;   // non-zero LFSR seed

            switch (commandId)
            {
                // ── LITE ($11) — LITEN: DFREQ=+1, LFREQ=1, CYCNT=3 ──────────
                case SoundCommand.LITE:
                    _mode              = NoiseMode.Liten;
                    _dfreq             = 1;
                    _lfreq             = 1;
                    _cycntLiten        = 3;
                    _cycntLitenCurrent = 3;
                    _sound             = 0xFF;  // LDAA #$FF; STAA SOUND in LITEN
                    _active            = true;
                    UpdateLitenCycles();
                    break;

                // ── APPEAR ($15) — LITEN: DFREQ=$FE(−2), LFREQ=$C0, CYCNT=16 ─
                case SoundCommand.APPEAR:
                    _mode              = NoiseMode.Liten;
                    _dfreq             = unchecked((sbyte)0xFE); // −2 signed
                    _lfreq             = 0xC0;
                    _cycntLiten        = 0x10;
                    _cycntLitenCurrent = 0x10;
                    _sound             = 0xFF;
                    _active            = true;
                    UpdateLitenCycles();
                    break;

                // ── LASER ($14) — NOISE: DECAY=1, NFRQ1=1, NAMP=$FF, CYCNT=32 ─
                case SoundCommand.LASER:
                    _mode              = NoiseMode.Noise;
                    _decay             = 0x01;
                    _nfrq1             = 0x0001;
                    _nfrq1Init         = 0x0001;
                    _namp              = 0xFF;
                    _cycntNoise        = 0x20;
                    _cycntNoiseCurrent = 0x20;
                    _nfflg             = true;  // NFFLG=$20 != 0 → pitch drops
                    _sound             = 0x80;  // silence until first LFSR bit
                    _active            = true;
                    UpdateNoiseCycles();
                    break;
            }
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
                while (_cycleAccum >= _cyclesPerBit && _active)
                {
                    _cycleAccum -= _cyclesPerBit;
                    if (_mode == NoiseMode.Liten) StepLiten();
                    else                          StepNoise();
                }
            }
        }

        // ── LITEN step ────────────────────────────────────────────────────────
        // Each call = one LFSR clock = one LITE3 inner bit.
        private void StepLiten()
        {
            // LFSR clock; carry = old LO bit 0 (output bit)
            bool carry = ClockLFSR();

            // BCC LITE2 / COM SOUND: complement output if carry set
            if (carry)
                _sound = (byte)~_sound;

            // Inner CYCNT counter (DECB / BNE LITE1)
            _cycntLitenCurrent = (byte)(_cycntLitenCurrent - 1);
            if (_cycntLitenCurrent != 0)
                return;   // more inner bits remain

            // Inner loop exhausted — update LFREQ (ADDA DFREQ / STAA LFREQ)
            _cycntLitenCurrent = _cycntLiten;
            byte newLfreq = unchecked((byte)(_lfreq + _dfreq));
            _lfreq = newLfreq;

            if (newLfreq == 0)
            {
                // BNE LITE0 not taken → RTS → sound ends
                _active = false;
                _sound  = 0x80;
            }
            else
            {
                UpdateLitenCycles();
            }
        }

        // ── NOISE step ────────────────────────────────────────────────────────
        // Each call = one LFSR clock = one NOISE1 inner bit.
        private void StepNoise()
        {
            // LFSR clock; carry = output bit
            bool carry = ClockLFSR();

            // LDAA #0 / BCC NOISE2 / LDAA NAMP: binary output
            _sound = carry ? _namp : (byte)0x00;

            // Inner CYCNT counter (DECB / BNE NOISE1)
            _cycntNoiseCurrent = (byte)(_cycntNoiseCurrent - 1);
            if (_cycntNoiseCurrent != 0)
                return;   // more inner bits remain

            // Inner loop exhausted — decay NAMP (LDAB NAMP / SUBB DECAY / STAB NAMP)
            _cycntNoiseCurrent = _cycntNoise;
            _namp = unchecked((byte)(_namp - _decay));  // 8-bit wrap; authentic hardware behaviour
            if (_namp == 0)
            {
                _active = false;
                _sound  = 0x80;
                return;
            }

            // Update NFRQ1 (INX / STX NFRQ1 vs. reload from NFRQ1 init)
            if (_nfflg)
            {
                ushort next = (ushort)(_nfrq1 + 1);
                _nfrq1 = next == 0 ? (ushort)1 : next; // guard: NFRQ1=0 would freeze
            }
            else
            {
                _nfrq1 = _nfrq1Init;            // constant pitch: reset delay counter
            }
            UpdateNoiseCycles();
        }

        // ── LFSR — standard polynomial (LDAA LO; LSRA×3; EORA LO; LSRA; ROR HI; ROR LO) ──
        // Returns: old LO bit 0 (carry from final ROR LO = output bit).
        private bool ClockLFSR()
        {
            byte a     = (byte)((_lo >> 3) ^ _lo);     // feedback polynomial
            bool carry = (a & 0x01) != 0;

            bool nextCarry = (_hi & 0x01) != 0;
            _hi   = (byte)((_hi >> 1) | (carry ? 0x80 : 0x00));
            carry = nextCarry;

            bool outputBit = (_lo & 0x01) != 0;        // old LO bit 0
            _lo   = (byte)((_lo >> 1) | (carry ? 0x80 : 0x00));

            return outputBit;
        }

        // ── Timing helpers ────────────────────────────────────────────────────
        private void UpdateLitenCycles()
        {
            // LITEN inner-bit cost: LFSR (~26) + BCC/COM (~5) + LDAA LFREQ + DECA/BNE loop + DECB/BNE
            // ≈ 39 + 6×LFREQ cycles  (+ ~14/CYCNT outer overhead, negligible)
            _cyclesPerBit = 39.0 + 6.0 * _lfreq;
        }

        private void UpdateNoiseCycles()
        {
            // NOISE inner-bit cost: LFSR (~26) + output path (~8) + LDX (~4) + DEX/BNE loop + DECB/BNE
            // ≈ 44 + 8×NFRQ1 cycles
            _cyclesPerBit = 44.0 + 8.0 * _nfrq1;
        }
    }
}
