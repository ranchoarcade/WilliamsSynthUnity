namespace WilliamsSynth
{
    /// <summary>
    /// Waveform lookup tables extracted verbatim from VSNDRM1.SRC (GWVTAB block + RADSND).
    ///
    /// The ROM stores each wavetable as: [length_byte, data...].
    /// The length byte is stripped here — use array.Length instead.
    ///
    /// Used by GWaveGenerator (GWAVE routine) and RadioGenerator (RADIO routine).
    /// </summary>
    public static class WaveformTables
    {
        // ── GWVTAB waveform sub-tables ────────────────────────────────────────────
        // Source: VSNDRM1.SRC, GWVTAB label. Byte 0 of each ROM entry is the count;
        // stripped here. All values are unsigned bytes fed to DAC1408.ToFloat().

        /// <summary>GS2 — 8-byte simple sine. ROM: FCB 8, 127, 217, 255, ...</summary>
        public static readonly byte[] GS2 =
        {
            127, 217, 255, 217, 127, 36, 0, 36
        };

        /// <summary>GSSQ2 — 8-byte simple square.</summary>
        public static readonly byte[] GSSQ2 =
        {
            0, 64, 128, 0, 255, 0, 128, 64
        };

        /// <summary>GS1 — 16-byte sine wave.</summary>
        public static readonly byte[] GS1 =
        {
            127, 176, 217, 245, 255, 245, 217, 176,
            127,  78,  36,   9,   0,   9,  36,  78
        };

        /// <summary>GS12 — 16-byte custom harmonic.</summary>
        public static readonly byte[] GS12 =
        {
            127, 197, 236, 231, 191, 141, 109, 106,
            127, 148, 146, 113,  64,  23,  18,  57
        };

        /// <summary>GSQ22 — 16-byte square wave variant.</summary>
        public static readonly byte[] GSQ22 =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        /// <summary>
        /// GS72 — 72-byte full-length sine wave. Primary GWAVE waveform.
        /// This is the most commonly used table; it produces the characteristic
        /// smooth sine tone heard in heartbeat, distorto, and sweep sounds.
        /// </summary>
        public static readonly byte[] GS72 =
        {
            138, 149, 160, 171, 181, 191, 200, 209,
            218, 225, 232, 238, 243, 247, 251, 253, 254, 255,
            254, 253, 251, 247, 243, 238, 232, 225, 218,
            209, 200, 191, 181, 171, 160, 149, 138, 127,
            117, 106,  95,  84,  74,  64,  55,  46,  37,  30,  23,  17,  12,
              8,   4,   2,   1,   0,
              1,   2,   4,   8,  12,  17,  23,  30,  37,  46,  55,  64,  74,  84,
             95, 106, 117, 127
        };

        /// <summary>GS17 — 16-byte custom waveform. ROM label: GS1.7</summary>
        public static readonly byte[] GS17 =
        {
             89, 123, 152, 172, 179, 172, 152, 123,
             89,  55,  25,   6,   0,   6,  25,  55
        };

        // ── RADSND ────────────────────────────────────────────────────────────────

        /// <summary>
        /// RADSND — 16-byte waveform for the RADIO oscillator (RADIO routine, cmd $17).
        /// Source: VSNDRM1.SRC, RADSND label.
        /// </summary>
        public static readonly byte[] RADSND =
        {
            0x8C, 0x5B, 0xB6, 0x40, 0xBF, 0x49, 0xA4, 0x73,
            0x73, 0xA4, 0x49, 0xBF, 0x40, 0xB6, 0x5B, 0x8C
        };
    }
}
