namespace WilliamsSynth
{
    /// <summary>
    /// Raw 5-bit sound board commands sent from the main CPU to the sound board
    /// via the PIA at $CC02. Names match the original assembly ROM labels from
    /// VSNDRM1.SRC and DEFA7.SRC.
    ///
    /// These are the values written to SoundStep.CommandId and dispatched directly
    /// to SoundBoardEmulator. They do NOT carry priority, timing, or repeat logic —
    /// those belong to SoundSequence / SoundSequencer (Level 1).
    /// </summary>
    public static class SoundCommand
    {
        /// <summary>$00 — silence / stop current foreground sound.</summary>
        public const byte Silence = 0x00;

        // ── GWAVE commands ($01–$0C) ──────────────────────────────────────────
        // Routed to GWaveGenerator with parameters from SoundParameterTables.GWaveParams.

        /// <summary>$01 — HBDV: Heartbeat distorto.</summary>
        public const byte HBDV    = 0x01;

        /// <summary>$02 — STDV: Start distorto.</summary>
        public const byte STDV    = 0x02;

        /// <summary>$03 — DP1V: Dual-pitch variant / sweep pattern.</summary>
        public const byte DP1V    = 0x03;

        /// <summary>$04 — XBV: Spinner (GWAVE variant).</summary>
        public const byte XBV     = 0x04;

        /// <summary>$05 — BBSV: Big Ben bell tone.</summary>
        public const byte BBSV    = 0x05;

        /// <summary>$06 — HBEV: Heartbeat echo.</summary>
        public const byte HBEV    = 0x06;

        /// <summary>$07 — PROTV: Protect sound.</summary>
        public const byte PROTV   = 0x07;

        /// <summary>$08 — SPNRV: Spinner drip.</summary>
        public const byte SPNRV   = 0x08;

        /// <summary>$09 — CLDWNV: Cool down (falling frequency sweep).</summary>
        public const byte CLDWNV  = 0x09;

        /// <summary>$0A — SV3: Sound variant 3.</summary>
        public const byte SV3     = 0x0A;

        /// <summary>$0B — ED10: Ed's sound 10.</summary>
        public const byte ED10    = 0x0B;

        /// <summary>$0C — ED12: Ed's sound 12/17.</summary>
        public const byte ED12    = 0x0C;

        /// <summary>$0D — ED17: Ed's sound 17 (GWAVE variant).</summary>
        public const byte ED17    = 0x0D;

        // ── Special / persistent commands ($0E–$1C) ───────────────────────────

        /// <summary>$0E — SP1: Spinner sound #1 (VARI-based).</summary>
        public const byte SP1     = 0x0E;

        /// <summary>$0F — BG1: Background engine rumble (FNOISE loop, starts BG1FLG).</summary>
        public const byte BG1      = 0x0F;

        /// <summary>$10 — BG2INC: Background increment.</summary>
        public const byte BG2INC   = 0x10;

        /// <summary>$11 — LITE: Lightning — rising swept noise.</summary>
        public const byte LITE     = 0x11;

        /// <summary>$12 — BONV: Bonus #2 (GWAVE variant).</summary>
        public const byte BONV     = 0x12;

        /// <summary>$13 — BGEND: Background end (stops BG1FLG).</summary>
        public const byte BGEND    = 0x13;

        /// <summary>$14 — TURBO: Turbo (FNOISE with frequency decay).</summary>
        public const byte TURBO    = 0x14;

        /// <summary>$15 — APPEAR: Appear — falling noise burst.</summary>
        public const byte APPEAR   = 0x15;

        /// <summary>$16 — THRUST: Thrust loop (FNOISE, sets THFLG).</summary>
        public const byte THRUST   = 0x16;

        /// <summary>$17 — CANNON: Cannon shot (FNOISE with distortion, DSFLG set).</summary>
        public const byte CANNON   = 0x17;

        /// <summary>$18 — RADIO: Radio tuning effect (wavetable oscillator).</summary>
        public const byte RADIO    = 0x18;

        /// <summary>$19 — HYPER: Hyperspace sweep (square wave, COM SOUND).</summary>
        public const byte HYPER    = 0x19;

        /// <summary>$1A — SCREAM: Astronaut scream (4-voice polyphonic echo, STABLE table).</summary>
        public const byte SCREAM   = 0x1A;

        /// <summary>$1B — ORGANT: Organ Toccata tune (Bach Toccata &amp; Fugue in D minor, TACC tempo).</summary>
        public const byte ORGANT   = 0x1B;

        /// <summary>$1C — ORGANN: Organ Phantom note (Phantom of the Opera, PHANC tempo).</summary>
        public const byte ORGANN   = 0x1C;

        // ── VARI preset commands ($1C–$1F) ────────────────────────────────────
        // Routed to VariWaveGenerator with parameters from VariParameterTables.Presets.

        /// <summary>$1D — SAW: VARI preset 0 — sawtooth-approximating sweep.</summary>
        public const byte SAW      = 0x1D;

        /// <summary>$1E — FOSHIT: VARI preset 1 — harsh distorted tone.</summary>
        public const byte FOSHIT   = 0x1E;

        /// <summary>$1F — QUASAR: VARI preset 2 — rising quasar effect.</summary>
        public const byte QUASAR   = 0x1F;

        /// <summary>$20 — CABSHK: VARI preset 3 — cabinet shake / rumble.</summary>
        public const byte CABSHK   = 0x20;

        /// <summary>
        /// Returns the ROM label name for a command byte, for logging and debug UI.
        /// Returns the hex value as a string for unknown commands.
        /// </summary>
        public static string GetLabel(byte command) => command switch
        {
            Silence => "SILENCE",
            HBDV    => "HBDV",
            STDV    => "STDV",
            DP1V    => "DP1V",
            XBV     => "XBV",
            BBSV    => "BBSV",
            HBEV    => "HBEV",
            PROTV   => "PROTV",
            SPNRV   => "SPNRV",
            CLDWNV  => "CLDWNV",
            SV3     => "SV3",
            ED10    => "ED10",
            ED12    => "ED12",
            ED17    => "ED17",
            SP1     => "SP1",
            BG1     => "BG1",
            BG2INC  => "BG2INC",
            LITE    => "LITE",
            BONV    => "BONV",
            BGEND   => "BGEND",
            TURBO   => "TURBO",
            APPEAR  => "APPEAR",
            THRUST  => "THRUST",
            CANNON  => "CANNON",
            RADIO   => "RADIO",
            HYPER   => "HYPER",
            SCREAM  => "SCREAM",
            ORGANT  => "ORGANT",
            ORGANN  => "ORGANN",
            SAW     => "SAW",
            FOSHIT  => "FOSHIT",
            QUASAR  => "QUASAR",
            CABSHK  => "CABSHK",
            _       => $"0x{command:X2}"
        };
    }
}
