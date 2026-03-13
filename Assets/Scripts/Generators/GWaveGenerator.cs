using System;

namespace WilliamsSynth
{
    /// <summary>
    /// Implements the GWAVE / GOUT synthesis routine from VSNDRM1.SRC.
    ///
    /// Handles commands $01–$0D and $11 (BONV), $13 (TRBV).
    /// Parameters are read from SoundParameterTables.GWaveParams[commandId].
    ///
    /// ── Algorithm (VSNDRM1.SRC lines 782–863) ────────────────────────────────
    ///
    ///   Each command selects a waveform (GWVTAB entry) and a frequency pattern
    ///   (GFRTAB sub-table). The main loop:
    ///
    ///   1. For each entry in the GFRTAB pattern:
    ///      - GPER = GFRTAB[pos] + FOFSET  (period byte, 8-bit, wrapping)
    ///      - Play the waveform GCCNT complete passes at this GPER
    ///      - Each waveform byte: delay GPER × ~6 CPU cycles, then write byte to DAC
    ///
    ///   2. When the GFRTAB pattern is exhausted: one "echo pass" is complete.
    ///      - Apply amplitude decay (WVDECA): each RAM byte -= GECDEC × (ROM_byte >> 4)
    ///      - Decrement echo counter; if more echoes remain, restart from GFRTAB start.
    ///
    ///   3. After all echoes, if GDFINC != 0:
    ///      - DEC GDCNT (8-bit wrap): GDCNT=0→255 (255 more steps); GDCNT=1→0 (terminate).
    ///      - If GDCNT decremented to 0 → terminate (BEQ GEND1).
    ///      - Otherwise: FOFSET += GDFINC (signed) — shifts the entire frequency pattern.
    ///      - Optionally refresh waveform from ROM + pre-decay (if GECDEC != 0).
    ///      - Restart the echo loop with the new frequency offset.
    ///
    ///   4. Terminate when GDCNT or GDFINC exhausted.
    ///
    /// ── 8-bit wrapping counters (critical for authenticity) ───────────────────
    ///   GECHO=0 → GECNT=0 → DEC wraps to 255 → 256 total echo passes.
    ///   GDCNT=0 → DEC wraps to 255 → 255 freq-mod continuation steps (for sweeps).
    ///   GDCNT=1 → DEC gives 0    → BEQ GEND1 terminates immediately.
    ///   Both counters use (_cnt - 1) & 0xFF to replicate the 6800 DEC/BNE behaviour.
    ///
    /// ── SVTAB byte layout (SoundParameterTables.GWaveParams[cmd]) ─────────────
    ///   [0] EchoCycle  — upper nibble: echo count (GECHO); lower nibble: cycle count (GCCNT)
    ///   [1] DecayWave  — upper nibble: decay factor (GECDEC); lower nibble: waveform index
    ///   [2] PreDecay   — pre-decay factor applied once before first echo (PRDECA)
    ///   [3] FreqInc    — signed frequency offset increment per mod-step (GDFINC)
    ///   [4] DeltaCnt   — number of frequency mod steps (GDCNT)
    ///   [5] FreqLen    — length of GFRTAB sub-pattern for this sound
    ///   [6] FreqOffset — start index into FrequencyTables.GFRTAB
    ///
    /// ── WVDECA (VSNDRM1.SRC lines 877–905) ───────────────────────────────────
    ///   For each waveform byte i:
    ///     ram[i] -= GECDEC × (rom[i] >> 4)   (8-bit wrap, authentic)
    ///
    /// ── Timing ────────────────────────────────────────────────────────────────
    ///   Per waveform byte: ≈ 23 + 6 × GPER  CPU cycles  (GPER=0 treated as 256).
    ///   Fundamental frequency ≈ 894886 / ((23 + 6×GPER) × waveform_length) Hz.
    /// </summary>
    public sealed class GWaveGenerator : ISoundGenerator
    {
        // ── Waveform buffers ──────────────────────────────────────────────────
        private byte[] _waveRom;   // original bytes — read-only reference for decay maths
        private byte[] _waveRam;   // mutable copy — amplitude decayed in-place
        private int    _waveLen;

        // ── Loop state ────────────────────────────────────────────────────────
        private int  _wavePos;    // current position in waveform (0.._waveLen-1)
        private int  _cycleCnt;   // remaining waveform passes at current freq entry
        private int  _gccnt;      // passes per freq entry (from SVTAB)
        // _echoCnt uses 8-bit wrapping semantics matching 6800 DEC instruction:
        //   GECHO=0 → GECNT=0 → DEC wraps to 255 → 256 total echo passes.
        //   Stored as int; decremented via (_echoCnt - 1) & 0xFF to match hardware.
        private int  _echoCnt;    // remaining echoes (8-bit wrap; 0 → 256 passes)
        private int  _gecho;      // echo count loaded from SVTAB (0 means 256 passes)
        private byte _gecdec;     // amplitude decay factor per echo
        private byte _prdeca;     // pre-decay factor (applied once at Trigger)

        // ── Frequency state ───────────────────────────────────────────────────
        private int  _freqStart;  // GFRTAB start index for this command
        private int  _freqEnd;    // GFRTAB end index (exclusive)
        private int  _freqPos;    // current GFRTAB index
        private byte _fofset;     // running frequency offset (FOFSET; += GDFINC each mod step)
        private byte _gper;       // current period (GFRTAB[freqPos] + _fofset)
        private sbyte _gdfinc;    // signed per-step frequency increment
        // _gdcnt uses 8-bit wrapping semantics matching 6800 DEC instruction:
        //   GDCNT=0 → DEC wraps to 255 → 255 freq-mod continuation steps.
        //   GDCNT=1 → DEC gives 0 → BEQ GEND1 terminates immediately.
        private int   _gdcnt;     // frequency mod steps remaining (8-bit wrap)

        // ── Output + timing ───────────────────────────────────────────────────
        private byte   _sound;          // current DAC output byte
        private bool   _active;
        private double _cycleAccum;
        private double _cyclesPerByte;  // 6800 CPU cycles per waveform byte output

        // ── WVDECA hold state ─────────────────────────────────────────────────
        // On the original 6800 hardware, WVDECA + restart overhead consume real
        // CPU cycles between echo passes (DAC holds last value = silence burst).
        // Two distinct hold calculations:
        //   Echo-restart  (GWT4): 84 + wL×(67+12×gecdec)  [WVDECA(gecdec) only]
        //   Freq-mod-restart (GW3): 167 + wL×(91+12×prdeca)  [WVTRAN+WVDECA(prdeca)]
        //
        // For SV3 (GS72=72 bytes, GECDEC=1): 84+72×79=5772 cycles ≈ 6.45 ms ✓
        // Verification: burst period = waveform(5112) + hold(5772) = 10884 cycles
        //               = 12.16 ms → 82.2 Hz ✓ (matches dfstartsnd.wav upward crossings).
        private bool   _inDecayHold;
        private double _decaHoldRemaining;  // CPU cycles of hold left

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            byte[] p = SoundParameterTables.GWaveParams[commandId];
            if (p == null || (p[0] == 0 && p[1] == 0 && p[5] == 0))
                return;   // not a GWAVE command

            // ── SVTAB byte 0: EchoCycle ──
            _gccnt  = p[0] & 0x0F;              // lower nibble: cycle count
            _gecho  = (p[0] >> 4) & 0x0F;       // upper nibble: echo count
            if (_gccnt == 0) _gccnt = 1;        // guard against zero (GCCNT=0 undefined in ROM)
            // NOTE: do NOT clamp _gecho. GECHO=0 is authentic (e.g. SV3 $0A).
            // On 6800, GECNT=0 then DEC wraps to 255 → 256 total echo passes.

            // ── SVTAB byte 1: DecayWave ──
            int waveIndex = p[1] & 0x0F;
            _gecdec = (byte)((p[1] >> 4) & 0x0F);

            // ── SVTAB bytes 2–6 ──
            _prdeca  = p[2];
            _gdfinc  = unchecked((sbyte)p[3]);   // signed increment
            _gdcnt   = p[4];
            int freqLen = p[5];
            int freqOff = p[6];

            // ── Select and copy waveform ──
            _waveRom = SelectWaveform(waveIndex);
            _waveLen = _waveRom.Length;
            _waveRam = new byte[_waveLen];
            Array.Copy(_waveRom, _waveRam, _waveLen);

            // ── Apply pre-decay (GWLD calls WVDECA with PRDECA) ──
            if (_prdeca > 0) ApplyDecay(_prdeca);

            // ── Freq table range ──
            _freqStart = freqOff;
            _freqEnd   = freqOff + freqLen;
            _fofset    = 0;

            // ── Reset loop state ──
            // _echoCnt uses 8-bit wrap semantics: store as int, decrement via & 0xFF.
            // GECHO=0 → _echoCnt=0 → first DEC: (0-1)&0xFF=255 → 256 total passes.
            _echoCnt           = _gecho;
            _cycleAccum        = 0.0;
            _wavePos           = 0;
            _sound             = 0x80;   // silence until first byte
            _inDecayHold       = false;  // clear any hold from a previous trigger
            _decaHoldRemaining = 0.0;

            // Load first freq table entry.
            // _cycleCnt must be initialised here — it is not reset by LoadFreqEntry().
            _freqPos  = _freqStart;
            _cycleCnt = _gccnt;   // FIX: was uninitialized; first entry needs gccnt cycles
            LoadFreqEntry();

            _active = true;
        }

        public void Stop()
        {
            _active         = false;
            _inDecayHold    = false;
            _sound          = 0x80;
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

                if (_inDecayHold)
                {
                    // ── WVDECA hold: DAC frozen while 6800 runs WVDECA + GWT4/GPLAY ──
                    // _sound stays at last waveform byte; we count down the CPU cycles.
                    _decaHoldRemaining -= cyclesPerSample;
                    if (_decaHoldRemaining <= 0.0)
                    {
                        // Hold finished: carry overshoot into the next waveform's timer.
                        _inDecayHold = false;
                        _cycleAccum  = -_decaHoldRemaining;  // overshoot → head start
                        LoadFreqEntry();
                        // Immediately consume any whole-byte intervals in the overshoot.
                        while (_cycleAccum >= _cyclesPerByte && _active && !_inDecayHold)
                        {
                            _cycleAccum -= _cyclesPerByte;
                            StepWaveform();
                        }
                    }
                }
                else
                {
                    _cycleAccum += cyclesPerSample;
                    while (_cycleAccum >= _cyclesPerByte && _active && !_inDecayHold)
                    {
                        _cycleAccum -= _cyclesPerByte;
                        StepWaveform();
                    }
                }
            }
        }

        // ── Advance one waveform byte output ──────────────────────────────────
        // Corresponds to one GOUTLP iteration (GPRLP delay → LDAA ,X → STAA SOUND).
        private void StepWaveform()
        {
            // Output the current waveform byte
            _sound = _waveRam[_wavePos];

            // Advance waveform position
            _wavePos++;
            if (_wavePos < _waveLen)
                return;   // mid-waveform: nothing else to do this step

            // ── End of waveform pass ──────────────────────────────────────────
            _wavePos = 0;
            _cycleCnt--;
            if (_cycleCnt > 0)
                return;   // more passes at this GPER remain (BRA GOUT path)

            // ── All GCCNT cycles at this freq entry done ──────────────────────
            // Advance to the next freq table entry (GPLAY path)
            _freqPos++;
            if (_freqPos < _freqEnd)
            {
                // Load next GFRTAB entry
                _cycleCnt = _gccnt;
                LoadFreqEntry();
            }
            else
            {
                // Freq table exhausted → GEND: decay + echo
                OnFreqTableEnd();
            }
        }

        // ── GEND: amplitude decay + echo bookkeeping ─────────────────────────
        // Assembly: WVDECA; DEC GECNT; BNE GWT4; (check freq mod or terminate)
        private void OnFreqTableEnd()
        {
            // Apply amplitude decay to in-place waveform (instantaneous in C#).
            if (_gecdec > 0) ApplyDecay(_gecdec);

            // 8-bit DEC GECNT then BNE GWT4 (authentic 6800 behaviour).
            // GECNT=0 wraps to 255 → BNE taken → 256 total passes for GECHO=0.
            _echoCnt = (_echoCnt - 1) & 0xFF;
            if (_echoCnt != 0)
            {
                // More echoes: set up restart state, then enter echo-restart hold.
                // LoadFreqEntry() will be called when the hold expires in FillBuffer.
                _freqPos  = _freqStart;
                _cycleCnt = _gccnt;
                StartEchoRestartHold();
                return;
            }

            // ── All echoes done (GECNT wrapped to 0) ──────────────────────────
            // GEND50: check B2FLG (bonus-stop flag — always 0 in our emulator)
            // then GDFINC for frequency-mod continuation.

            if (_gdfinc == 0)
            {
                _active = false;   // GEND1: GDFINC==0 → no freq mod, terminate
                _sound  = 0x80;
                return;
            }

            // DEC GDCNT then BEQ GEND1 (authentic 6800 behaviour).
            // GDCNT=0 wraps to 255 → BEQ not taken → 255 freq-mod steps.
            // GDCNT=1 wraps to 0   → BEQ taken    → terminate immediately.
            _gdcnt = (_gdcnt - 1) & 0xFF;
            if (_gdcnt == 0)
            {
                _active = false;   // GEND1: GDCNT decremented to 0 → terminate
                _sound  = 0x80;
                return;
            }

            // FOFSET += GDFINC  (8-bit wrap — ADDA FOFSET in assembly)
            _fofset = unchecked((byte)(_fofset + _gdfinc));

            // ── GW0/GW1: trim the active freq-table range to only valid entries ────
            // Assembly GEND61→GW0/GW1 (VSNDRM1.SRC lines 832–862):
            //   Scans [GWFRQ, FRQEND) and finds the contiguous valid sub-range.
            //   For DEC (GDFINC<0): valid = carry AND non-zero (GFRTAB[i]+FOFSET ∈ [257,511]).
            //   For INC (GDFINC>0): valid = no carry         (GFRTAB[i]+FOFSET ∈ [1,255]).
            //   If no valid entry is found → RTS (terminate).
            if (!TrimFreqRange())
            {
                _active = false;
                _sound  = 0x80;
                return;
            }

            // GW3: If GECDEC != 0 → WVTRAN (reload ROM→RAM) + WVDECA(prdeca).
            if (_gecdec != 0)
            {
                Array.Copy(_waveRom, _waveRam, _waveLen);
                if (_prdeca > 0) ApplyDecay(_prdeca);
            }

            // GEND0: JMP GWAVE — restart echo loop with new freq range.
            _echoCnt  = _gecho;
            _freqPos  = _freqStart;
            _cycleCnt = _gccnt;
            StartFreqModHold();
        }

        // ── GW0/GW1: trim freq-table range after each freq-mod step ──────────
        // Scans [_freqStart, _freqEnd) and finds the first contiguous sub-range
        // of "valid" entries under the current FOFSET, updating _freqStart and
        // _freqEnd in-place.  Returns false if no valid entry exists (terminate).
        //
        // DEC path (GDFINC < 0) — GW1 branch:
        //   valid = carry AND non-zero  (sum ∈ [257,511])
        //   BEQ GW2  : carry=1, result=0 → invalid (sum wrapped to 0)
        //   BCS GW2A : carry=1, result≠0 → valid
        //   fall-thru: carry=0           → invalid (not yet in range)
        //
        // INC path (GDFINC ≥ 0) — fall-through after TST GDFINC / BMI GW1:
        //   valid = no carry  (sum ∈ [0,255])
        //   BCS GW2 : carry=1 → invalid (overflowed past 255)
        //   BRA GW2A: carry=0 → valid
        private bool TrimFreqRange()
        {
            bool startFound = false;
            int  newStart   = _freqStart;

            for (int i = _freqStart; i < _freqEnd; i++)
            {
                int sum   = FrequencyTables.GFRTAB[i] + _fofset;
                bool carry = sum > 255;
                bool zero  = (sum & 0xFF) == 0;

                bool valid = (_gdfinc < 0)
                    ? (carry && !zero)   // DEC: GW1 — BCS GW2A path
                    : !carry;            // INC: GW0 — BRA GW2A path

                if (valid)   // GW2A: valid entry
                {
                    if (!startFound)
                    {
                        newStart   = i;
                        startFound = true;
                    }
                    // keep scanning — end = furthest valid entry + 1 or old _freqEnd
                }
                else         // GW2: invalid entry
                {
                    if (startFound)
                    {
                        // GW3: first invalid after start → mark end here
                        _freqStart = newStart;
                        _freqEnd   = i;
                        return true;
                    }
                    // start not found yet — keep scanning
                }
            }

            if (!startFound) return false;   // RTS: no valid entries → terminate

            // GW3 via end-of-loop: new end = old _freqEnd (X was already at FRQEND)
            _freqStart = newStart;
            // _freqEnd unchanged
            return true;
        }

        // ── StartEchoRestartHold ─────────────────────────────────────────────
        // Hold for the GWT4 (echo-restart) path.
        // Simulates: BSR WVDECA(gecdec) + DEC GECNT + BNE GWT4 + GPLAY setup.
        //
        // Inner WVDLP1 loop: SBA(2)+DEC(6)+BNE(4) = 12 cycles per iteration.
        // Per-byte cost = 67 + 12×gecdec  (outer overhead 67, inner 12×N).
        // Fixed overhead = 84 cycles (BSR+setup+RTS+restart).
        //
        // Validated against dfstartsnd.wav (burst period 12.168 ms = 82.2 Hz):
        //   SV3 / GS72 / GECDEC=1: 84 + 72×(67+12) = 84+72×79 = 5772 cycles ✓
        //
        // NOTE: 71+8N and 67+12N both give 79 for N=1, so validation passes
        // for either formula; the 12-cycle inner loop is correct per assembly.
        private void StartEchoRestartHold()
        {
            _decaHoldRemaining = _gecdec > 0
                ? 84.0 + _waveLen * (67.0 + 12.0 * _gecdec)
                : 79.0;   // gecdec=0: WVDECA exits at BEQ WVDCX, minimal overhead
            _inDecayHold = true;
        }

        // ── StartFreqModHold ──────────────────────────────────────────────────
        // Hold for the GW3 (freq-mod-restart) path.
        // Simulates: BSR WVTRAN (ROM→RAM copy) + LDAA PRDECA
        //          + BSR WVDECA(prdeca) + JMP GWAVE + restart overhead.
        //
        // WVTRAN body cost ≈ 64 + 24×waveLen  (JSR TRANS loop ≈ 24 cycles/byte).
        // WVDECA(prdeca>0) = 38 + waveLen×(67+12×prdeca).
        // Fixed overhead (GW3 prefix 15 + LDAA 4 + restart 46) = 65 cycles.
        // Total (prdeca>0): 65 + (64+24×wL) + 38 + wL×(67+12×p)
        //                 = 167 + wL×(91+12×prdeca)
        //
        // For PROTV (wL=72, prdeca=17, gecdec=3):
        //   167 + 72×(91+204) = 167 + 72×295 = 167+21240 = 21407 cycles ≈ 23.9 ms
        // vs echo-restart: 84 + 72×(67+36) = 84+7416 = 7500 cycles ≈ 8.4 ms
        private void StartFreqModHold()
        {
            double holdCycles;
            if (_gecdec > 0)
            {
                holdCycles = _prdeca > 0
                    ? 167.0 + _waveLen * (91.0  + 12.0 * _prdeca)  // WVTRAN + WVDECA(prdeca)
                    : 143.0 + _waveLen * 24.0;                      // WVTRAN only (prdeca=0 → BEQ WVDCX)
            }
            else
            {
                // GECDEC=0: GW3 skips WVTRAN and WVDECA (BEQ GEND0 taken).
                // Just GW3 prefix (15) + JMP GWAVE (4) + restart (~29) ≈ 48 cycles.
                holdCycles = 48.0;
            }
            _decaHoldRemaining = holdCycles;
            _inDecayHold = true;
        }

        // ── Load one GFRTAB entry into GPER and update cycle timing ──────────
        // Assembly GPLAY: LDAA ,X; ADDA FOFSET; STAA GPER; LDAB GCCNT
        private void LoadFreqEntry()
        {
            // Clamp freqPos to array bounds
            int safePos = Math.Min(_freqPos, FrequencyTables.GFRTAB.Length - 1);
            _gper = unchecked((byte)(FrequencyTables.GFRTAB[safePos] + _fofset));
            UpdateCyclesPerByte();
        }

        // ── WVDECA: amplitude decay ───────────────────────────────────────────
        // For each byte i: ram[i] -= decayFactor × (rom[i] >> 4)
        // 8-bit arithmetic — wrap is authentic (SBA in 6800 assembly).
        private void ApplyDecay(byte decayFactor)
        {
            for (int i = 0; i < _waveLen; i++)
            {
                int sub = decayFactor * (_waveRom[i] >> 4);
                _waveRam[i] = unchecked((byte)(_waveRam[i] - sub));
            }
        }

        // ── cyclesPerByte = 23 + 6×GPER (GPER=0 → 256 iterations in GPRLP) ─
        private void UpdateCyclesPerByte()
        {
            int effectivePer = _gper == 0 ? 256 : _gper;
            _cyclesPerByte = 23.0 + 6.0 * effectivePer;
        }

        // ── Waveform selection (GWVTAB index 0–6) ─────────────────────────────
        // Matches the ROM GWVTAB layout: GS2, GSSQ2, GS1, GS12, GSQ22, GS72, GS17
        private static byte[] SelectWaveform(int index)
        {
            return index switch
            {
                0 => WaveformTables.GS2,
                1 => WaveformTables.GSSQ2,
                2 => WaveformTables.GS1,
                3 => WaveformTables.GS12,
                4 => WaveformTables.GSQ22,
                5 => WaveformTables.GS72,
                6 => WaveformTables.GS17,
                _ => WaveformTables.GS72,  // fallback to primary 72-byte waveform
            };
        }
    }
}
