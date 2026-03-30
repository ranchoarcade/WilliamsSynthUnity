using System;
using System.Collections;
using UnityEngine;

namespace WilliamsSynth
{
    /// <summary>
    /// MonoBehaviour entry point for the Williams Defender sound board emulator.
    ///
    /// Attach to the "SoundBoard" GameObject (created by WilliamsSynth > Setup Scene).
    /// Requires an AudioSource component; audio is generated via OnAudioFilterRead
    /// on the Unity audio thread, driven by SoundBoardEmulator / AudioMixer.
    ///
    /// PUBLIC API — three distinct levels:
    ///
    ///   Level 1a — Named game-event wrappers (TriggerCoinInsert, TriggerPlayerDeath, …)
    ///              Delegate to TriggerSequence with constants from GameSoundSequences.
    ///
    ///   Level 1b — TriggerSequence(SoundSequence)
    ///              THE canonical Level 1 entry point. Loads a SoundSequence into
    ///              SoundSequencer for full priority arbitration, multi-step timing,
    ///              and repeat-count logic.
    ///
    ///   Level 2  — SendSoundCommand(byte)
    ///              Direct equivalent of the main CPU writing one byte to $CC02.
    ///              Bypasses SoundSequencer — no priority, no timing.
    ///              Use for testing and diagnostics.
    ///
    ///   Persistent — TriggerThrust(bool), SetBackground(bool)
    ///              Looping sounds that run independently of the sequencer.
    ///
    /// SMOKE TEST (P1.5):
    ///   While _smokeTestActive is true, OnAudioFilterRead outputs a 440 Hz sine wave.
    ///   Call StopSmokeTest() or set enabled = false to silence it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DefenderSoundBoard : MonoBehaviour
    {
        // ── Inspector knobs ───────────────────────────────────────────────────────

        [Tooltip("Plays a 440 Hz sine wave on startup to verify the audio pipeline. " +
                 "Disable once the smoke test passes.")]
        [SerializeField] private bool _smokeTestOnAwake = true;

        // ── P8 core ───────────────────────────────────────────────────────────────

        private SoundBoardEmulator _emulator;
        private SoundSequencer     _sequencer;

        // ── Audio thread support ──────────────────────────────────────────────────

        // Cached on the main thread in Awake(); AudioSettings.outputSampleRate is
        // main-thread-only since Unity 5.0 — never call it from OnAudioFilterRead.
        private int _sampleRate;

        // Smoke-test sine oscillator state (audio thread only).
        private bool   _smokeTestActive;
        private double _smokeTestPhase;
        private const double SmokeTestFreq = 440.0;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _emulator   = new SoundBoardEmulator();
            _sequencer  = new SoundSequencer(_emulator.DispatchCommand);

            if (_smokeTestOnAwake)
                StartSmokeTest();

            Debug.Log($"[WilliamsSynth] DefenderSoundBoard Awake — " +
                      $"sampleRate={_sampleRate} Hz, " +
                      $"cyclesPerSample={AudioMixer.CyclesPerSample(_sampleRate):F4}");
        }

        /// <summary>
        /// Called by Unity once per frame on the main thread.
        /// Advances the SoundSequencer timer so step commands fire at the correct time.
        /// </summary>
        private void Update()
        {
            _sequencer.Tick(Time.deltaTime);
        }

        // ── Audio thread ──────────────────────────────────────────────────────────

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_smokeTestActive)
            {
                FillSmokeTest(data, channels, _sampleRate);
                return;
            }

            _emulator.FillBuffer(data, channels, _sampleRate);
        }

        // ── Smoke-test helpers ────────────────────────────────────────────────────

        /// <summary>Starts the 440 Hz sine wave smoke test.</summary>
        public void StartSmokeTest()
        {
            _smokeTestActive = true;
            _smokeTestPhase  = 0.0;
            Debug.Log("[WilliamsSynth] Smoke test started — 440 Hz sine wave.");
        }

        /// <summary>Stops the smoke test and hands control to the emulator.</summary>
        public void StopSmokeTest()
        {
            _smokeTestActive = false;
            Debug.Log("[WilliamsSynth] Smoke test stopped.");
        }

        /// <summary>
        /// Hard-stops everything: smoke test, active sequence, and the foreground generator.
        /// Resets the sequencer priority to 0 so any subsequent TriggerSequence() call will
        /// succeed regardless of what was playing. Persistent sounds (Thrust, BG1, Spinner)
        /// are NOT affected — stop those explicitly via TriggerThrust/SetBackground/SetSpinner.
        ///
        /// Use in the test UI before each trigger so you always hear the intended sound
        /// without being blocked by a lingering high-priority sequence.
        /// </summary>
        public void StopAll()
        {
            _smokeTestActive = false;
            _sequencer.Reset();
            _emulator.StopForeground();
        }

        private void FillSmokeTest(float[] data, int channels, int sampleRate)
        {
            double phaseStep = 2.0 * Math.PI * SmokeTestFreq / sampleRate;
            int frameCount   = data.Length / channels;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float sample    = (float)Math.Sin(_smokeTestPhase);
                _smokeTestPhase += phaseStep;
                if (_smokeTestPhase >= 2.0 * Math.PI)
                    _smokeTestPhase -= 2.0 * Math.PI;

                int baseIndex = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                    data[baseIndex + ch] = sample;
            }
        }

        // ── P3 test helpers ───────────────────────────────────────────────────────

        [ContextMenu("Test / LITE ($11)")]
        public void TestLite()   { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.LITE);   }

        [ContextMenu("Test / APPEAR ($15)")]
        public void TestAppear() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.APPEAR); }

        [ContextMenu("Test / LASER ($14)")]
        public void TestLaser()  { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.LASER);  }

        [ContextMenu("Test / BG1 ($0F) start")]
        public void TestBg1On()  { StopSmokeTest(); _emulator.SetBg1(true);  }

        [ContextMenu("Test / BG1 stop")]
        public void TestBg1Off() { _emulator.SetBg1(false); }

        [ContextMenu("Test / THRUST ($15) start")]
        public void TestThrustOn()  { StopSmokeTest(); _emulator.SetThrust(true);  }

        [ContextMenu("Test / THRUST stop")]
        public void TestThrustOff() { _emulator.SetThrust(false); }

        [ContextMenu("Test / CANNON ($17)")]
        public void TestCannon() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.CANNON); }

        // ── P4 GWAVE test helpers ─────────────────────────────────────────────────

        [ContextMenu("Test / HBDV ($01) — Heartbeat")]
        public void TestHbdv()   { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.HBDV);   }

        [ContextMenu("Test / BBSV ($05) — Big Ben")]
        public void TestBbsv()   { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.BBSV);   }

        [ContextMenu("Test / BONV ($12) — Explosion/Bonus")]
        public void TestBonv()   { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.BONV);   }

        [ContextMenu("Test / STDV ($02) — Start Distorto")]
        public void TestStdv()   { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.STDV);   }

        [ContextMenu("Test / CLDWNV ($09) — Cool Down")]
        public void TestCldwnv() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.CLDWNV); }

        // ── P5 VARI test helpers ──────────────────────────────────────────────────

        [ContextMenu("Test / SAW ($1D)")]
        public void TestSaw()    { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.SAW);    }

        [ContextMenu("Test / FOSHIT ($1E)")]
        public void TestFoshit() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.FOSHIT); }

        [ContextMenu("Test / QUASAR ($1F)")]
        public void TestQuasar() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.QUASAR); }

        [ContextMenu("Test / CABSHK ($20)")]
        public void TestCabshk() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.CABSHK); }

        // ── P6 test helpers ───────────────────────────────────────────────────────

        [ContextMenu("Test / SCREAM ($1A) — 4-voice wail")]
        public void TestScream() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.SCREAM); }

        [ContextMenu("Test / RADIO ($18) — warbling sweep")]
        public void TestRadio()  { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.RADIO);  }

        [ContextMenu("Test / HYPER ($19) — hyperspace sweep")]
        public void TestHyper()  { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.HYPER);  }

        // ── P7 ORGAN test helpers ─────────────────────────────────────────────────

        [ContextMenu("Test / ORGANT ($1B) — Bach Toccata")]
        public void TestOrgant() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.ORGANT); }

        [ContextMenu("Test / ORGANN ($1C) — Phantom")]
        public void TestOrgann() { StopSmokeTest(); _emulator.DispatchCommand(SoundCommand.ORGANN); }

        // ── P8 sequence test helpers ──────────────────────────────────────────────

        [ContextMenu("Test / Sequence — CoinInsert (priority $FF)")]
        public void TestSeqCoinInsert()  { StopSmokeTest(); TriggerSequence(GameSoundSequences.CoinInsert);  }

        [ContextMenu("Test / Sequence — PlayerDeath (priority $F0)")]
        public void TestSeqPlayerDeath() { StopSmokeTest(); TriggerSequence(GameSoundSequences.PlayerDeath); }

        [ContextMenu("Test / Sequence — SmartBomb (priority $E8)")]
        public void TestSeqSmartBomb()   { StopSmokeTest(); TriggerSequence(GameSoundSequences.SmartBomb);   }

        [ContextMenu("Test / Sequence — Laser (priority $C0)")]
        public void TestSeqLaser()       { StopSmokeTest(); TriggerSequence(GameSoundSequences.Laser);       }

        // ── P8 automated priority tests ───────────────────────────────────────────
        // Each test fires two sequences automatically with a delay between them so
        // you don't need to hit two menu items in quick succession.
        // Watch the Console for pass/fail annotations and listen to the audio output.

        // PlayerDeath duration:  BONV×2 × 128 ms + RADIO×1 × 512 ms = ~768 ms total
        // CoinInsert duration:   SCREAM×1 × 384 ms total
        // Trigger-within timing: 150 ms into the first sequence (safely inside step 1)

        [ContextMenu("P8 Test / 1: PlayerDeath ($F0) blocks Laser ($C0)")]
        public void P8Test1_BlockedByHigherPriority() =>
            StartCoroutine(Co_BlockedByHigherPriority());

        private IEnumerator Co_BlockedByHigherPriority()
        {
            StopSmokeTest();
            Debug.Log("[P8 Test 1] Triggering PlayerDeath ($F0) — BONV×2 then RADIO...");
            TriggerSequence(GameSoundSequences.PlayerDeath);

            yield return new WaitForSeconds(0.15f);

            byte priBefore = _sequencer.CurrentPriority;
            Debug.Log($"[P8 Test 1] Attempting Laser ($C0) while priority={priBefore} — " +
                      $"EXPECTED: dropped (Laser priority $C0 < $F0).");
            TriggerSequence(GameSoundSequences.Laser);

            // Priority should be unchanged — Laser was dropped
            byte priAfter = _sequencer.CurrentPriority;
            bool passed = priAfter == priBefore;
            Debug.Log($"[P8 Test 1] Priority after attempt: {priAfter}. " +
                      (passed ? "PASS — priority held." : "FAIL — priority changed!"));
            Debug.Log("[P8 Test 1] LISTEN: PlayerDeath should play to completion uninterrupted.");
        }

        [ContextMenu("P8 Test / 2: CoinInsert ($FF) interrupts PlayerDeath ($F0)")]
        public void P8Test2_InterruptedByHigherPriority() =>
            StartCoroutine(Co_InterruptedByHigherPriority());

        private IEnumerator Co_InterruptedByHigherPriority()
        {
            StopSmokeTest();
            Debug.Log("[P8 Test 2] Triggering PlayerDeath ($F0)...");
            TriggerSequence(GameSoundSequences.PlayerDeath);

            yield return new WaitForSeconds(0.15f);

            Debug.Log($"[P8 Test 2] Interrupting with CoinInsert ($FF) — " +
                      $"EXPECTED: PlayerDeath cut off, SCREAM starts.");
            TriggerSequence(GameSoundSequences.CoinInsert);

            byte priAfter = _sequencer.CurrentPriority;
            bool passed = priAfter == 0xFF;
            Debug.Log($"[P8 Test 2] Priority after interrupt: 0x{priAfter:X2}. " +
                      (passed ? "PASS — priority is $FF." : "FAIL — expected $FF!"));
            Debug.Log("[P8 Test 2] LISTEN: BONV should cut off, then SCREAM plays.");
        }

        [ContextMenu("P8 Test / 3: Equal priority — second CoinInsert restarts first")]
        public void P8Test3_EqualPriorityInterrupt() =>
            StartCoroutine(Co_EqualPriorityInterrupt());

        private IEnumerator Co_EqualPriorityInterrupt()
        {
            StopSmokeTest();
            Debug.Log("[P8 Test 3] Triggering CoinInsert ($FF) — first SCREAM...");
            TriggerSequence(GameSoundSequences.CoinInsert);

            yield return new WaitForSeconds(0.1f);

            Debug.Log("[P8 Test 3] Triggering CoinInsert ($FF) again — " +
                      "EXPECTED: SCREAM restarts from the beginning (equal priority interrupts).");
            TriggerSequence(GameSoundSequences.CoinInsert);

            byte priAfter = _sequencer.CurrentPriority;
            bool passed = priAfter == 0xFF;
            Debug.Log($"[P8 Test 3] Priority: 0x{priAfter:X2}. " +
                      (passed ? "PASS — sequence reloaded." : "FAIL — expected $FF!"));
            Debug.Log("[P8 Test 3] LISTEN: SCREAM should restart with a brief audible cut.");
        }

        [ContextMenu("P8 Test / 4: PlayerDeath step order (BONV×2 then RADIO)")]
        public void P8Test4_PlayerDeathStepOrder() =>
            StartCoroutine(Co_PlayerDeathStepOrder());

        private IEnumerator Co_PlayerDeathStepOrder()
        {
            StopSmokeTest();
            // PlayerDeath: SoundStep(2, 8, BONV) + SoundStep(1, 32, RADIO)
            //   BONV fires at t=0 ms
            //   BONV fires at t=128 ms  (8 frames × 16 ms)
            //   RADIO fires at t=256 ms (8 frames × 16 ms again)
            //   Sequence ends at t=768 ms (256 + 32 frames × 16 ms)
            Debug.Log("[P8 Test 4] t=0 ms   — Triggering PlayerDeath. LISTEN: BONV should fire NOW.");
            TriggerSequence(GameSoundSequences.PlayerDeath);

            yield return new WaitForSeconds(0.128f);
            Debug.Log("[P8 Test 4] t=128 ms — BONV should fire again NOW (repeat 2 of 2).");

            yield return new WaitForSeconds(0.128f);
            Debug.Log("[P8 Test 4] t=256 ms — RADIO should start NOW.");

            yield return new WaitForSeconds(0.512f);
            bool sequenceDone = !_sequencer.IsActive;
            Debug.Log($"[P8 Test 4] t=768 ms — Sequence should be complete. " +
                      (sequenceDone ? "PASS — sequencer is idle." : "FAIL — sequencer still active!"));
        }

        // ── Level 1b — Generic sequence ───────────────────────────────────────────

        /// <summary>
        /// THE canonical Level 1 entry point. Loads a SoundSequence into the
        /// SoundSequencer for full priority arbitration, multi-step timing, and
        /// repeat-count logic. All named Trigger* methods call this.
        ///
        /// A sequence is silently dropped if lower priority than the active sequence.
        /// An equal or higher-priority sequence interrupts the current one.
        /// </summary>
        public void TriggerSequence(SoundSequence sequence)
        {
            _sequencer.LoadSequence(sequence);
        }

        // ── Level 2 — Raw board command ───────────────────────────────────────────

        /// <summary>
        /// Software equivalent of the main CPU writing one byte to $CC02.
        /// Bypasses SoundSequencer entirely — no priority arbitration, no timing.
        /// Use for testing, diagnostics, and direct synthesis exploration only.
        /// </summary>
        public void SendSoundCommand(byte command)
        {
            _emulator.DispatchCommand(command);
        }

        // ── Persistent sounds (bypass sequencer) ─────────────────────────────────

        /// <summary>
        /// Starts or stops the player thrust noise (FDFLG=0 FNOISE loop).
        /// Bypasses the sequencer — called continuously while the player is thrusting.
        /// </summary>
        public void TriggerThrust(bool active) => _emulator.SetThrust(active);

        /// <summary>
        /// Starts or stops the background engine hum (BG1 FDFLG=0 loop).
        /// Equivalent to SendSoundCommand(BG1) / SendSoundCommand(BGEND).
        /// </summary>
        public void SetBackground(bool active) => _emulator.SetBg1(active);

        // ── Level 1a — Named game events ─────────────────────────────────────────
        // Sequences sourced from GameSoundSequences (extracted verbatim from DEFA7.SRC).

        /// <summary>Coin inserted. Priority $FF. ROM label: CNSND.</summary>
        public void TriggerCoinInsert()  => TriggerSequence(GameSoundSequences.CoinInsert);

        /// <summary>Player ship destroyed. Priority $F0. ROM label: PDSND.</summary>
        public void TriggerPlayerDeath() => TriggerSequence(GameSoundSequences.PlayerDeath);

        /// <summary>Laser fired. Priority $C0. ROM label: LASSND.</summary>
        public void TriggerLaserFire()   => TriggerSequence(GameSoundSequences.Laser);

        /// <summary>Smart bomb detonated. Priority $E8. ROM label: SBSND.</summary>
        public void TriggerSmartBomb()   => TriggerSequence(GameSoundSequences.SmartBomb);

        /// <summary>Hyperspace entered. Priority $C0.</summary>
        public void TriggerHyperspace()  => TriggerSequence(GameSoundSequences.Hyperspace);

        /// <summary>Free ship awarded. Priority $FF. ROM label: RPSND.</summary>
        public void TriggerFreeShip()    => TriggerSequence(GameSoundSequences.FreeShip);

        /// <summary>Terrain blow (mountain destroyed). Priority $E8. ROM label: TBSND.</summary>
        public void TriggerTerrainBlow()     => TriggerSequence(GameSoundSequences.TerrainBlow);

        /// <summary>Start 1-player game. Priority $F0. ROM label: ST1SND.</summary>
        public void TriggerStart1Player()    => TriggerSequence(GameSoundSequences.Start1Player);

        /// <summary>Start 2-player game. Priority $F0. ROM label: ST2SND.</summary>
        public void TriggerStart2Player()    => TriggerSequence(GameSoundSequences.Start2Player);

        /// <summary>Astronaut caught by player. Priority $E0. ROM label: ACSND.</summary>
        public void TriggerAstronautCatch()  => TriggerSequence(GameSoundSequences.AstronautCatch);

        /// <summary>Astronaut lands safely. Priority $E0. ROM label: ALSND.</summary>
        public void TriggerAstronautLand()   => TriggerSequence(GameSoundSequences.AstronautLand);

        /// <summary>Astronaut hit / killed. Priority $E0. ROM label: AHSND.</summary>
        public void TriggerAstronautHit()    => TriggerSequence(GameSoundSequences.AstronautHit);

        /// <summary>Astronaut screaming while being abducted. Priority $D8. ROM label: ASCSND.</summary>
        public void TriggerAstronautScream() => TriggerSequence(GameSoundSequences.AstronautScream);

        /// <summary>Enemy appears (Lander spawns). Priority $D0. ROM label: APSND.</summary>
        public void TriggerAppear()          => TriggerSequence(GameSoundSequences.Appear);

        /// <summary>Probe / Phred hit. Priority $D0. ROM label: PRHSND.</summary>
        public void TriggerProbeHit()        => TriggerSequence(GameSoundSequences.ProbeHit);

        /// <summary>Schitzo hit. Priority $D0. ROM label: SCHSND.</summary>
        public void TriggerSchitzHit()       => TriggerSequence(GameSoundSequences.SchitzHit);

        /// <summary>UFO / Bomber hit. Priority $D0. ROM label: UFHSND.</summary>
        public void TriggerUFOHit()          => TriggerSequence(GameSoundSequences.UFOHit);

        /// <summary>Tie Fighter hit. Priority $D0. ROM label: TIHSND.</summary>
        public void TriggerTieHit()          => TriggerSequence(GameSoundSequences.TieHit);

        /// <summary>Lander destroyed. Priority $D0. ROM label: LHSND.</summary>
        public void TriggerLanderDestroyed() => TriggerSequence(GameSoundSequences.LanderDestroyed);

        /// <summary>Lander picks up astronaut. Priority $D0. ROM label: LPKSND.</summary>
        public void TriggerLanderPickup()    => TriggerSequence(GameSoundSequences.LanderPickup);

        /// <summary>Lander suck (mutant conversion loop). Priority $C8. ROM label: LSKSND.</summary>
        public void TriggerLanderSuck()      => TriggerSequence(GameSoundSequences.LanderSuck);

        /// <summary>Swarmer hit. Priority $C0. ROM label: SWHSND.</summary>
        public void TriggerSwarmerHit()      => TriggerSequence(GameSoundSequences.SwarmerHit);

        /// <summary>Lander grabs astronaut. Priority $C0. ROM label: LGSND.</summary>
        public void TriggerLanderGrab()      => TriggerSequence(GameSoundSequences.LanderGrab);

        /// <summary>Lander fires at player. Priority $C0. ROM label: LSHSND.</summary>
        public void TriggerLanderShoot()     => TriggerSequence(GameSoundSequences.LanderShoot);

        /// <summary>Schitzo fires at player. Priority $C0. ROM label: SSHSND.</summary>
        public void TriggerSchitzoShoot()    => TriggerSequence(GameSoundSequences.SchitzoShoot);

        /// <summary>UFO / Bomber fires. Priority $C0. ROM label: USHSND.</summary>
        public void TriggerUFOShoot()        => TriggerSequence(GameSoundSequences.UFOShoot);

        /// <summary>Swarmer fires. Priority $C0. ROM label: SWSSND.</summary>
        public void TriggerSwarmerShoot()    => TriggerSequence(GameSoundSequences.SwarmerShoot);

        // ── P9 additions ─────────────────────────────────────────────────────

        /// <summary>Priority of the currently active sequence (0 when idle).</summary>
        public byte CurrentPriority    => _sequencer.CurrentPriority;

        /// <summary>True while a sound sequence is actively playing.</summary>
        public bool IsSequenceActive   => _sequencer.IsActive;

        /// <summary>Starts or stops the spinner (SP1 VARI preset — stub, params TBD).</summary>
        public void SetSpinner(bool active) => _emulator.SetSpinner(active);

        /// <summary>
        /// Fires the explosion sequence appropriate for the given enemy type.
        /// Selects among LanderDestroyed, ProbeHit, UFOHit, TieHit, SchitzHit, SwarmerHit.
        /// </summary>
        public void TriggerExplosion(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Lander:
                case EnemyType.Mutant:     TriggerLanderDestroyed(); break;
                case EnemyType.Probe:      TriggerProbeHit();        break;
                case EnemyType.UFO:        TriggerUFOHit();          break;
                case EnemyType.TieFighter: TriggerTieHit();          break;
                case EnemyType.Schitz:     TriggerSchitzHit();       break;
                case EnemyType.Swarmer:    TriggerSwarmerHit();      break;
            }
        }
    }

    // ── Enemy type enumeration ────────────────────────────────────────────────
    /// <summary>
    /// Enemy types used by <see cref="DefenderSoundBoard.TriggerExplosion"/>.
    /// Each value maps to the most appropriate destruction sound sequence.
    /// </summary>
    public enum EnemyType
    {
        Lander,      // LanderDestroyed — HBEV ($D0)
        Mutant,      // LanderDestroyed — same burst (lander that successfully abducted human)
        Probe,       // ProbeHit — BBSV ($D0)
        UFO,         // UFOHit — PROTV ($D0)
        TieFighter,  // TieHit — HBDV ($D0)
        Schitz,      // SchitzHit — RADIO ($D0)
        Swarmer,     // SwarmerHit — PROTV ($C0)
    }
}
