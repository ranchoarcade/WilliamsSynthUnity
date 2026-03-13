namespace WilliamsSynth
{
    /// <summary>
    /// NOTTAB — 12-byte chromatic note period table, used by the ORGAN generator.
    /// Source: VSNDRM1.SRC, NOTTAB label.
    ///
    /// Each byte is a period value (lower = higher pitch) for one semitone step.
    /// The ORGAN routine uses these as timer periods for its four oscillator voices.
    ///
    ///   Index 0  = $47 (71)  — lowest note in the table
    ///   Index 1  = $3F (63)  — A
    ///   Index 2  = $37 (55)
    ///   Index 3  = $30 (48)
    ///   Index 4  = $29 (41)
    ///   Index 5  = $23 (35)  — C# (CS)
    ///   Index 6  = $1D (29)  — D
    ///   Index 7  = $17 (23)
    ///   Index 8  = $12 (18)  — E
    ///   Index 9  = $0D (13)  — F
    ///   Index 10 = $08 ( 8)  — G (approximately)
    ///   Index 11 = $04 ( 4)  — highest note, approx two octaves above index 0
    ///
    /// The ORGTAB tune data references these by index via the low byte of the
    /// FDB voice/period word (e.g. $7F1D selects period $1D = index 6 = D note).
    /// </summary>
    public static class NoteTable
    {
        // Source: VSNDRM1.SRC
        //   NOTTAB  FCB $47,$3F,$37,$30,$29,$23
        //           FCB $1D,$17,$12,$0D,$08,$04
        public static readonly byte[] NOTTAB =
        {
            0x47, 0x3F, 0x37, 0x30, 0x29, 0x23,
            0x1D, 0x17, 0x12, 0x0D, 0x08, 0x04
        };
    }
}
