using System;

namespace WilliamsSynth
{
    /// <summary>
    /// Priority-arbitrated sound sequence player.
    ///
    /// Mirrors the DEFA7.SRC SNDLD / SNDSEQ interrupt logic:
    ///   SNDLD: stores incoming (priority, sequence) into a pending slot if higher priority.
    ///   SNDSEQ: drives the active sequence — fires commands, counts repeats and timers.
    ///
    /// ── Sequence lifecycle ────────────────────────────────────────────────────────
    ///   1. LoadSequence(seq): drop if seq.Priority &lt; current priority (lower = ignored).
    ///      Equal priority interrupts the running sequence (authentic DEFA7.SRC behaviour).
    ///   2. The first step's command fires immediately on load.
    ///   3. Tick(deltaTime) advances time. When the step timer expires:
    ///        – if more repeats remain: retrigger the command, reset the timer.
    ///        – if all repeats done: fire the next step's command immediately, reset timer.
    ///   4. When all steps are exhausted: CurrentPriority resets to 0 (any sequence can now load).
    ///
    /// ── Timer units ───────────────────────────────────────────────────────────────
    ///   SoundStep.TimerFrames × 0.016 f = duration in seconds per repeat interval.
    ///   1 TimerFrame ≈ one 60 Hz game frame ≈ 16 ms (matching SNDTMR in DEFA7.SRC).
    ///
    /// ── Thread safety ─────────────────────────────────────────────────────────────
    ///   LoadSequence and Tick are called on the Unity main thread.
    ///   The dispatch callback (SoundBoardEmulator.DispatchCommand) is also main-thread.
    ///   No audio-thread interaction; thread-safe by construction.
    /// </summary>
    public sealed class SoundSequencer
    {
        private readonly Action<byte> _dispatch;

        // ── Active sequence state ─────────────────────────────────────────────────
        private SoundSequence _current;
        private int   _stepIndex;
        private int   _repeatFired;  // times current step command has been fired (≥ 1)
        private float _timerAccum;   // seconds elapsed in the current repeat interval
        private bool  _active;

        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>The priority of the currently playing sequence (0 when idle).</summary>
        public byte CurrentPriority => _active ? _current.Priority : (byte)0;

        /// <summary>True while a sequence is running.</summary>
        public bool IsActive => _active;

        /// <param name="dispatchCommand">
        /// Callback invoked each time a step fires. Wired to SoundBoardEmulator.DispatchCommand.
        /// </param>
        public SoundSequencer(Action<byte> dispatchCommand)
        {
            _dispatch = dispatchCommand ?? throw new ArgumentNullException(nameof(dispatchCommand));
        }

        // ── LoadSequence ──────────────────────────────────────────────────────────
        /// <summary>
        /// Attempt to start a new sequence. Dropped silently if lower priority than the
        /// currently active sequence. Equal or higher priority interrupts immediately.
        /// </summary>
        public void LoadSequence(SoundSequence seq)
        {
            // Priority check: drop if strictly lower (equal priority can interrupt)
            if (_active && seq.Priority < _current.Priority)
                return;

            if (seq.Steps == null || seq.Steps.Length == 0)
            {
                _active = false;
                return;
            }

            _current     = seq;
            _stepIndex   = 0;
            _repeatFired = 1;   // first fire is about to happen
            _timerAccum  = 0f;
            _active      = true;

            FireCurrent();
        }

        // ── Tick ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Advance the sequencer by deltaTime seconds. Call once per frame from
        /// DefenderSoundBoard.Update() on the Unity main thread.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_active) return;

            _timerAccum += deltaTime;

            // Guard against zero-duration steps causing an infinite loop
            float threshold = System.Math.Max(0.001f,
                _current.Steps[_stepIndex].TimerFrames * 0.016f);

            while (_active && _timerAccum >= threshold)
            {
                _timerAccum -= threshold;

                if (_repeatFired < _current.Steps[_stepIndex].RepeatCount)
                {
                    // More repeats on this step — retrigger and stay on same step
                    _repeatFired++;
                    FireCurrent();
                    // threshold stays the same (same step, same TimerFrames)
                }
                else
                {
                    // All repeats exhausted — advance to the next step
                    _stepIndex++;
                    _repeatFired = 0;

                    if (_stepIndex >= _current.Steps.Length)
                    {
                        // Sequence complete — reset priority so anything can load next
                        _active = false;
                        break;
                    }

                    // Fire first instance of the new step immediately
                    _repeatFired = 1;
                    FireCurrent();
                    threshold = System.Math.Max(0.001f,
                        _current.Steps[_stepIndex].TimerFrames * 0.016f);
                }
            }
        }

        // ── Reset ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Immediately clears the active sequence and resets priority to 0 (idle).
        /// Any sequence can load after a Reset, regardless of what was playing.
        /// Used by DefenderSoundBoard.StopAll() for test-UI force-stop.
        /// </summary>
        public void Reset()
        {
            _active     = false;
            _timerAccum = 0f;
            _stepIndex  = 0;
            _repeatFired = 0;
        }

        // ─────────────────────────────────────────────────────────────────────────
        private void FireCurrent()
        {
            _dispatch(_current.Steps[_stepIndex].CommandId);
        }
    }
}
