namespace WilliamsSynth
{
    /// <summary>
    /// Replicates the exact 6800 LFSR sequence from VSNDRM1.SRC (LITEN/NOISE/FNOISE routines).
    ///
    /// 16-bit state is held in two RAM bytes HI ($09) and LO ($0A).
    ///
    /// Assembly sequence executed per noise bit:
    ///   LDAA  LO          ; load LO into A
    ///   LSRA              ; A = LO >> 1,              carry = LO bit 0
    ///   LSRA              ; A = LO >> 2,              carry = LO bit 1
    ///   LSRA              ; A = LO >> 3,              carry = LO bit 2
    ///   EORA  LO          ; A = (LO >> 3) XOR LO     (feedback polynomial)
    ///   LSRA              ; A = ((LO>>3) XOR LO) >> 1, carry = feedback bit
    ///   ROR   HI          ; HI = (carry&lt;&lt;7)|(HI>>1),  carry = old HI bit 0
    ///   ROR   LO          ; LO = (carry&lt;&lt;7)|(LO>>1),  carry = old LO bit 0 = OUTPUT
    ///
    /// Polynomial: feedback = ((LO >> 3) XOR LO) bit 0, fed into HI bit 7 via ROR.
    /// Output bit: the old LO bit 0 shifted out by the final ROR LO.
    /// Seed must be non-zero; hardware initial value is unspecified — default 0x0001.
    /// </summary>
    public sealed class LFSR16
    {
        private ushort _state;

        /// <param name="seed">Initial 16-bit state. Must be non-zero (all-zero freezes the LFSR).</param>
        public LFSR16(ushort seed = 0x0001)
        {
            _state = seed != 0 ? seed : (ushort)0x0001;
        }

        /// <summary>
        /// Advances the LFSR one step and returns the output bit.
        /// This is the exact 6800 ROR-based sequence from VSNDRM1.SRC.
        /// Call once per synthesis sample to generate white noise.
        /// </summary>
        /// <returns>One pseudo-random bit (true or false).</returns>
        public bool Clock()
        {
            byte lo = (byte)(_state & 0xFF);
            byte hi = (byte)(_state >> 8);

            // LSRA ×3 + EORA LO: compute feedback polynomial (LO >> 3) XOR LO
            byte a = (byte)((lo >> 3) ^ lo);

            // LSRA: feedback bit → carry; a itself is not used further
            bool carry = (a & 0x01) != 0;

            // ROR HI: carry → HI bit 7, old HI bit 0 → carry
            bool nextCarry = (hi & 0x01) != 0;
            hi    = (byte)((hi >> 1) | (carry ? 0x80 : 0x00));
            carry = nextCarry;

            // ROR LO: carry → LO bit 7, old LO bit 0 → carry = output bit
            bool outputBit = (lo & 0x01) != 0;
            lo = (byte)((lo >> 1) | (carry ? 0x80 : 0x00));

            _state = (ushort)((hi << 8) | lo);
            return outputBit;
        }

        /// <summary>
        /// Resets the LFSR to a new seed. Seed must be non-zero.
        /// </summary>
        public void Reset(ushort seed = 0x0001)
        {
            _state = seed != 0 ? seed : (ushort)0x0001;
        }

        /// <summary>Current 16-bit state (HI:LO). Exposed for unit testing.</summary>
        public ushort State => _state;
    }
}
