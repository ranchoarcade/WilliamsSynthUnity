namespace WilliamsSynth
{
    /// <summary>
    /// Interface implemented by each of the 8 synthesis generators
    /// (GWave, Vari, Noise, FNoise, Scream, Radio, Hyper, Organ).
    ///
    /// AudioMixer holds a collection of these, calls FillBuffer on each active one
    /// per audio callback, sums the results, and applies soft-clip output.
    ///
    /// All generators produce mono float samples. Channel spreading is handled
    /// by AudioMixer before writing to Unity's interleaved output buffer.
    /// </summary>
    public interface ISoundGenerator
    {
        /// <summary>True while this generator is producing audio output.</summary>
        bool IsActive { get; }

        /// <summary>
        /// Arms the generator for the given raw sound board command.
        /// Resets internal state and starts output. Called by SoundBoardEmulator.
        /// </summary>
        /// <param name="commandId">Raw 5-bit command byte (SoundCommand constants, 0x00–0x1F).</param>
        void Trigger(byte commandId);

        /// <summary>
        /// Halts the generator immediately. Sets IsActive to false and silences output.
        /// Called by SoundBoardEmulator on Silence ($00) or when a new command preempts this one.
        /// </summary>
        void Stop();

        /// <summary>
        /// Fills <paramref name="count"/> mono float samples into <paramref name="buffer"/>
        /// starting at <paramref name="offset"/>.
        ///
        /// Called by AudioMixer once per Unity audio callback. Generators that are not active
        /// must still fill zeros (or may be skipped — AudioMixer checks IsActive first).
        ///
        /// Each sample corresponds to <c>894886.0 / sampleRate</c> 6800 CPU cycles.
        /// Generators advance their internal state by that many cycles per sample to stay
        /// in sync with the original hardware clock rate.
        /// </summary>
        /// <param name="buffer">Output buffer to write into.</param>
        /// <param name="offset">Index of the first sample slot to write.</param>
        /// <param name="count">Number of mono samples to write.</param>
        /// <param name="sampleRate">Unity output sample rate in Hz (e.g. 44100).</param>
        void FillBuffer(float[] buffer, int offset, int count, int sampleRate);
    }
}
