namespace WilliamsSynth
{
    /// <summary>
    /// One entry in a sound sequence, corresponding to the 3-byte entries in the
    /// DEFA7.SRC SNDTBL format: [RepeatCount, Timer, SoundID].
    ///
    ///   RepeatCount → SNDREP — how many times CommandId is fired before advancing
    ///   TimerFrames → SNDTMR — duration in 16 ms units (60 Hz frames)
    ///   CommandId   → raw 5-bit sound board command (0x00–0x1F)
    /// </summary>
    public readonly struct SoundStep
    {
        /// <summary>SNDREP — number of times to fire CommandId before advancing to the next step.</summary>
        public readonly int  RepeatCount;

        /// <summary>SNDTMR — duration in 16 ms frames (60 Hz) before advancing to the next step.</summary>
        public readonly int  TimerFrames;

        /// <summary>Raw 5-bit sound board command (SoundCommand constants, 0x00–0x1F).</summary>
        public readonly byte CommandId;

        public SoundStep(int repeatCount, int timerFrames, byte commandId)
        {
            RepeatCount = repeatCount;
            TimerFrames = timerFrames;
            CommandId   = commandId;
        }
    }

    /// <summary>
    /// A complete sound sequence: priority byte + ordered steps + implicit terminator.
    /// Mirrors the DEFA7.SRC sound table format: Priority, N×[RepeatCount, Timer, SoundID], $00.
    ///
    /// Immutable — safe to store as a static constant (see GameSoundSequences).
    /// Pass to DefenderSoundBoard.TriggerSequence() for full priority arbitration,
    /// multi-step timing, and repeat-count logic (Level 1b API).
    /// </summary>
    public readonly struct SoundSequence
    {
        /// <summary>SNDPRI — higher byte = higher priority. Equal priority interrupts the current sequence.</summary>
        public readonly byte        Priority;

        /// <summary>Steps played in order; sequence ends when all steps are exhausted.</summary>
        public readonly SoundStep[] Steps;

        /// <param name="priority">SNDPRI byte — e.g. 0xFF (coin), 0xF0 (player death), 0xC0 (laser).</param>
        /// <param name="steps">Ordered sequence of sound steps (params — list them inline).</param>
        public SoundSequence(byte priority, params SoundStep[] steps)
        {
            Priority = priority;
            Steps    = steps ?? System.Array.Empty<SoundStep>();
        }
    }
}
