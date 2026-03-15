namespace WilliamsSynth
{
    /// <summary>
    /// ORGTAB — organ tune data for ORGANT ($1A, Bach Toccata) and ORGANN ($1B, Phantom).
    /// Source: VSNDRM1.SRC, ORGTAB label.
    ///
    /// ROM layout:
    ///   [length_byte] N × FDB_word1, FDB_word2 ... [next_tune_length] ... [0x00 = LAST TUNE]
    ///
    /// Each note entry is 4 bytes:
    ///   Word 1 (2 bytes): voice bitmask (high byte) + period index from NOTTAB (low byte)
    ///   Word 2 (2 bytes): duration in ORGAN timer ticks
    ///
    /// Duration expressions resolved (B5 unblocked, 2026-03-10):
    ///   TACC  = 4   (Toccata tempo constant — VSNDRM1.SRC line 20)
    ///   PHANC = 3   (Phantom tempo constant — VSNDRM1.SRC line 21)
    ///   Note time constants (after >>1): TA=18390, TBF=19483, TCS=23170, TD=24548,
    ///     TE=27554, TF=29192, TFS=30928, TG=32767
    ///   Assembler evaluates left-to-right with integer (truncating) division.
    ///   Example: TA/TACC/8*1 = (((18390/4)/8)*1) = 574
    ///
    /// The voice/period word (Word 1) encodes:
    ///   High byte = VoiceMask (OSCIL) — bitmask of oscillator voices active for this note
    ///   Low byte  = Period — raw NOTTAB value (e.g. $1D=D, $3F=A, $04=high-G)
    /// </summary>
    public static class TuneTable
    {
        // ── Tune entry structure ──────────────────────────────────────────────────
        // Each TuneNote encodes one note event. The OrganGenerator reads these
        // sequentially, advancing to the next when the duration timer expires.
        public readonly struct TuneNote
        {
            /// <summary>
            /// High byte of the ORGTAB FDB word 1.
            /// Bitmask selecting which of the 8 TEMPB counter bits are ANDed together
            /// to generate the audio waveform via popcount. Controls chord timbre.
            /// ($1F=bits 0–4 octave 4, $3E=bits 1–5 octave 3,
            ///  $7C=bits 2–6 octave 2, $F8=bits 3–7 octave 1, etc.)
            /// </summary>
            public readonly byte VoiceMask;

            /// <summary>
            /// Low byte of the ORGTAB FDB word 1.
            /// Raw period / RDELAY value from NoteTable.NOTTAB
            /// (e.g. $1D = D note, $3F = A note, $04 = highest note).
            /// Added to OrganCyclesBase to form _cyclesPerStep.
            /// </summary>
            public readonly byte Period;

            /// <summary>
            /// Duration in ORGAN step iterations (= inner-loop count for this note).
            /// Derived from assembler time-constant expressions divided by tempo constant.
            /// </summary>
            public readonly ushort Duration;

            public TuneNote(byte voiceMask, byte period, ushort duration)
            {
                VoiceMask = voiceMask;
                Period    = period;
                Duration  = duration;
            }
        }

        // ── ORGANN ($1B) — Phantom ────────────────────────────────────────────────
        // ROM: FCB 3*4  (= 12 bytes for 3 notes)
        // Source comments: "PHANTOM"
        // Duration expressions (PHANC=3):
        //   TD/PHANC/2*1     = 24548/3/2*1 = 8182/2  = 4091
        //   TCS/PHANC/2*1    = 23170/3/2*1 = 7723/2  = 3861
        //   (TFS/PHANC/1*1)*2 = (30928/3/1*1)*2 = 10309*2 = 20618

        public static readonly TuneNote[] Phantom =
        {
            new TuneNote(0x7F, 0x1D, 4091),  // FDB $7F1D, TD/PHANC/2*1     — D    quarter note
            new TuneNote(0x7F, 0x23, 3861),  // FDB $7F23, TCS/PHANC/2*1    — CS   quarter note
            new TuneNote(0xFE, 0x08, 20618), // FDB $FE08, (TFS/PHANC/1*1)*2 — FS  whole note
        };

        // ── ORGANT ($1A) — Toccata (Bach) ─────────────────────────────────────────
        // ROM: FCB 34*4  (= 136 bytes for 34 notes)
        // Source comments: "*TACCATA" (sic), FCB 0 = LAST TUNE at end
        // Duration expressions resolved with TACC=4 (left-to-right truncating integer div):
        //   TA/TACC/8*1  = 18390/4/8*1 = 574      TG/TACC/8*1  = 32767/4/8*1 = 1023
        //   TA/TACC/2*5  = 18390/4/2*5 = 11490     TE/TACC/2*1  = 27554/4/2*1 = 3444
        //   TF/TACC/2*1  = 29192/4/2*1 = 3649      TCS/TACC/2*1 = 23170/4/2*1 = 2896
        //   TD/TACC/4*7  = 24548/4/4*7 = 10738     TD/TACC/1*1  = 24548/4/1*1 = 6137
        //   TCS/TACC/4*1 = 23170/4/4*1 = 1448      TE/TACC/4*1  = 27554/4/4*1 = 1722
        //   TG/TACC/4*1  = 32767/4/4*1 = 2047      TBF/TACC/4*1 = 19483/4/4*1 = 1217
        //   TCS/TACC/1*1 = 23170/4/1*1 = 5792      TBF/TACC/1*1 = 19483/4/1*1 = 4870
        //   TA/TACC/2*1  = 18390/4/2*1 = 2298      TG/TACC/2*1  = 32767/4/2*1 = 4095
        //   (TD/TACC/1*1)*2 = 12274                (TD/TACC/1*2)*2 = 24548

        public static readonly TuneNote[] Toccata =
        {
            new TuneNote(0x3E, 0x3F, 574),   // FDB $3E3F, TA/TACC/8*1     — A3   1/16 note
            new TuneNote(0x7C, 0x04, 1023),  // FDB $7C04, TG/TACC/8*1     — G2   1/16 note
            new TuneNote(0x3E, 0x3F, 11490), // FDB $3E3F, TA/TACC/2*5     — A3   5/4  note
            new TuneNote(0x7C, 0x12, 3444),  // FDB $7C12, TE/TACC/2*1     — E2   1/4  note
            new TuneNote(0x7C, 0x0D, 3649),  // FDB $7C0D, TF/TACC/2*1     — F2   1/4  note
            new TuneNote(0x7C, 0x23, 2896),  // FDB $7C23, TCS/TACC/2*1    — CS2  1/4  note
            new TuneNote(0x7C, 0x1D, 10738), // FDB $7C1D, TD/TACC/4*7     — D2   7/8  note
            new TuneNote(0x7C, 0x3F, 574),   // FDB $7C3F, TA/TACC/8*1     — A2   1/16 note
            new TuneNote(0xF8, 0x04, 1023),  // FDB $F804, TG/TACC/8*1     — G1   1/16 note
            new TuneNote(0x7C, 0x3F, 11490), // FDB $7C3F, TA/TACC/2*5     — A2   5/4  note
            new TuneNote(0xF8, 0x12, 3444),  // FDB $F812, TE/TACC/2*1     — E1   1/4  note
            new TuneNote(0xF8, 0x0D, 3649),  // FDB $F80D, TF/TACC/2*1     — F1   1/4  note
            new TuneNote(0xF8, 0x23, 2896),  // FDB $F823, TCS/TACC/2*1    — CS1  1/4  note
            new TuneNote(0xF8, 0x1D, 12274), // FDB $F81D, (TD/TACC/1*1)*2 — D1   1    note
            new TuneNote(0xF8, 0x23, 1448),  // FDB $F823, TCS/TACC/4*1    — CS1  1/8  note
            new TuneNote(0xF8, 0x12, 1722),  // FDB $F812, TE/TACC/4*1     — E1   1/8  note
            new TuneNote(0xF8, 0x04, 2047),  // FDB $F804, TG/TACC/4*1     — G1   1/8  note
            new TuneNote(0x7C, 0x37, 1217),  // FDB $7C37, TBF/TACC/4*1    — BF2  1/8  note
            new TuneNote(0x7C, 0x23, 1448),  // FDB $7C23, TCS/TACC/4*1    — CS2  1/8  note
            new TuneNote(0x7C, 0x12, 1722),  // FDB $7C12, TE/TACC/4*1     — E2   1/8  note
            new TuneNote(0x3E, 0x04, 2047),  // FDB $3E04, TG/TACC/4*1     — G3   1/8  note
            new TuneNote(0x3E, 0x37, 1217),  // FDB $3E37, TBF/TACC/4*1    — BF3  1/8  note
            new TuneNote(0x3E, 0x23, 1448),  // FDB $3E23, TCS/TACC/4*1    — CS3  1/8  note
            new TuneNote(0x1F, 0x12, 1722),  // FDB $1F12, TE/TACC/4*1     — E4   1/8  note
            new TuneNote(0x1F, 0x04, 2047),  // FDB $1F04, TG/TACC/4*1     — G4   1/8  note
            new TuneNote(0x1F, 0x37, 1217),  // FDB $1F37, TBF/TACC/4*1    — BF4  1/8  note
            new TuneNote(0x1F, 0x23, 5792),  // FDB $1F23, TCS/TACC/1*1    — CS4  1/2  note
            new TuneNote(0xFE, 0x1D, 6137),  // FDB $FE1D, TD/TACC/1*1     — D1   1/2  note
            new TuneNote(0x7F, 0x37, 4870),  // FDB $7F37, TBF/TACC/1*1    — BF2  1/2  note
            new TuneNote(0x7F, 0x3F, 2298),  // FDB $7F3F, TA/TACC/2*1     — A2   1/4  note
            new TuneNote(0xFE, 0x04, 4095),  // FDB $FE04, TG/TACC/2*1     — G1   1/4  note
            new TuneNote(0xFE, 0x0D, 3649),  // FDB $FE0D, TF/TACC/2*1     — F1   1/4  note
            new TuneNote(0xFE, 0x23, 2896),  // FDB $FE23, TCS/TACC/2*1    — CS1  1/4  note
            new TuneNote(0xFE, 0x1D, 24548), // FDB $FE1D, (TD/TACC/1*2)*2 — D1   2    note
        };
    }
}
