namespace WilliamsSynth
{
    /// <summary>
    /// Frequency pattern table extracted verbatim from VSNDRM1.SRC (GFRTAB block).
    ///
    /// GFRTAB is a flat byte array. The GWAVE generator indexes into it using
    /// two fields from SoundParameterTables: FreqOffset (byte offset from start)
    /// and FreqLen (number of bytes to read). The generator walks FreqLen bytes
    /// starting at FreqOffset, stepping the period register by FreqInc each step.
    ///
    /// Named offset constants below reproduce the assembler expressions
    /// "LABEL - GFRTAB" used in SVTAB. Use them as: GFRTAB[FreqOffset].
    /// </summary>
    public static class FrequencyTables
    {
        /// <summary>
        /// GFRTAB — flat frequency pattern byte array (163 bytes total).
        /// Source: VSNDRM1.SRC, GFRTAB label.
        /// </summary>
        public static readonly byte[] GFRTAB =
        {
            // BONSND (offset 0, 13 bytes) — bonus/explosion sweep
            0xA0, 0x98, 0x90, 0x88, 0x80, 0x78, 0x70, 0x68,
            0x60, 0x58, 0x50, 0x44, 0x40,

            // HBTSND (offset 13, 14 bytes) — hundred-point sound
            // Referenced by BG1/BG2 background sounds (commands $0E/$0F).
            0x01, 0x01, 0x02, 0x02, 0x04, 0x04, 0x08, 0x08,
            0x10, 0x10, 0x30, 0x60, 0xC0, 0xE0,

            // SPNSND (offset 27, 13 bytes) — spinner sound
            0x01, 0x01, 0x02, 0x02, 0x03, 0x04, 0x05, 0x06,
            0x07, 0x08, 0x09, 0x0A, 0x0C,

            // TRBPAT (offset 40, 9 bytes) — turbine startup oscillation
            0x80, 0x7C, 0x78, 0x74, 0x70, 0x74, 0x78, 0x7C, 0x80,

            // HBDSND (offset 49, 22 bytes) — heartbeat distorto sweep
            0x01, 0x01, 0x02, 0x02, 0x04, 0x04, 0x08, 0x08,
            0x10, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50,
            0x60, 0x70, 0x80, 0xA0, 0xB0, 0xC0,

            // SWPAT = BBSND (offset 71, 20 bytes) — Big Ben / sweep pattern.
            // SWPAT is defined as EQU * (same address as BBSND), so both offset
            // constants point here. DP1V uses SWPAT; BBSV uses BBSND.
            0x08, 0x40, 0x08, 0x40, 0x08, 0x40, 0x08, 0x40, 0x08, 0x40,
            0x08, 0x40, 0x08, 0x40, 0x08, 0x40, 0x08, 0x40, 0x08, 0x40,

            // HBESND (offset 91, 14 bytes) — heartbeat echo
            // NOTE: HBEV SVTAB entry specifies FreqLen=15, which reads 1 byte
            // past this table into SPNR ($40). This is authentic ROM behaviour.
            0x01, 0x02, 0x04, 0x08, 0x09, 0x0A, 0x0B, 0x0C,
            0x0E, 0x0F, 0x10, 0x12, 0x14, 0x16,

            // SPNR (offset 105, 1 byte) — spinner "drip" single frequency
            0x40,

            // COOLDN (offset 106, 3 bytes) — cool downer descending sweep
            0x10, 0x08, 0x01,

            // STDSND (offset 109, 39 bytes) — start distorto sweep (up then down)
            0x01, 0x01, 0x01, 0x01, 0x02, 0x02, 0x03, 0x03, 0x04, 0x04,
            0x05, 0x06, 0x08, 0x0A, 0x0C, 0x10, 0x14, 0x18, 0x20, 0x30,
            0x40, 0x50, 0x40, 0x30, 0x20, 0x10, 0x0C, 0x0A, 0x08, 0x07,
            0x06, 0x05, 0x04, 0x03, 0x02, 0x02, 0x01, 0x01, 0x01,

            // ED10FP (offset 148, 6 bytes) — Ed's sound 10 frequency pattern
            0x07, 0x08, 0x09, 0x0A, 0x0C, 0x08,

            // ED13FP (offset 154, 9 bytes) — Ed's sound 13 frequency pattern
            // "MATCH THE PROMS" comment in source — trailing zeroes are intentional.
            0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x00, 0x00, 0x00,
        };

        // ── Named byte offsets into GFRTAB ────────────────────────────────────────
        // These reproduce the assembler expressions "LABEL - GFRTAB" from SVTAB.

        public const int BONSND = 0;
        public const int HBTSND = 13;
        public const int SPNSND = 27;
        public const int TRBPAT = 40;
        public const int HBDSND = 49;
        /// <summary>SWPAT and BBSND share the same address (EQU *). Offset = 71.</summary>
        public const int SWPAT  = 71;
        public const int BBSND  = 71;
        public const int HBESND = 91;
        public const int SPNR   = 105;
        public const int COOLDN = 106;
        public const int STDSND = 109;
        public const int ED10FP = 148;
        public const int ED13FP = 154;
    }
}
