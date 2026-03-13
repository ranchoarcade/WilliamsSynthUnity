namespace WilliamsSynth
{
    /// <summary>
    /// Implements the SCREAM synthesis routine from VSNDRM1.SRC lines 475–515.
    ///
    /// Handles command $19 (SCREAM — astronaut scream / 4-voice polyphonic echo).
    ///
    /// ── Algorithm ────────────────────────────────────────────────────────────────
    ///
    ///   Uses STABLE: 4 pairs of (FREQ, TIMER) bytes at 2-byte offsets.
    ///   ECHOS = 4; voices indexed 0–3.
    ///
    ///   Init: all STABLE zeroed; voice 0 FREQ = $40.
    ///
    ///   Each outer step (SCREM2):
    ///     TEMPA = $80  (amplitude, halved per voice)
    ///     B     = 0    (output accumulator)
    ///     For each voice i (0–3):
    ///       TIMER[i] += FREQ[i]   (8-bit wrap)
    ///       if (TIMER[i] bit7 = 1): B += TEMPA   (phase accumulator fires)
    ///       TEMPA >>= 1           (halve amplitude for next voice)
    ///     SOUND = B
    ///
    ///   After 256 outer steps (TEMPB wraps 0→0): frequency decay (SCREM5):
    ///     For each voice with FREQ != 0:
    ///       if FREQ == $37 and voice i+1 exists: FREQ[i+1] = $41  (start next echo)
    ///       FREQ[i]--
    ///     If all FREQ == 0: terminate.
    ///
    /// ── Voice amplitudes (fixed) ───────────────────────────────────────────────
    ///   Voice 0: TEMPA = $80  (loudest)
    ///   Voice 1: TEMPA = $40
    ///   Voice 2: TEMPA = $20
    ///   Voice 3: TEMPA = $10  (quietest)
    ///
    /// ── Echo cascade ──────────────────────────────────────────────────────────
    ///   Voice 0 starts at $40; when it reaches $37, voice 1 spawns at $41.
    ///   When voice 1 reaches $37, voice 2 spawns at $41. And so on.
    ///   Four cascading echoes produce the characteristic multi-voice wail.
    ///
    /// ── Timing ────────────────────────────────────────────────────────────────
    ///   ≈ 193 CPU cycles per outer step (4 voices × ~42 cycles + setup/output overhead)
    ///   Frequency decay every 256 outer steps (TEMPB byte wrap)
    /// </summary>
    public sealed class ScreamGenerator : ISoundGenerator
    {
        private const int Voices = 4;   // ECHOS EQU 4

        // ── STABLE: 4 × (FREQ, TIMER) pairs ──────────────────────────────────
        private byte[] _freq  = new byte[Voices];
        private byte[] _timer = new byte[Voices];

        // ── Outer-loop counter ────────────────────────────────────────────────
        // Triggers freq decay when it wraps 255 → 0 (after 256 outer steps).
        private byte _tempb;

        // ── Output / state ────────────────────────────────────────────────────
        private byte   _sound;
        private bool   _active;
        private double _cycleAccum;

        // ~193 CPU cycles per SCREM2 outer step:
        // 4 voices × (LDAA_TIMER + ADDA_FREQ + STAA_TIMER + BPL + [ADDB] + LSR + INX×2 + CPX + BNE)
        // + SCREM2 init (LDX + LDAA + STAA + CLRB) + STAB + INC + BNE = ~193
        private const double CyclesPerStep = 193.0;

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            // SCREM1: zero all STABLE entries
            for (int i = 0; i < Voices; i++) { _freq[i] = 0; _timer[i] = 0; }
            // First echo starts at $40 (LDAA #$40; STAA STABLE+FREQ)
            _freq[0]    = 0x40;
            _tempb      = 0;
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
                    StepScream();
                }
            }
        }

        // ── SCREM2/SCREM3: one outer step — process all voices, output SOUND ─
        private void StepScream()
        {
            byte tempa = 0x80;   // LDAA #$80; STAA TEMPA
            byte b     = 0;      // CLRB

            // SCREM3: iterate all 4 voices
            for (int i = 0; i < Voices; i++)
            {
                _timer[i] = (byte)(_timer[i] + _freq[i]);  // ADDA FREQ,X; STAA TIMER,X
                if ((_timer[i] & 0x80) != 0)               // BPL SCREM4 fails (N=1)
                    b = unchecked((byte)(b + tempa));       // ADDB TEMPA
                tempa >>= 1;                                // LSR TEMPA
            }

            _sound = b;   // STAB SOUND

            // INC TEMPB; BNE SCREM2 — trigger freq decay when TEMPB wraps to 0
            _tempb++;
            if (_tempb == 0)
                ApplyFreqDecay();
        }

        // ── SCREM5: frequency decay + echo cascade ────────────────────────────
        private void ApplyFreqDecay()
        {
            bool anyNonZero = false;

            for (int i = 0; i < Voices; i++)
            {
                if (_freq[i] == 0) continue;

                // CMPA #$37; BNE SCREM6: if this voice's freq hits $37, spawn next echo
                if (_freq[i] == 0x37 && i + 1 < Voices)
                    _freq[i + 1] = 0x41;   // LDAB #$41; STAB FREQ+2,X

                _freq[i]--;   // DEC FREQ,X
                anyNonZero = true;
            }

            // TSTB; BNE SCREM2: if no voice is active, terminate
            if (!anyNonZero)
            {
                _active = false;
                _sound  = 0x80;
            }
        }
    }
}
