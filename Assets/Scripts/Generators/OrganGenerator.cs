namespace WilliamsSynth
{
    /// <summary>
    /// Implements the ORGAN synthesis routine from VSNDRM1.SRC lines 517–639.
    ///
    /// Handles commands $1A (ORGANT — Bach Toccata) and $1B (ORGANN — Phantom).
    /// Note data loaded from TuneTable.Toccata / TuneTable.Phantom.
    ///
    /// ── Algorithm (VSNDRM1.SRC ORGAN1 / ORGAN2) ────────────────────────────────
    ///
    ///   Each step:
    ///     TEMPB++ (8-bit wrap)
    ///     B = TEMPB & OSCIL
    ///     SOUND = PopCount(B) × 16    (7-shift add + ABA + 4×ASLA)
    ///     DEX; if X==0 → advance note (ORGAN2)
    ///     else RDELAY(DELAY) → loop (ORGAN1)
    ///
    ///   OSCIL (= VoiceMask) selects which counter bits form the waveform:
    ///     $1F (bits 0–4): highest pitch register (octave 4)
    ///     $3E (bits 1–5): next register          (octave 3)
    ///     $7C (bits 2–6): mid register            (octave 2)
    ///     $F8 (bits 3–7): lowest register         (octave 1)
    ///   The popcount of the masked value produces a multi-voice staircase waveform
    ///   whose frequency and timbre depend on OSCIL bit selection.
    ///
    ///   DELAY (= Period) comes from NOTTAB and is the inner per-step delay count.
    ///   Larger DELAY → slower step rate → lower pitch.
    ///
    ///   Duration (DUR) counts the total number of ORGAN1 steps for the current note.
    ///   When DUR expires ORGAN2 loads the next ORGTAB entry (or terminates).
    ///
    /// ── Timing ──────────────────────────────────────────────────────────────────
    ///   cycles_per_step = OrganCyclesBase + DELAY
    ///     OrganCyclesBase ≈ 72.0 (ORGAN1 body ≈58 cy + inner-loop overhead ≈14 cy).
    ///     Calibrated in P10 against MAME/hardware recordings.
    ///
    /// ── ORGTAB entry layout (confirmed from VSNDRM1.SRC) ───────────────────────
    ///   [0] VoiceMask — OSCIL byte
    ///   [1] Period    — DELAY byte (= NOTTAB value, e.g. $3F=A, $1D=D, $04=high-G)
    ///   [2] Duration  — step count, high byte
    ///   [3] Duration  — step count, low byte
    /// </summary>
    public sealed class OrganGenerator : ISoundGenerator
    {
        // ── Active tune ───────────────────────────────────────────────────────
        private TuneTable.TuneNote[] _tune;
        private int                  _noteIndex;

        // ── Per-note state ────────────────────────────────────────────────────
        private byte _oscil;       // current OSCIL (voice bitmask)
        private int  _delay;       // current DELAY (= note.Period, NOTTAB value)
        private int  _durCounter;  // remaining step count for this note

        // ── Running oscillator state ──────────────────────────────────────────
        private byte _tempb;       // TEMPB: incremented each step, wraps at 256
        private byte _sound;       // current SOUND / DAC output value

        // ── Timing ────────────────────────────────────────────────────────────
        private double _cyclesPerStep; // OrganCyclesBase + _delay
        private double _cycleAccum;

        private bool _active;

        // ORGAN1 body (≈58 cy) + per-step loop-overhead (≈14 cy) = 72.
        // This constant is a calibration target for P10; adjust against hardware.
        private const double OrganCyclesBase = 72.0;

        // ─────────────────────────────────────────────────────────────────────
        public bool IsActive => _active;

        // ── Trigger ───────────────────────────────────────────────────────────
        public void Trigger(byte commandId)
        {
            _tune       = (commandId == SoundCommand.ORGANT)
                          ? TuneTable.Toccata
                          : TuneTable.Phantom;
            _tempb      = 0;
            _cycleAccum = 0.0;
            _noteIndex  = -1;   // AdvanceNote() will move to index 0
            _active     = true;
            AdvanceNote();
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
                while (_cycleAccum >= _cyclesPerStep && _active)
                {
                    _cycleAccum -= _cyclesPerStep;
                    StepOrgan();
                }
            }
        }

        // ── StepOrgan: one ORGAN1 inner-loop iteration ────────────────────────
        // Assembly ORGAN1:
        //   INCB; STAB TEMPB; ANDB OSCIL; 7×(LSRB;ADCA#0); ABA; 4×ASLA; STAA SOUND
        //   DEX; BEQ ORGAN2; JMP RDELAY
        private void StepOrgan()
        {
            // INCB / STAB TEMPB: increment counter (8-bit wrap)
            _tempb = unchecked((byte)(_tempb + 1));

            // ANDB OSCIL then popcount → SOUND (popcount × 16)
            byte b = (byte)(_tempb & _oscil);
            _sound = (byte)(PopCount8(b) << 4);

            // DEX equivalent: count down duration, advance note when expired
            if (--_durCounter <= 0)
                AdvanceNote();
        }

        // ── AdvanceNote (ORGAN2): load next ORGTAB entry ──────────────────────
        private void AdvanceNote()
        {
            _noteIndex++;
            if (_noteIndex >= _tune.Length)
            {
                // TUNEND / end of tune — terminate
                _active = false;
                _sound  = 0x80;
                return;
            }

            ref readonly TuneTable.TuneNote note = ref _tune[_noteIndex];
            _oscil        = note.VoiceMask;
            _delay        = note.Period;
            _durCounter   = note.Duration;
            _cyclesPerStep = OrganCyclesBase + _delay;
        }

        // ── PopCount8: count set bits in a byte (authentic 7-shift+ABA sequence) ──
        // Equivalent to the 6800 assembly: 7×(LSRB; ADCA #0); ABA (carry-chain popcount).
        // Returns the number of 1-bits in b (range 0–8).
        private static int PopCount8(byte b)
        {
            int n = b;
            n -= (n >> 1) & 0x55;
            n  = (n & 0x33) + ((n >> 2) & 0x33);
            n  = (n + (n >> 4)) & 0x0F;
            return n;
        }
    }
}
