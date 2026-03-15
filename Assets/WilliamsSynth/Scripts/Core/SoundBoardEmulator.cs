namespace WilliamsSynth
{
    /// <summary>
    /// Software equivalent of the Williams Defender sound board receiving a byte at $CC02.
    ///
    /// Owns all synthesis generators and the AudioMixer. Instantiated once by
    /// DefenderSoundBoard.Awake(). Thread-safety note: DispatchCommand() is called on the
    /// Unity main thread (from SoundSequencer.Tick or SendSoundCommand); FillBuffer() is
    /// called on the Unity audio thread. The potential data race on generator state is
    /// accepted for P8 — the rate of Trigger() calls is low and worst-case is a brief
    /// audio glitch. Locking can be added in P10 if needed.
    ///
    /// ── Generator allocation ─────────────────────────────────────────────────────
    ///   _gwave   : GWaveGenerator  — $01–$0C (HBDV–ED12) + $11 (BONV)
    ///   _noise   : NoiseGenerator  — $10 LITE, $13 TURBO, $14 APPEAR
    ///   _fnoise  : FilteredNoiseGenerator — $16 CANNON (one-shot transient)
    ///   _bg1     : FilteredNoiseGenerator — $0E BG1 (persistent loop, stopped by $12 BGEND)
    ///   _thrust  : FilteredNoiseGenerator — $15 THRUST (persistent loop, stopped by SetThrust(false))
    ///   _vari    : VariWaveGenerator       — $1C–$1F (SAW / FOSHIT / QUASAR / CABSHK)
    ///   _scream  : ScreamGenerator         — $19 SCREAM
    ///   _radio   : RadioGenerator          — $17 RADIO
    ///   _hyper   : HyperGenerator          — $18 HYPER
    ///   _organ   : OrganGenerator          — $1A ORGANT, $1B ORGANN
    ///   _spinner : VariWaveGenerator       — $0D SP1 (stub — parameters not yet extracted)
    ///
    /// ── Foreground exclusivity ────────────────────────────────────────────────────
    ///   The original 6800 runs exactly one foreground routine at a time. When a new
    ///   foreground command arrives, the previous foreground generator is Stop()ped
    ///   before the new one is Trigger()ed. This is enforced by TriggerForeground().
    ///   _bg1, _thrust, and _spinner are persistent — they are never stopped by
    ///   foreground dispatch and can mix simultaneously with the foreground sound.
    ///
    /// ── Unimplemented stubs ───────────────────────────────────────────────────────
    ///   $0D SP1     — spinner VARI preset not yet extracted from ROM source
    ///   $0F BG2INC  — background-level increment, not implemented
    /// </summary>
    public sealed class SoundBoardEmulator
    {
        // ── Mixer ─────────────────────────────────────────────────────────────────
        private readonly AudioMixer _mixer;

        // ── Transient generators ──────────────────────────────────────────────────
        private readonly GWaveGenerator         _gwave;
        private readonly NoiseGenerator         _noise;
        private readonly FilteredNoiseGenerator _fnoise;   // CANNON one-shots
        private readonly VariWaveGenerator      _vari;
        private readonly ScreamGenerator        _scream;
        private readonly RadioGenerator         _radio;
        private readonly HyperGenerator         _hyper;
        private readonly OrganGenerator         _organ;

        // ── Persistent generators (loop until explicitly stopped) ─────────────────
        private readonly FilteredNoiseGenerator _bg1;      // $0E BG1 / $12 BGEND
        private readonly FilteredNoiseGenerator _thrust;   // $15 THRUST
        private readonly VariWaveGenerator      _spinner;  // $0D SP1 (stub)

        // ── Foreground exclusivity ────────────────────────────────────────────────
        // The original 6800 runs exactly one foreground sound routine at a time.
        // When a new command is dispatched, the previous foreground generator is
        // stopped so the two cannot mix simultaneously.
        // Persistent generators (_bg1, _thrust, _spinner) are excluded — they run
        // independently of foreground dispatch and must survive command changes.
        private ISoundGenerator _foreground;

        // ── DC-blocking filter state (per-channel) ────────────────────────────────
        // Allocated lazily on first FillBuffer call; grown if channel count changes.
        private float[] _hpX;   // x[n-1] per channel
        private float[] _hpY;   // y[n-1] per channel

        // ─────────────────────────────────────────────────────────────────────────
        public SoundBoardEmulator()
        {
            _mixer   = new AudioMixer();

            _gwave   = new GWaveGenerator();
            _noise   = new NoiseGenerator();
            _fnoise  = new FilteredNoiseGenerator();
            _vari    = new VariWaveGenerator();
            _scream  = new ScreamGenerator();
            _radio   = new RadioGenerator();
            _hyper   = new HyperGenerator();
            _organ   = new OrganGenerator();
            _bg1     = new FilteredNoiseGenerator();
            _thrust  = new FilteredNoiseGenerator();
            _spinner = new VariWaveGenerator();

            _mixer.AddGenerator(_gwave);
            _mixer.AddGenerator(_noise);
            _mixer.AddGenerator(_fnoise);
            _mixer.AddGenerator(_vari);
            _mixer.AddGenerator(_scream);
            _mixer.AddGenerator(_radio);
            _mixer.AddGenerator(_hyper);
            _mixer.AddGenerator(_organ);
            _mixer.AddGenerator(_bg1);
            _mixer.AddGenerator(_thrust);
            _mixer.AddGenerator(_spinner);
        }

        // ── Audio thread entry point ──────────────────────────────────────────────
        /// <summary>
        /// Called from DefenderSoundBoard.OnAudioFilterRead on the Unity audio thread.
        /// Mixes all active generators into the interleaved output buffer, then applies
        /// the DC-blocking high-pass filter that models the hardware speaker rolloff.
        /// </summary>
        public void FillBuffer(float[] data, int channels, int sampleRate)
        {
            _mixer.FillBuffer(data, channels, sampleRate);
            ApplyDCBlock(data, channels, sampleRate);
        }

        // ── Command dispatch (main thread) ────────────────────────────────────────
        /// <summary>
        /// Routes a raw 5-bit sound board command to the appropriate generator.
        /// Equivalent to the 6800 IRQ handler at the top of VSNDRM1.SRC.
        /// Called on the Unity main thread from SoundSequencer.Tick() or directly
        /// from DefenderSoundBoard.SendSoundCommand().
        /// </summary>
        public void DispatchCommand(byte cmd)
        {
            switch (cmd)
            {
                case SoundCommand.Silence:
                    // $00 — no-op (original board ignores command 0 after masking bit 5)
                    break;

                // ── GWAVE $01–$0C ─────────────────────────────────────────────────
                case SoundCommand.HBDV:
                case SoundCommand.STDV:
                case SoundCommand.DP1V:
                case SoundCommand.XBV:
                case SoundCommand.BBSV:
                case SoundCommand.HBEV:
                case SoundCommand.PROTV:
                case SoundCommand.SPNRV:
                case SoundCommand.CLDWNV:
                case SoundCommand.SV3:
                case SoundCommand.ED10:
                case SoundCommand.ED12:
                    TriggerForeground(_gwave, cmd);
                    break;

                // ── Persistent / special $0D–$12 ──────────────────────────────────
                case SoundCommand.SP1:
                    // $0D — spinner VARI preset not yet extracted; stub
                    break;

                case SoundCommand.BG1:
                    // $0E — start persistent background engine hum
                    _bg1.Trigger(SoundCommand.BG1);
                    break;

                case SoundCommand.BG2INC:
                    // $0F — background level increment; not implemented
                    break;

                case SoundCommand.LITE:
                    // $10 — rising swept noise
                    TriggerForeground(_noise, SoundCommand.LITE);
                    break;

                case SoundCommand.BONV:
                    // $11 — GWAVE explosion/bonus tone
                    TriggerForeground(_gwave, SoundCommand.BONV);
                    break;

                case SoundCommand.BGEND:
                    // $12 — stop background engine hum
                    _bg1.Stop();
                    break;

                case SoundCommand.TURBO:
                    // $13 — turbo burst (NOISE, not FNOISE — authentic per VSNDRM1.SRC)
                    TriggerForeground(_noise, SoundCommand.TURBO);
                    break;

                case SoundCommand.APPEAR:
                    // $14 — enemy appear sweep
                    TriggerForeground(_noise, SoundCommand.APPEAR);
                    break;

                case SoundCommand.THRUST:
                    // $15 — persistent thrust loop (dedicated generator, independent of CANNON)
                    // Not routed through TriggerForeground — THRUST is persistent.
                    _thrust.Trigger(SoundCommand.THRUST);
                    break;

                case SoundCommand.CANNON:
                    // $16 — one-shot cannon crack
                    TriggerForeground(_fnoise, SoundCommand.CANNON);
                    break;

                case SoundCommand.RADIO:
                    TriggerForeground(_radio, SoundCommand.RADIO);
                    break;

                case SoundCommand.HYPER:
                    TriggerForeground(_hyper, SoundCommand.HYPER);
                    break;

                case SoundCommand.SCREAM:
                    TriggerForeground(_scream, SoundCommand.SCREAM);
                    break;

                case SoundCommand.ORGANT:
                    TriggerForeground(_organ, SoundCommand.ORGANT);
                    break;

                case SoundCommand.ORGANN:
                    TriggerForeground(_organ, SoundCommand.ORGANN);
                    break;

                // ── VARI presets $1C–$1F ──────────────────────────────────────────
                case SoundCommand.SAW:
                case SoundCommand.FOSHIT:
                case SoundCommand.QUASAR:
                case SoundCommand.CABSHK:
                    TriggerForeground(_vari, cmd);
                    break;
            }
        }

        // ── Foreground helper ─────────────────────────────────────────────────────
        /// <summary>
        /// Stops the currently active foreground generator (if any, and if different
        /// from the incoming one), then triggers the new generator.
        /// This enforces the original hardware constraint of one active foreground
        /// sound at a time.
        /// </summary>
        private void TriggerForeground(ISoundGenerator gen, byte cmd)
        {
            if (_foreground != null && _foreground != gen)
            {
                UnityEngine.Debug.Log(
                    $"[SoundBoard] Preempt: {_foreground.GetType().Name} stopped " +
                    $"→ {gen.GetType().Name} cmd 0x{cmd:X2}");
                _foreground.Stop();
            }
            _foreground = gen;
            gen.Trigger(cmd);
        }

        // ── Persistent sound controls (bypass sequencer) ──────────────────────────

        /// <summary>Starts or stops the persistent BG1 engine hum (FDFLG=0 loop).</summary>
        public void SetBg1(bool on)
        {
            if (on) _bg1.Trigger(SoundCommand.BG1);
            else    _bg1.Stop();
        }

        /// <summary>Starts or stops the persistent thrust noise (FDFLG=0 loop).</summary>
        public void SetThrust(bool on)
        {
            if (on) _thrust.Trigger(SoundCommand.THRUST);
            else    _thrust.Stop();
        }

        /// <summary>Starts or stops the spinner sound (SP1 parameters TBD — stub).</summary>
        public void SetSpinner(bool on)
        {
            if (!on) _spinner.Stop();
            // Start path: SP1 VARI parameters not yet extracted from ROM source
        }

        // ── Test / diagnostic helpers ─────────────────────────────────────────────

        /// <summary>
        /// Stops the currently active foreground generator and clears the foreground slot.
        /// Does NOT affect persistent generators (_bg1, _thrust, _spinner) — those run
        /// independently and must be stopped explicitly via SetBg1/SetThrust/SetSpinner.
        /// Called by DefenderSoundBoard.StopAll() for test-UI force-stop.
        /// </summary>
        public void StopForeground()
        {
            if (_foreground == null) return;
            _foreground.Stop();
            _foreground = null;
        }

        // ── DC-blocking filter (applied post-mix) ─────────────────────────────────

        /// <summary>
        /// Single-pole IIR high-pass filter.  Removes DC and sub-audio content
        /// below ~40 Hz from the final mixed output, matching the rolloff of the
        /// original Defender speaker's coupling capacitor.
        ///
        /// Without this, GWAVE commands whose GPER wraps to 0 (= 256 effective,
        /// ~8 Hz) — such as PROTV after FOFSET=255 — output near-full-scale DC
        /// rather than the near-silent tone heard on real hardware.
        ///
        /// Filter equation (per channel):
        ///   y[n] = x[n] − x_prev + α × y_prev
        ///   α = exp(−2π × fc / fs),  fc = 40 Hz
        ///
        /// Called on the Unity audio thread from FillBuffer().
        /// </summary>
        private void ApplyDCBlock(float[] data, int channels, int sampleRate)
        {
            // α for 40 Hz cutoff at the current sample rate
            float alpha = UnityEngine.Mathf.Exp(-2f * UnityEngine.Mathf.PI * 40f / sampleRate);

            // Allocate (or grow) per-channel state on first call / channel-count change.
            if (_hpX == null || _hpX.Length < channels)
            {
                _hpX = new float[channels];
                _hpY = new float[channels];
            }

            int numFrames = data.Length / channels;
            for (int frame = 0; frame < numFrames; frame++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int   idx = frame * channels + ch;
                    float x   = data[idx];
                    float y   = x - _hpX[ch] + alpha * _hpY[ch];
                    _hpX[ch]  = x;
                    _hpY[ch]  = y;
                    data[idx] = y;
                }
            }
        }
    }
}
