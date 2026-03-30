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

        public static readonly SoundSequence CoinInsert = new SoundSequence(0xFF,
            new SoundStep(1, 0x18, SoundCommand.HYPER));       // $19 HYPER, 24 frames

        public static readonly SoundSequence FreeShip = new SoundSequence(0xFF,
            new SoundStep(1, 0x20, SoundCommand.FOSHIT));      // $1E FOSHIT, 32 frames

        // ── Priority $F0 — player events ─────────────────────────────────────────

        public static readonly SoundSequence PlayerDeath = new SoundSequence(0xF0,
            new SoundStep(2, 0x08, SoundCommand.LITE),         // $11 LITE ×2, 8 frames
            new SoundStep(1, 0x20, SoundCommand.CANNON));      // $17 CANNON ×1, 32 frames

        /// <summary>ST1SND — Start 1-player game. FCB $F0,$01,$40,$0A,0</summary>
        public static readonly SoundSequence Start1Player = new SoundSequence(0xF0,
            new SoundStep(1, 0x40, SoundCommand.SV3));         // $0A SV3, 64 frames

        /// <summary>ST2SND — Start 2-player game. FCB $F0,$01,$10,$0B,0</summary>
        public static readonly SoundSequence Start2Player = new SoundSequence(0xF0,
            new SoundStep(1, 0x10, SoundCommand.ED10));        // $0B ED10, 16 frames

        // ── Priority $E8 — smart bomb / terrain ───────────────────────────────────

        public static readonly SoundSequence TerrainBlow = new SoundSequence(0xE8,
            new SoundStep(1, 0x04, SoundCommand.LASER),        // $14 LASER ×1, 4 frames
            new SoundStep(2, 0x06, SoundCommand.LITE),         // $11 LITE  ×2, 6 frames
            new SoundStep(2, 0x0A, SoundCommand.CANNON));      // $17 CANNON ×2, 10 frames

        public static readonly SoundSequence SmartBomb = new SoundSequence(0xE8,
            new SoundStep(6, 0x04, SoundCommand.LITE),         // $11 LITE ×6, 4 frames
            new SoundStep(1, 0x10, SoundCommand.CANNON));      // $17 CANNON ×1, 16 frames

        // ── Priority $E0 — astronaut events ──────────────────────────────────────

        /// <summary>ACSND — Astronaut caught. FCB $E0,$03,$0A,$08,0</summary>
        public static readonly SoundSequence AstronautCatch = new SoundSequence(0xE0,
            new SoundStep(3, 0x0A, SoundCommand.SPNRV));       // $08 SPNRV ×3, 10 frames

        public static readonly SoundSequence AstronautLand = new SoundSequence(0xE0,
            new SoundStep(1, 0x18, SoundCommand.QUASAR));      // $1F QUASAR, 24 frames

        public static readonly SoundSequence AstronautHit = new SoundSequence(0xE0,
            new SoundStep(1, 0x18, SoundCommand.LITE));        // $11 LITE, 24 frames

        // ── Priority $D8 ──────────────────────────────────────────────────────────

        public static readonly SoundSequence AstronautScream = new SoundSequence(0xD8,
            new SoundStep(1, 0x10, SoundCommand.SCREAM));      // $1A SCREAM, 16 frames

        // ── Priority $D0 — enemy hits / appear ───────────────────────────────────

        public static readonly SoundSequence Appear = new SoundSequence(0xD0,
            new SoundStep(1, 0x30, SoundCommand.APPEAR));      // $15 APPEAR, 48 frames

        /// <summary>PRHSND — Probe hit. FCB $D0,$01,$10,$05,0</summary>
        public static readonly SoundSequence ProbeHit = new SoundSequence(0xD1,
            new SoundStep(1, 0x10, SoundCommand.BBSV));        // $05 BBSV, 16 frames

        public static readonly SoundSequence SchitzHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x08, SoundCommand.CANNON));      // $17 CANNON, 8 frames

        /// <summary>UFHSND — UFO / Bomber hit. FCB $D0,$01,$08,$07,0</summary>
        public static readonly SoundSequence UFOHit = new SoundSequence(0xD0,
            new SoundStep(1, 0x20, SoundCommand.PROTV));       // Extended to 0x20 (512ms) to cover audio tail

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

        public static readonly SoundSequence LanderSuck = new SoundSequence(0xC8,
            new SoundStep(10, 0x01, SoundCommand.SP1));        // $0E SP1 ×10, 1 frame

        // ── Priority $C0 — weapons / movement ────────────────────────────────────

        /// <summary>SWHSND — Swarmer hit. FCB $C0,$01,$08,$07,0</summary>
        public static readonly SoundSequence SwarmerHit = new SoundSequence(0xC0,
            new SoundStep(1, 0x20, SoundCommand.PROTV));       // Extended to 0x20 (512ms) to cover audio tail

        public static readonly SoundSequence Laser = new SoundSequence(0xC0,
            new SoundStep(1, 0x30, SoundCommand.LASER));       // $14 LASER, 48 frames

        public static readonly SoundSequence LanderGrab = new SoundSequence(0xC0,
            new SoundStep(1, 0x20, SoundCommand.RADIO));       // $18 RADIO, 32 frames

        /// <summary>LSHSND — Lander shoots. FCB $C0,$01,$08,$03,0</summary>
        public static readonly SoundSequence LanderShoot = new SoundSequence(0xC0,
            new SoundStep(1, 0x08, SoundCommand.DP1V));        // $03 DP1V, 8 frames

        /// <summary>SSHSND — Schitzo shoots. FCB $C0,$01,$30,$09,0</summary>
        public static readonly SoundSequence SchitzoShoot = new SoundSequence(0xD1,
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

        public static readonly SoundSequence Hyperspace = new SoundSequence(0xC0,
            new SoundStep(1, 0x20, SoundCommand.RADIO));       // $18 RADIO, 32 frames

        /// <summary>
        /// Resolves a <see cref="GameSoundSequenceId"/> to its corresponding <see cref="SoundSequence"/> data.
        /// Returns an empty sequence if the ID is <see cref="GameSoundSequenceId.None"/> or unrecognized.
        /// </summary>
        public static SoundSequence GetSequence(GameSoundSequenceId id) => id switch
        {
            GameSoundSequenceId.CoinInsert       => CoinInsert,
            GameSoundSequenceId.FreeShip         => FreeShip,
            GameSoundSequenceId.PlayerDeath      => PlayerDeath,
            GameSoundSequenceId.Start1Player     => Start1Player,
            GameSoundSequenceId.Start2Player     => Start2Player,
            GameSoundSequenceId.TerrainBlow      => TerrainBlow,
            GameSoundSequenceId.SmartBomb        => SmartBomb,
            GameSoundSequenceId.AstronautCatch   => AstronautCatch,
            GameSoundSequenceId.AstronautLand    => AstronautLand,
            GameSoundSequenceId.AstronautHit     => AstronautHit,
            GameSoundSequenceId.AstronautScream  => AstronautScream,
            GameSoundSequenceId.Appear           => Appear,
            GameSoundSequenceId.ProbeHit         => ProbeHit,
            GameSoundSequenceId.SchitzHit        => SchitzHit,
            GameSoundSequenceId.UFOHit           => UFOHit,
            GameSoundSequenceId.TieHit           => TieHit,
            GameSoundSequenceId.LanderDestroyed  => LanderDestroyed,
            GameSoundSequenceId.LanderPickup     => LanderPickup,
            GameSoundSequenceId.LanderSuck       => LanderSuck,
            GameSoundSequenceId.SwarmerHit       => SwarmerHit,
            GameSoundSequenceId.Laser            => Laser,
            GameSoundSequenceId.LanderGrab       => LanderGrab,
            GameSoundSequenceId.LanderShoot      => LanderShoot,
            GameSoundSequenceId.SchitzoShoot     => SchitzoShoot,
            GameSoundSequenceId.UFOShoot         => UFOShoot,
            GameSoundSequenceId.SwarmerShoot     => SwarmerShoot,
            GameSoundSequenceId.Hyperspace       => Hyperspace,
            _                                    => default
        };
    }
}
