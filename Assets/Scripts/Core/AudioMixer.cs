using System;
using System.Collections.Generic;
using UnityEngine;

namespace WilliamsSynth
{
    /// <summary>
    /// Sums output from all active ISoundGenerator instances and writes the result into
    /// Unity's interleaved audio buffer (OnAudioFilterRead).
    ///
    /// Mixing model:
    ///   1. Each active generator fills a mono scratch buffer.
    ///   2. Scratch buffers are summed sample-by-sample.
    ///   3. Sum is passed through Math.Tanh(x * 0.7f) soft-clip to approximate
    ///      the natural headroom of the original TBA2002 analog output stage.
    ///   4. The clipped mono signal is written to all channels of Unity's output buffer.
    ///
    /// cyclesPerSample (= 894886 / outputSampleRate ≈ 20.293 at 44,100 Hz) is computed
    /// once when the sample rate is first seen and passed to generators via sampleRate.
    /// </summary>
    public sealed class AudioMixer
    {
        private const double CpuClockHz = 894886.0;

        private readonly List<ISoundGenerator> _generators = new List<ISoundGenerator>();

        // Scratch buffers — reused each callback to avoid GC allocations on the audio thread.
        private float[] _scratch  = Array.Empty<float>();
        private float[] _monoSum  = Array.Empty<float>();

        /// <summary>
        /// Registers a generator with the mixer. Call once at startup for each of the 8
        /// synthesis generators before the first audio callback arrives.
        /// </summary>
        public void AddGenerator(ISoundGenerator generator)
        {
            if (generator != null)
                _generators.Add(generator);
        }

        /// <summary>
        /// Called from DefenderSoundBoard.OnAudioFilterRead. Fills Unity's interleaved
        /// output buffer with the mixed, soft-clipped output of all active generators.
        /// </summary>
        /// <param name="data">Unity interleaved sample buffer (channels × frames).</param>
        /// <param name="channels">Number of channels (typically 1 or 2).</param>
        /// <param name="sampleRate">Unity output sample rate in Hz.</param>
        public void FillBuffer(float[] data, int channels, int sampleRate)
        {
            int frameCount = data.Length / channels;
            if (frameCount == 0) return;

            // Ensure scratch buffers are large enough (grow-only, no GC once stable).
            if (_scratch.Length < frameCount)
                _scratch = new float[frameCount];
            if (_monoSum.Length < frameCount)
                _monoSum = new float[frameCount];

            // Zero the sum buffer before accumulating generators.
            Array.Clear(_monoSum, 0, frameCount);
            float[] monoSum = _monoSum;

            foreach (var gen in _generators)
            {
                if (!gen.IsActive) continue;

                // Zero the scratch slot for this generator.
                Array.Clear(_scratch, 0, frameCount);
                gen.FillBuffer(_scratch, 0, frameCount, sampleRate);

                for (int i = 0; i < frameCount; i++)
                    monoSum[i] += _scratch[i];
            }

            // Soft-clip + write to all channels.
            for (int frame = 0; frame < frameCount; frame++)
            {
                float clipped = (float)Math.Tanh(monoSum[frame] * 0.7f);
                int baseIndex = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                    data[baseIndex + ch] = clipped;
            }
        }

        /// <summary>
        /// Returns the 6800 CPU cycles elapsed per Unity audio sample at the given rate.
        /// Generators use this to advance their internal phase accumulators at the
        /// correct speed relative to the original hardware clock.
        /// </summary>
        public static double CyclesPerSample(int sampleRate) =>
            sampleRate > 0 ? CpuClockHz / sampleRate : CpuClockHz / 44100.0;
    }
}
