namespace WilliamsSynth
{
    /// <summary>
    /// Implements the FNOISE filtered-noise synthesis routine from VSNDRM1.SRC.
    ///
    /// Commands handled:
    ///   $0E  BG1    — persistent engine hum    (FMAX=1,   FDFLG=0, DSFLG=0, loops until Stop())
    ///   $15  THRUST — persistent thrust noise  (FMAX=3,   FDFLG=0, DSFLG=0, loops until Stop())
    ///   $16  CANNON — one-shot cannon crack    (FMAX=$FF, FDFLG=1, DSFLG=1, finite ~0.3 s)
    ///
    /// ── Algorithm (VSNDRM1.SRC lines 360–426) ────────────────────────────────
    ///
    ///   FNOISE is a first-order ramp-follower noise filter:
    ///
    ///   1. LFSR is clocked to produce a new target value (LO byte).
    ///      Polynomial: feedback = ((SOUND>>3) XOR LO) bit 0.
    ///      Unlike the standard LFSR, the current SOUND output feeds back into
    ///      the polynomial, coupling noise and output for a "filtered" character.
    ///
    ///   2. A 16-bit accumulator A:B ramps toward LO each step by adding or
    ///      subtracting FHI:FLO (slope up if A ≤ LO, slope down if A > LO).
    ///
    ///   3. When A reaches LO (crosses threshold), snap A = LO, output LO,
    ///      clock LFSR again for the next target.
    ///
    ///   4. FHI = FMAX if DSFLG=0; FHI = FMAX AND HI if DSFLG=1 (distortion).
    ///      Larger FHI:FLO → faster ramp → higher bandwidth → harsher sound.
    ///
    ///   5. SAMPC counts slope steps. When expired:
    ///      - FDFLG=0: reload SAMPC and continue (infinite loop, BG1/THRUST).
    ///      - FDFLG=1: decay FMAX:FLO by factor 7/8 (≈ 3× right-shift + negate + add).
    ///        Exit when FMAX=0 AND FLO ≤ 7.
    ///
    /// Cycle timing: ≈ 27 CPU cycles per slope step (6800 @ 894 886 Hz).
    /// </summary>
    public sealed class FilteredNoiseGenerator : ISoundGenerator
    {
        // ── LFSR state (HI:LO) ── FNOISE uses a signal-dependent polynomial ──
        private byte _hi;
        private byte _lo;

        // ── Ramp-follower accumulator (A = coarse/output, B = fine) ──────────
        private byte _accumA;   // coarse byte — directly output to DAC as SOUND
        private byte _accumB;   // fine byte (sub-threshold precision)

        // ── Frequency parameters ──────────────────────────────────────────────
        private byte _fmax;     // initial / max step size (high byte of FHI:FLO step)
        private byte _flo;      // step size low byte (starts 0; grows during decay)

        // ── Sample counter ────────────────────────────────────────────────────
        private ushort _sampc;      // running counter (decremented each slope step)
        private ushort _sampcInit;  // reload value (from STX SAMPC at FNOISE entry)

        // ── Control flags ─────────────────────────────────────────────────────
        private byte _fdflg;    // frequency decay flag: 0 = none (loop), 1 = decay
        private byte _dsflg;    // distortion flag:      0 = FHI=FMAX, 1 = FHI=FMAX&HI

        private bool _active;

        // ── Cycle accumulator ─────────────────────────────────────────────────
        private double _cycleAccum;
        private const double CyclesPerStep = 27.0; // ≈ cycles per slope step (DEX + slope + compare)

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            _cycleAccum = 0.0;
            _hi    = 0x00;
            _lo    = 0x01;   // non-zero LFSR seed
            _accumA = 0x80;  // mid-rail silence (typical SOUND value before trigger)
            _accumB = 0x00;
            _flo   = 0;      // CLR FLO at FNOISE entry

            switch (commandId)
            {
                // ── BG1 ($0E): engine hum, loops indefinitely ─────────────────
                // BG1:  LDAB #1; STAB BG1FLG; CLRA; STAA DSFLG; BRA FNOISE
                // At FNOISE entry: ACCA=0 (FDFLG=0), ACCB=1 (FMAX=1), X≈0 (SAMPC≈0)
                case SoundCommand.BG1:
                    _fmax     = 1;
                    _fdflg    = 0;
                    _dsflg    = 0;
                    _sampcInit = 0;          // X was 1 (BG1FLG write) → effectively 0 in NOISE
                    _sampc    = 0;
                    _active   = true;
                    break;

                // ── THRUST ($15): directional push noise, loops indefinitely ──
                // THRUST: CLRA; STAA DSFLG; LDAB #3; BRA FNOISE
                // At FNOISE entry: ACCA=0 (FDFLG=0), ACCB=3 (FMAX=3), X≈0 (SAMPC≈0)
                case SoundCommand.THRUST:
                    _fmax      = 3;
                    _fdflg     = 0;
                    _dsflg     = 0;
                    _sampcInit = 0;
                    _sampc     = 0;
                    _active    = true;
                    break;

                // ── CANNON ($16): sharp crack, decays to silence ───────────────
                // CANNON: LDAA #1; STAA DSFLG; LDX #1000; LDAA #1; LDAB #$FF; BRA FNOISE
                // At FNOISE entry: ACCA=1 (FDFLG=1), ACCB=$FF (FMAX=$FF), X=1000 (SAMPC=1000)
                case SoundCommand.CANNON:
                    _fmax      = 0xFF;
                    _fdflg     = 1;
                    _dsflg     = 1;
                    _sampcInit = 1000;
                    _sampc     = 1000;
                    _active    = true;
                    break;
            }

            // Prime the LFSR with one clock so _lo has a meaningful first target
            ClockLFSR();
        }

        public void Stop()
        {
            _active = false;
            _accumA = 0x80;
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
                // SOUND = current accumulator coarse byte
                buffer[offset + i] = DAC1408.ToFloat(_accumA);

                _cycleAccum += cyclesPerSample;
                while (_cycleAccum >= CyclesPerStep && _active)
                {
                    _cycleAccum -= CyclesPerStep;
                    StepFNoise();
                }
            }
        }

        // ── One FNOISE slope step ─────────────────────────────────────────────
        // Corresponds to one DEX iteration in FNOIS3 or FNOIS4.
        private void StepFNoise()
        {
            // LDAB FMAX; TST DSFLG; BEQ FNOIS2; ANDB HI
            byte fhi = (_dsflg != 0) ? (byte)(_fmax & _hi) : _fmax;

            // Guard: if step is zero, clock LFSR and continue rather than hanging
            if (fhi == 0 && _flo == 0)
            {
                ClockLFSR();
                DecrementSampc();
                return;
            }

            // CMPA LO; BHI FNOIS4 — slope direction
            if (_accumA <= _lo)
            {
                // FNOIS3: slope up — ADDB FLO; ADCA FHI (16-bit add)
                int newB  = _accumB + _flo;
                bool carB = newB > 0xFF;
                _accumB   = (byte)(newB & 0xFF);
                int newA  = _accumA + fhi + (carB ? 1 : 0);
                bool carA = newA > 0xFF;
                _accumA   = (byte)(newA & 0xFF);

                // BCS FNOIS5 (carry) or CMPA LO; BLS FNOIS3 (still below)
                bool crossed = carA || ((byte)newA > _lo);
                if (crossed)
                    SnapToLo();  // FNOIS5
            }
            else
            {
                // FNOIS4: slope down — SUBB FLO; SBCA FHI (16-bit subtract)
                int newB  = _accumB - _flo;
                bool borB = newB < 0;
                _accumB   = (byte)((newB + 256) & 0xFF);
                int newA  = _accumA - fhi - (borB ? 1 : 0);
                bool borA = newA < 0;
                _accumA   = (byte)((newA + 256) & 0xFF);

                // BCS FNOIS5 (borrow) or CMPA LO; BHI FNOIS4 (still above)
                bool crossed = borA || ((byte)newA <= _lo);
                if (crossed)
                    SnapToLo();  // FNOIS5
            }

            DecrementSampc();
        }

        // ── FNOIS5: snap accumulator to LO, clock LFSR for next target ────────
        // LDAA LO; STAA SOUND; BRA FNOIS1 (which clocks LFSR)
        private void SnapToLo()
        {
            _accumA = _lo;
            ClockLFSR();
        }

        // ── DEX / BEQ FNOIS6 — sample count management ───────────────────────
        private void DecrementSampc()
        {
            _sampc = (ushort)(_sampc - 1);     // 16-bit wrap authentic (ushort underflow)
            if (_sampc != 0)
                return;

            // FNOIS6: LDAB FDFLG; BEQ FNOIS1 (if 0, don't decay, loop)
            if (_fdflg != 0)
            {
                // Frequency decay: new FMAX:FLO = FMAX:FLO × 7/8
                // Assembly: LSRA;RORB (×3) → COMA;NEGB;SBCA #-1 → ADDB FLO;ADCA FMAX
                ApplyFrequencyDecay();

                // Exit condition: FMAX=0 AND FLO≤7 (FLO=7 is the fixed-point of the ×7/8 decay)
                if (_fmax == 0 && _flo <= 7)
                {
                    _active = false;
                    _accumA = 0x80;
                    return;
                }
            }

            // Reload sample counter (FNOIS0: LDX SAMPC) — also covers FDFLG=0 loop
            _sampc = _sampcInit;
        }

        // ── Frequency decay: FMAX:FLO ← FMAX:FLO − (FMAX:FLO >> 3) ─────────
        // Equivalent to FMAX:FLO × 7/8.  Uses integer shift to stay exact.
        private void ApplyFrequencyDecay()
        {
            // 16-bit value: FHI_ext:FLO = FMAX:FLO
            int val16   = (_fmax << 8) | _flo;
            int decay16 = val16 >> 3;           // logical shift right 3 (divide by 8)
            int newVal  = val16 - decay16;      // × 7/8

            _fmax = (byte)((newVal >> 8) & 0xFF);
            _flo  = (byte)(newVal & 0xFF);
        }

        // ── FNOISE LFSR (signal-dependent polynomial) ─────────────────────────
        // VSNDRM1.SRC FNOIS1: TAB (B=SOUND); LSRB×3; EORB LO; LSRB; ROR HI; ROR LO
        // Polynomial: feedback = ((SOUND>>3) XOR LO) bit 0.
        // After clock: _lo = new LO (used as ramp target), _hi = new HI (used for distortion).
        private void ClockLFSR()
        {
            byte b     = (byte)((_accumA >> 3) ^ _lo);   // (SOUND>>3) XOR LO
            bool carry = (b & 0x01) != 0;                  // feedback bit

            bool nextCarry = (_hi & 0x01) != 0;
            _hi    = (byte)((_hi >> 1) | (carry ? 0x80 : 0x00));
            carry  = nextCarry;

            // ROR LO: carry → LO bit 7; LO's old bit 0 → carry (discarded in FNOISE)
            _lo    = (byte)((_lo >> 1) | (carry ? 0x80 : 0x00));
        }
    }
}
