namespace WilliamsSynth
{
    /// <summary>
    /// GWAVE sound parameter table (SVTAB) extracted verbatim from VSNDRM1.SRC.
    ///
    /// Each entry is 7 bytes corresponding to one GWAVE sound command.
    /// The byte fields (from SVTAB format, VSNDRM1.SRC):
    ///
    ///   [0] EchoCycle  — ECHO: upper nibble = echo count, CYCLE: lower nibble = cycle length
    ///   [1] DecayWave  — DECAY: upper nibble = amplitude decay step, WAVE: lower nibble = waveform index
    ///   [2] PreDecay   — initial amplitude before decay begins (0 = no pre-decay)
    ///   [3] FreqInc    — frequency increment per step (unsigned; treat as signed sbyte for negative sweep)
    ///   [4] DeltaCnt   — number of frequency steps before stopping sweep
    ///   [5] FreqLen    — number of bytes in the GFRTAB frequency pattern for this sound
    ///   [6] FreqOffset — byte offset into FrequencyTables.GFRTAB for this sound's pattern
    ///
    /// Indexed by: GWaveParams[commandId] where commandId is the raw SoundCommand byte.
    /// Entries for non-GWAVE commands are all zeros (unused).
    /// </summary>
    public static class SoundParameterTables
    {
        // ── SVTAB — GWave sound parameter entries ─────────────────────────────────
        // Source: VSNDRM1.SRC, SVTAB label.
        // Comments show: ROM label, command byte, assembly source line.
        //
        // Field names: EchoCycle | DecayWave | PreDecay | FreqInc | DeltaCnt | FreqLen | FreqOffset

        public static readonly byte[][] GWaveParams = new byte[32][]
        {
            // $00 — Silence (not a GWAVE command)
            new byte[] { 0, 0, 0, 0, 0, 0, 0 },

            // $01 HBDV — Heartbeat distorto
            // FCB $81,$24,0,0,0,22,HBDSND-GFRTAB
            new byte[] { 0x81, 0x24, 0x00, 0x00, 0x00, 22, FrequencyTables.HBDSND },

            // $02 STDV — Start distorto
            // FCB $12,$05,$1A,$FF,0,39,STDSND-GFRTAB
            new byte[] { 0x12, 0x05, 0x1A, 0xFF, 0x00, 39, FrequencyTables.STDSND },

            // $03 DP1V — DP1 sweep
            // FCB $11,$05,$11,1,15,1,SWPAT-GFRTAB
            new byte[] { 0x11, 0x05, 0x11, 0x01, 0x0F, 0x01, FrequencyTables.SWPAT },

            // $04 XBV — Spinner (XB variant)
            // FCB $11,$31,0,1,0,13,SPNSND-GFRTAB
            new byte[] { 0x11, 0x31, 0x00, 0x01, 0x00, 13, FrequencyTables.SPNSND },

            // $05 BBSV — Big Ben
            // FCB $F4,$12,0,0,0,20,BBSND-GFRTAB
            new byte[] { 0xF4, 0x12, 0x00, 0x00, 0x00, 20, FrequencyTables.BBSND },

            // $06 HBEV — Heartbeat echo
            // FCB $41,$45,0,0,0,15,HBESND-GFRTAB
            // NOTE: FreqLen=15 but HBESND has 14 bytes — reads 1 byte into SPNR ($40). Authentic.
            new byte[] { 0x41, 0x45, 0x00, 0x00, 0x00, 15, FrequencyTables.HBESND },

            // $07 PROTV — Probe/protector
            // FCB $21,$35,$11,$FF,0,13,SPNSND-GFRTAB
            new byte[] { 0x21, 0x35, 0x11, 0xFF, 0x00, 13, FrequencyTables.SPNSND },

            // $08 SPNRV — Spinner (drip)
            // FCB $15,$00,0,$FD,0,1,SPNR-GFRTAB
            new byte[] { 0x15, 0x00, 0x00, 0xFD, 0x00, 0x01, FrequencyTables.SPNR },

            // $09 CLDWNV — Cool downer
            // FCB $31,$11,0,1,0,3,COOLDN-GFRTAB
            new byte[] { 0x31, 0x11, 0x00, 0x01, 0x00, 0x03, FrequencyTables.COOLDN },

            // $0A SV3 — SV3 tone (uses single Big Ben byte)
            // FCB $01,$15,1,1,1,1,BBSND-GFRTAB
            new byte[] { 0x01, 0x15, 0x01, 0x01, 0x01, 0x01, FrequencyTables.BBSND },

            // $0B ED10 — Ed's sound 10
            // FCB $F6,$53,3,0,2,6,ED10FP-GFRTAB
            new byte[] { 0xF6, 0x53, 0x03, 0x00, 0x02, 0x06, FrequencyTables.ED10FP },

            // $0C ED12 — Ed's sound 12
            // FCB $6A,$10,2,0,2,6,ED13FP-GFRTAB
            new byte[] { 0x6A, 0x10, 0x02, 0x00, 0x02, 0x06, FrequencyTables.ED13FP },

            // $0D SP1 — Ed's sound 17 (ROM label: ED17, dispatched via SP1 command)
            // FCB $1F,$12,0,$FF,$10,4,SPNR-GFRTAB
            // NOTE: FreqLen=4 reads SPNR + 3 bytes of COOLDN — intentional overlap. Authentic.
            new byte[] { 0x1F, 0x12, 0x00, 0xFF, 0x10, 0x04, FrequencyTables.SPNR },

            // $0E BG1 — Background sound 1 (GWAVE params not confirmed; placeholder)
            new byte[] { 0, 0, 0, 0, 0, 0, 0 },

            // $0F BG2INC — Background sound 2 increment (placeholder)
            new byte[] { 0, 0, 0, 0, 0, 0, 0 },

            // $10 LITE — Noise burst (handled by NoiseGenerator, not GWAVE)
            new byte[] { 0, 0, 0, 0, 0, 0, 0 },

            // $11 BONV — Bonus/explosion tone
            // FCB $31,$11,0,$FF,0,13,BONSND-GFRTAB
            new byte[] { 0x31, 0x11, 0x00, 0xFF, 0x00, 13, FrequencyTables.BONSND },

            // $12 BGEND — Background end (not a GWAVE command)
            new byte[] { 0, 0, 0, 0, 0, 0, 0 },

            // $13 TURBO — Turbine startup
            // FCB $12,$06,$00,$FF,1,9,TRBPAT-GFRTAB
            new byte[] { 0x12, 0x06, 0x00, 0xFF, 0x01, 0x09, FrequencyTables.TRBPAT },

            // $14–$1F — NOISE, FNOISE, SCREAM, RADIO, HYPER, ORGAN, VARI commands
            // These are handled by their own generators, not GWaveGenerator.
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $14 APPEAR
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $15 THRUST
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $16 CANNON
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $17 RADIO
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $18 HYPER
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $19 SCREAM
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1A ORGANT
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1B ORGANN
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1C SAW
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1D FOSHIT
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1E QUASAR
            new byte[] { 0, 0, 0, 0, 0, 0, 0 }, // $1F CABSHK
        };
    }
}
