namespace WilliamsSynth
{
    /// <summary>
    /// Models the MC1408 / DAC1408 8-bit DAC (IC6) on the Williams Defender sound board.
    ///
    /// The 6800 CPU writes unsigned 8-bit values to PIA Port A ($0400). Port A drives the
    /// DAC1408 directly: $00 = full negative, $80 = zero, $FF = near-full positive.
    ///
    /// Normalisation to Unity float:
    ///   float = (b - 128) / 128.0f
    ///
    ///   $00 → −1.000f  (full negative)
    ///   $80 → +0.000f  (zero / silence mid-point)
    ///   $FF → +0.992f  (≈ 127/128 — intentional asymmetry; the DAC cannot reach +1.0)
    ///
    /// The asymmetry is a hardware property: the DAC has 256 steps below $80 but only
    /// 127 steps above it. Clamping or compensating for this would reduce fidelity —
    /// leave it as-is. The hardware amplifier (TBA2002) AC-couples the output, so the
    /// DC offset introduced by this asymmetry is removed naturally.
    /// </summary>
    public static class DAC1408
    {
        /// <summary>
        /// Converts an 8-bit unsigned DAC value to a Unity audio float.
        /// </summary>
        /// <param name="b">Raw byte written to PIA Port A (0x00–0xFF).</param>
        /// <returns>Normalised float in [−1.000, +0.992].</returns>
        public static float ToFloat(byte b) => (b - 128) / 128.0f;

        /// <summary>
        /// Converts a Unity audio float back to the nearest 8-bit DAC byte.
        /// Inverse of ToFloat — useful for unit test round-trip verification.
        /// </summary>
        /// <param name="f">Float value in [−1.0, +1.0].</param>
        /// <returns>Unsigned byte in [0, 255], clamped.</returns>
        public static byte ToByte(float f)
        {
            int v = (int)(f * 128.0f) + 128;
            if (v < 0)   v = 0;
            if (v > 255) v = 255;
            return (byte)v;
        }
    }
}
