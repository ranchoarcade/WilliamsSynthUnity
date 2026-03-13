namespace WilliamsSynth
{
    /// <summary>
    /// VARI (PWM square wave) preset parameter tables extracted verbatim from VSNDRM1.SRC
    /// (VVECT block). Commands $1C–$1F, indexed by (commandId - 0x1C).
    ///
    /// Each preset is 9 bytes. Field layout confirmed from LOCRAM equates in VSNDRM1.SRC:
    ///
    ///   [0] LOPER   — initial LO half-period (reloaded at each VAR0 restart)
    ///   [1] HIPER   — initial HI half-period (constant across LOMOD restarts)
    ///   [2] LODT    — signed per-sweep delta added to LOCNT  (8-bit wrap)
    ///   [3] HIDT    — signed per-sweep delta added to HICNT  (8-bit wrap)
    ///   [4] HIEN    — stop value for HICNT (8-bit unsigned comparison after sweep update)
    ///   [5] SWPDT_H — sweep timer high byte  ─┐ 16-bit big-endian; inner-loop iterations
    ///   [6] SWPDT_L — sweep timer low byte   ─┘ before each sweep update fires
    ///   [7] LOMOD   — signed LO modulation step (0 = terminate on HIEN; non-zero = restart)
    ///   [8] VAMP    — output amplitude / initial DAC byte
    ///
    /// Indexed by: VariPresets[commandId - SoundCommand.SAW]   (i.e. commandId - 0x1C)
    /// </summary>
    public static class VariParameterTables
    {
        // Source: VSNDRM1.SRC, VVECT label.

        public static readonly byte[][] VariPresets = new byte[4][]
        {
            // [0] $1C SAW — Sawtooth sweep
            // FCB $40,$01,$00,$10,$E1,$00,$80,$FF,$FF
            new byte[] { 0x40, 0x01, 0x00, 0x10, 0xE1, 0x00, 0x80, 0xFF, 0xFF },

            // [1] $1D FOSHIT — "Foshit" (fast downward sweep)
            // FCB $28,$01,$00,$08,$81,$02,$00,$FF,$FF
            new byte[] { 0x28, 0x01, 0x00, 0x08, 0x81, 0x02, 0x00, 0xFF, 0xFF },

            // [2] $1E QUASAR — Quasar (modulated sweep)
            // FCB $28,$81,$00,$FC,$01,$02,$00,$FC,$FF
            new byte[] { 0x28, 0x81, 0x00, 0xFC, 0x01, 0x02, 0x00, 0xFC, 0xFF },

            // [3] $1F CABSHK — Cabin shake (low rumble)
            // FCB $FF,$01,$00,$18,$41,$04,$80,$00,$FF
            new byte[] { 0xFF, 0x01, 0x00, 0x18, 0x41, 0x04, 0x80, 0x00, 0xFF },
        };
    }
}
