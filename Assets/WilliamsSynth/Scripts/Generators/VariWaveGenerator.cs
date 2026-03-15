using System;

namespace WilliamsSynth
{
    /// <summary>
    /// Implements the VARI (variable duty-cycle PWM square wave) synthesis routine
    /// from VSNDRM1.SRC lines 206–246.
    ///
    /// Handles commands $1C–$1F: SAW, FOSHIT, QUASAR, CABSHK.
    /// Parameters loaded from VariParameterTables.VariPresets[(commandId - 0x1C)].
    ///
    /// ── Algorithm (VSNDRM1.SRC lines 206–246) ─────────────────────────────────────
    ///
    ///   Generates an asymmetric square wave:
    ///     LO phase: LOCNT inner iterations  (SOUND = ~VAMP)
    ///     HI phase: HICNT inner iterations  (SOUND = VAMP)
    ///
    ///   X register counts down from SWPDT.  Every inner iteration decrements X and
    ///   the current half-period counter (A) simultaneously.  When A hits 0 the phase
    ///   toggles.  When X hits 0 a sweep update fires:
    ///
    ///     LOCNT += LODT   (8-bit wrap)
    ///     HICNT += HIDT   (8-bit wrap)
    ///
    ///   After the update: if HICNT == HIEN → check LOMOD restart or terminate.
    ///   If HICNT != HIEN → reload X = SWPDT and continue.
    ///
    ///   LOMOD restart (LOMOD != 0):
    ///     LOPER += LOMOD  (8-bit wrap)
    ///     If LOPER != 0: reset LOCNT = LOPER, HICNT = HIPER (VAR0) and continue.
    ///     If LOPER == 0: terminate.
    ///
    ///   SOUND normalisation at sweep boundary (VSWEEP):
    ///     Ensures bit 7 of SOUND is set before V0LP re-entry so that the
    ///     subsequent COM SOUND reliably enters the LO (< 0x80) phase.
    ///
    /// ── VVECT byte layout (confirmed from LOCRAM equates in VSNDRM1.SRC) ──────────
    ///   [0] LOPER   — initial LO half-period
    ///   [1] HIPER   — initial HI half-period
    ///   [2] LODT    — signed per-sweep delta added to LOCNT
    ///   [3] HIDT    — signed per-sweep delta added to HICNT
    ///   [4] HIEN    — stop value for HICNT (checked after each sweep update)
    ///   [5] SWPDT_H — sweep timer high byte (16-bit big-endian)
    ///   [6] SWPDT_L — sweep timer low byte
    ///   [7] LOMOD   — signed LO modulation step (0 = no restart → terminate)
    ///   [8] VAMP    — amplitude / initial DAC output byte
    ///
    /// ── Timing ────────────────────────────────────────────────────────────────────
    ///   6800 branch instructions (BEQ, BNE, BRA) are ALWAYS 4 cycles — taken or not.
    ///   Inner loop base: DEX(4) + BEQ(4) + DECA(2) + BNE(4) = 14 cycles per iteration.
    ///   Effective cycles per iteration (dynamically recomputed on every period change):
    ///     _cyclesPerIter = 14 + 22/(LOCNT + HICNT) + 40/SWPDT
    ///   where 22 = period setup overhead (V0LP 9 + LO→HI 9 + BRA 4) per full cycle
    ///   and   40 = VSWEEP overhead amortised over SWPDT inner iterations
    ///   Validated: QUASAR initial → 372.7 Hz computed, 372.5 Hz measured on hardware.
    ///   Period-counter = 0 → 256 iterations (8-bit wrap; authentic 6800 DECA behaviour)
    /// </summary>
    public sealed class VariWaveGenerator : ISoundGenerator
    {
        // ── Parameters (from VVECT) ───────────────────────────────────────────
        private byte   _loper;   // initial LO half-period (reset at each VAR0)
        private byte   _hiper;   // initial HI half-period (constant across restarts)
        private sbyte  _lodt;    // LO sweep delta per VSWEEP
        private sbyte  _hidt;    // HI sweep delta per VSWEEP
        private byte   _hien;    // stop value for HICNT
        private ushort _swpdt;   // inner-loop iterations per sweep window
        private sbyte  _lomod;   // LO modulation step on restart (0 = terminate)
        private byte   _vamp;    // amplitude / initial SOUND

        // ── Working counters ──────────────────────────────────────────────────
        private byte   _locnt;       // current LO half-period (swept from LOPER each sweep)
        private byte   _hicnt;       // current HI half-period (swept from HIPER each restart)
        private ushort _xCount;      // X register — counts down from SWPDT
        private byte   _phaseCount;  // A register — counts down current half-period
        private bool   _inLoPhase;   // true = LO (SOUND low), false = HI (SOUND high)

        // ── Output / state ────────────────────────────────────────────────────
        private byte   _sound;
        private bool   _active;
        private double _cycleAccum;

        // Dynamic effective cycles per inner iteration.
        // Base = 14 (DEX(4) + BEQ(4) + DECA(2) + BNE(4) — 6800 branches always 4 cycles).
        // +22/(LOCNT+HICNT): period overhead (V0LP entry + LO→HI + BRA) amortised.
        // +40/SWPDT:         VSWEEP event overhead amortised over the sweep window.
        // Recalculated in InitVar0() and DoVSweep() whenever _locnt/_hicnt change.
        private double _cyclesPerIter;

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            int index = commandId - SoundCommand.SAW;   // 0x1C offset
            if (index < 0 || index >= VariParameterTables.VariPresets.Length) return;

            byte[] p = VariParameterTables.VariPresets[index];
            _loper  = p[0];
            _hiper  = p[1];
            _lodt   = unchecked((sbyte)p[2]);
            _hidt   = unchecked((sbyte)p[3]);
            _hien   = p[4];
            _swpdt  = (ushort)((p[5] << 8) | p[6]);   // 16-bit big-endian
            _lomod  = unchecked((sbyte)p[7]);
            _vamp   = p[8];

            // Guard: SWPDT=0 would mean LDX #0 → 65536 iterations (we clamp to 1)
            if (_swpdt == 0) _swpdt = 1;

            _cycleAccum = 0.0;
            _sound      = _vamp;   // initial SOUND = VAMP (before first COM SOUND)
            InitVar0();
            _active = true;
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
                while (_cycleAccum >= _cyclesPerIter && _active)
                {
                    _cycleAccum -= _cyclesPerIter;
                    StepVari();
                }
            }
        }

        // ── VAR0: reload LOCNT/HICNT from LOPER/HIPER, then V0 ───────────────
        // Assembly VAR0: LDAA LOPER/STAA LOCNT; LDAA HIPER/STAA HICNT; (fall to V0)
        private void InitVar0()
        {
            _locnt = _loper;
            _hicnt = _hiper;
            UpdateCyclesPerIter();
            InitV0();
        }

        // ── V0/V0LP: reload X = SWPDT, COM SOUND, enter LO phase ─────────────
        // Assembly V0: LDX SWPDT; V0LP: LDAA LOCNT; COM SOUND
        // Called both on initial trigger and on VSWEEP re-entry.
        // On initial call _sound = VAMP; COM → ~VAMP (LO).
        // On VSWEEP re-entry _sound was normalised to bit7=1; COM → bit7=0 (LO).
        private void InitV0()
        {
            _xCount     = _swpdt;
            _sound      = (byte)~_sound;   // COM SOUND
            _phaseCount = _locnt;
            _inLoPhase  = true;
        }

        // ── StepVari: one V1/V2 inner-loop iteration ──────────────────────────
        private void StepVari()
        {
            // DEX — always; check X before A (matches 6800 BEQ priority)
            _xCount--;
            if (_xCount == 0)
            {
                DoVSweep();
                return;
            }

            // DECA — count down current half-period (8-bit wrap gives 256 for period=0)
            _phaseCount = (byte)(_phaseCount - 1);
            if (_phaseCount != 0) return;

            // Half-period expired → COM SOUND, switch to the other half
            _sound = (byte)~_sound;
            if (_inLoPhase)
            {
                // End of LO phase → HI phase: load HICNT
                _phaseCount = _hicnt;
                _inLoPhase  = false;
            }
            else
            {
                // End of HI phase → LO phase (BRA V0LP): reload LOCNT, COM already done
                _phaseCount = _locnt;
                _inLoPhase  = true;
            }
        }

        // ── UpdateCyclesPerIter: recalculate effective CPU cycles per inner iteration ──
        //
        // Derivation from VSNDRM1.SRC VARI inner loops (V1 / V2):
        //   V1/V2 per iteration: DEX(4) + BEQ(4) + DECA(2) + BNE(4) = 14 cycles
        //
        //   IMPORTANT: Motorola 6800 branch instructions are ALWAYS 4 cycles
        //   (taken or not-taken).  This is unlike the 6502 where untaken = 2 cycles.
        //   Using 3 cycles for untaken (a common mistake) gives a 7–8% pitch error.
        //
        // Period overhead amortised per iteration:  22 / (LOCNT + HICNT)
        //   V0LP entry:    LDAA LOCNT(3) + COM SOUND(6)            =  9 cy/period
        //   LO→HI switch: COM SOUND(6) + LDAA HICNT(3)            =  9 cy/period
        //   HI→LO switch: BRA V0LP(4)                             =  4 cy/period
        //   Total overhead per full square-wave period             = 22 cy/period
        //
        // VSWEEP overhead amortised per iteration:  40 / SWPDT
        //   Trigger iteration: DEX(4)+BEQ_T(4) = 8 cy  (skips DECA+BNE)
        //   VSWEEP code + LDX SWPDT:           ≈ 46 cy
        //   Net extra vs normal 14-cy iter     ≈ 40 cy per SWPDT-window
        //
        // Validated against hardware recording:
        //   QUASAR initial (LOCNT=40, HICNT=129, SWPDT=512):
        //     CPI = 14 + 22/169 + 40/512 = 14.208  →  894886/(14.208×169) = 372.7 Hz
        //     Hardware measured: 372.5 Hz  ✓
        //
        // Period counters of 0 represent 256 on authentic hardware (8-bit wrap).
        private void UpdateCyclesPerIter()
        {
            int effLo = _locnt == 0 ? 256 : _locnt;
            int effHi = _hicnt == 0 ? 256 : _hicnt;
            _cyclesPerIter = 14.0 + 22.0 / (effLo + effHi) + 40.0 / _swpdt;
        }

        // ── VSWEEP: update sweep counters, check stop condition ───────────────
        // Assembly: normalise SOUND; LOCNT+=LODT; HICNT+=HIDT; CMPA HIEN; BNE V0;
        //           check LOMOD or VARX
        private void DoVSweep()
        {
            // Normalise SOUND: force bit 7 set so COM SOUND in InitV0 gives LO (bit7=0)
            // Assembly: LDAA SOUND; BMI VS1; COMA; VS1: ADDA #0; STAA SOUND
            if ((_sound & 0x80) == 0)
                _sound = (byte)~_sound;

            // Update working sweep counters (8-bit wrap — authentic hardware arithmetic)
            _locnt = unchecked((byte)(_locnt + _lodt));
            _hicnt = unchecked((byte)(_hicnt + _hidt));

            // Recompute effective cycles/iter for the new period lengths.
            // Must happen here (not in InitV0) so both the InitV0 and InitVar0 paths benefit.
            UpdateCyclesPerIter();

            if (_hicnt != _hien)
            {
                // BNE V0: continue with updated periods
                InitV0();
                return;
            }

            // HICNT == HIEN — sweep complete
            if (_lomod == 0)
            {
                // VARX: no restart → terminate
                _active = false;
                _sound  = 0x80;
                return;
            }

            // LOMOD restart: LOPER += LOMOD; if LOPER == 0 → exit; else VAR0
            _loper = unchecked((byte)(_loper + _lomod));
            if (_loper == 0)
            {
                _active = false;
                _sound  = 0x80;
                return;
            }

            // VAR0: reset LOCNT = new LOPER, HICNT = original HIPER, reload X
            InitVar0();
        }
    }
}
