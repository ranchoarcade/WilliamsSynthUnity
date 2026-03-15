namespace WilliamsSynth
{
    /// <summary>
    /// All game sound sequences extracted verbatim from DEFA7.SRC (SVTAB area).
    ///
    /// Each constant is a SoundSequence ready to pass to DefenderSoundBoard.TriggerSequence().
    /// Priority, repeat counts, timer frames (16 ms units), and command IDs are taken
    /// directly from the FCB lines in DEFA7.SRC.
    ///
    /// Source format (DEFA7.SRC header comment):
    ///   SNDPRI, N*(REPCNT, SNDTMR, SND#)  — SNDTMR in 16 ms units
    ///   SNDPRI: higher byte = higher priority; equal priority CAN interrupt.
    ///   Sequence ends when REPCNT = 0.
    /// </summary>
    public static class GameSoundSequences
    {
        // ── Priority $FF — highest (coin / free ship) ─────────────────────────────

        /// <summary>CNSND — Coin inserted. FCB $FF,$01,$18,$19,0</summary>
        public static readonly SoundSequence CoinInsert = new SoundSequence(0xFF,
            new SoundStep(1, 0x18, SoundCommand.SCREAM));      // $19 SCREAM, 24 frames

        /// <summary>RPSND — Free ship awarded. FCB $FF,$01,$20,$1E,0</summary>
        public static readonly SoundSequence FreeShip = new SoundSequence(0xFF,
            new SoundStep(1, 0x20, SoundCommand.QUASAR));      // $1E QUASAR, 32 frames

        // ── Priority $F0 — player events ─────────────────────────────────────────

        /// <summary>PDSND — Player ship destroyed. FCB $F0,$02,$08,$11,$01,$20,$17,0</summary>
        public static readonly SoundSequence PlayerDeath = new SoundSequence(0xF0,
            new SoundStep(2, 0x08, SoundCommand.BONV),         // $11 BONV ×2, 8 frames
            new SoundStep(1, 0x20, SoundCommand.RADIO));       // $17 RADIO ×1, 32 frames

        /// <summary>ST1SND — Start 1-player game. FCB $F0,$01,$40,$0A,0</summary>
        public static readonly SoundSequence Start1Player = new SoundSequence(0xF0,
            new SoundStep(1, 0x40, SoundCommand.SV3));         // $0A SV3, 64 frames

        /// <summary>ST2SND — Start 2-player game. FCB $F0,$01,$10,$0B,0</summary>
        public static readonly SoundSequence Start2Player = new SoundSequence(0xF0,
            new SoundStep(1, 0x10, SoundCommand.ED10));        // $0B ED10, 16 frames

        // ── Priority $E8 — smart bomb / terrain ───────────────────────────────────

        /// <summary>TBSND — Terrain blow. FCB $E8,$01,$04,$14,$02,$06,$11,$02,$0A,$17,0</summary>
        public static readonly SoundSequence TerrainBlow = new SoundSequence(0xE8,
            new SoundStep(1, 0x04, SoundCommand.APPEAR),       // $14 APPEAR ×1, 4 frames
            new SoundStep(2, 0x06, SoundCommand.BONV),         // $11 BONV   ×2, 6 frames
            new SoundStep(2, 0x0A, SoundCommand.RADIO));       // $17 RADIO  ×2, 10 frames

        /// <summary>SBSND — Smart bomb. FCB $E8,$06,$04,$11,$01,$10,$17,0</summary>
        public static readonly SoundSequence SmartBomb = new SoundSequence(0xE8,
            new SoundStep(6, 0x04, SoundCommand.BONV),         // $11 BONV ×6, 4 frames
            new SoundStep(1, 0x10, SoundCommand.RADIO));       // $17 RADIO ×1, 16 frames

        // ── Priority $E0 — astronaut events ──────────────────────────────────────

        /// <summary>ACSND — Astronaut caught. FCB $E0,$03,$0A,$08,0</summary>
        public static readonly SoundSequence AstronautCatch = new SoundSequence(0xE0,
            new SoundStep(3, 0x0A, SoundCommand.SPNRV));       // $08 SPNRV ×3, 10 frames

        /// <summary>ALSND — Astronaut lands safely. FCB $E0,$01,$18,$1F,0</summary>
        public static readonly SoundSequence AstronautLand = new SoundSequence(0xE0,
            new SoundStep(1, 0x18, SoundCommand.CABSHK));      // $1F CABSHK, 24 frames

        /// <summary>AHSND — Astronaut hit. FCB $E0,$01,$18,$11,0</summary>
        public static readonly SoundSequence AstronautHit = new SoundSequence(0xE0,
            new SoundStep(1, 0x18, SoundCommand.BONV));        // $11 BONV, 24 frames

        // ── Priority $D8 ──────────────────────────────────────────────────────────

        /// <summary>ASCSND — Astronaut scream (lander abducting). FCB $D8,$01,$10,$1A,0</summary>
        public static readonly SoundSequence AstronautScream = new SoundSequence(0xD8,
            new SoundStep(1, 0x10, SoundCommand.ORGANT));      // $1A ORGANT, 16 frames

        // ── Priority $D0 — enemy hits / appear ───────────────────────────────────

        /// <summary>APSND — Enemy appears. FCB $D0,$01,$30,$15,0</summary>
        public static readonly SoundSequence Appear = new SoundSequence(0xD0,
            new SoundStep(1, 0x30, SoundCommand.THRUST));      // $15 THRUST, 48 frames

        /// <summary>PRHSND — Probe hit. FCB $D0,$01,$10,$05,0</summary>
        public static readonly SoundSequence ProbeHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x10, SoundCommand.BBSV));        // $05 BBSV, 16 frames

        /// <summary>SCHSND — Schitz (Swarmers) hit. FCB $D0,$01,$08,$17,0</summary>
        public static readonly SoundSequence SchitzHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x08, SoundCommand.RADIO));       // $17 RADIO, 8 frames

        /// <summary>UFHSND — UFO / Bomber hit. FCB $D0,$01,$08,$07,0</summary>
        public static readonly SoundSequence UFOHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x08, SoundCommand.PROTV));       // $07 PROTV, 8 frames

        /// <summary>TIHSND — Tie Fighter hit. FCB $D0,$01,$0A,$01,0</summary>
        public static readonly SoundSequence TieHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x0A, SoundCommand.HBDV));        // $01 HBDV, 10 frames

        /// <summary>LHSND — Lander destroyed. FCB $D0,$01,$0A,$06,0</summary>
        public static readonly SoundSequence LanderDestroyed = new SoundSequence(0xD0,
            new SoundStep(1, 0x0A, SoundCommand.HBEV));        // $06 HBEV, 10 frames

        /// <summary>LPKSND — Lander picks up astronaut. FCB $D0,$01,$10,$0B,0</summary>
        public static readonly SoundSequence LanderPickup = new SoundSequence(0xD0,
            new SoundStep(1, 0x10, SoundCommand.ED10));        // $0B ED10, 16 frames

        // ── Priority $C8 ──────────────────────────────────────────────────────────

        /// <summary>LSKSND — Lander mutant suck (repeating). FCB $C8,$0A,$01,$0E,0</summary>
        public static readonly SoundSequence LanderSuck = new SoundSequence(0xC8,
            new SoundStep(10, 0x01, SoundCommand.BG1));        // $0E BG1 ×10, 1 frame

        // ── Priority $C0 — weapons / movement ────────────────────────────────────

        /// <summary>SWHSND — Swarmer hit. FCB $C0,$01,$08,$07,0</summary>
        public static readonly SoundSequence SwarmerHit = new SoundSequence(0xC0,
            new SoundStep(1, 0x08, SoundCommand.PROTV));       // $07 PROTV, 8 frames

        /// <summary>LASSND — Laser fired. FCB $C0,$01,$30,$14,0</summary>
        public static readonly SoundSequence Laser = new SoundSequence(0xC0,
            new SoundStep(1, 0x30, SoundCommand.APPEAR));      // $14 APPEAR, 48 frames

        /// <summary>LGSND — Lander grabs astronaut. FCB $C0,$01,$20,$18,0</summary>
        public static readonly SoundSequence LanderGrab = new SoundSequence(0xC0,
            new SoundStep(1, 0x20, SoundCommand.HYPER));       // $18 HYPER, 32 frames

        /// <summary>LSHSND — Lander shoots. FCB $C0,$01,$08,$03,0</summary>
        public static readonly SoundSequence LanderShoot = new SoundSequence(0xC0,
            new SoundStep(1, 0x08, SoundCommand.DP1V));        // $03 DP1V, 8 frames

        /// <summary>SSHSND — Schitzo shoots. FCB $C0,$01,$30,$09,0</summary>
        public static readonly SoundSequence SchitzoShoot = new SoundSequence(0xC0,
            new SoundStep(1, 0x30, SoundCommand.CLDWNV));      // $09 CLDWNV, 48 frames

        /// <summary>USHSND — UFO / Bomber shoots. FCB $C0,$01,$08,$03,0</summary>
        public static readonly SoundSequence UFOShoot = new SoundSequence(0xC0,
            new SoundStep(1, 0x08, SoundCommand.DP1V));        // $03 DP1V, 8 frames

        /// <summary>SWSSND — Swarmer shoots. FCB $C0,$01,$18,$0C,0</summary>
        public static readonly SoundSequence SwarmerShoot = new SoundSequence(0xC0,
            new SoundStep(1, 0x18, SoundCommand.ED12));        // $0C ED12, 24 frames

        // ── Continuous sounds (managed outside sequencer) ─────────────────────────
        // Thrust ($15) and Hyperspace ($18) are triggered by the main game board
        // continuously while their conditions are active (handled via THFLG in SNDSEQ).
        // These are provided as single-step sequences for use with TriggerSequence()
        // if needed, but normally fired via SendSoundCommand() or the continuous path.

        /// <summary>Hyperspace entry. Not in DEFA7.SRC SVTAB — sent direct. Priority $C0.</summary>
        public static readonly SoundSequence Hyperspace = new SoundSequence(0xC0,
            new SoundStep(1, 0x20, SoundCommand.HYPER));       // $18 HYPER, 32 frames
    }
}
