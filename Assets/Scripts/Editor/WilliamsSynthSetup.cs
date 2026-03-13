using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WilliamsSynth.Editor
{
    /// <summary>
    /// One-shot setup utility. Run WilliamsSynth > Setup Scene once to create
    /// the SoundBoard GameObject in SampleScene and wire up the required components.
    ///
    /// NOTE — Sample rate: since Unity 5.0 the output sample rate cannot be set from
    /// scripts. It must be configured in Edit > Project Settings > Audio > System Sample Rate.
    /// 44100 Hz is recommended for best fidelity (cyclesPerSample ≈ 20.293).
    /// Use WilliamsSynth > Audio Settings to jump directly to that panel.
    /// </summary>
    public static class WilliamsSynthSetup
    {
        private const string MenuPath       = "WilliamsSynth/Setup Scene";
        private const string AudioMenuPath  = "WilliamsSynth/Open Audio Project Settings";
        private const string GameObjectName = "SoundBoard";

        // ── Setup Scene ──────────────────────────────────────────────────────────

        [MenuItem(MenuPath)]
        public static void SetupScene()
        {
            // Warn if the GameObject already exists.
            var existing = GameObject.Find(GameObjectName);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "WilliamsSynth Setup",
                    $"A GameObject named '{GameObjectName}' already exists in the scene.\n" +
                    "Remove it first if you want to re-run setup.",
                    "OK");
                return;
            }

            // Create the SoundBoard GameObject.
            var go = new GameObject(GameObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create SoundBoard");

            // AudioSource is required by DefenderSoundBoard ([RequireComponent]).
            // Add it explicitly here so the setup log is informative.
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake  = true;
            audioSource.loop         = true;
            // Volume at 1.0; the synthesiser controls amplitude internally.
            audioSource.volume       = 1.0f;
            // Disable spatialisation — this is a 2D board-level audio source.
            audioSource.spatialBlend = 0f;

            // Read the current sample rate (read-only since Unity 5.0 — set it in
            // Edit > Project Settings > Audio > System Sample Rate, not from code).
            int   sr  = AudioSettings.outputSampleRate;
            double cps = 894886.0 / sr;

            Debug.Log($"[WilliamsSynth] AudioSettings.outputSampleRate = {sr} Hz  " +
                      $"(set via Edit > Project Settings > Audio > System Sample Rate)");
            Debug.Log($"[WilliamsSynth] cyclesPerSample = {cps:F4}  " +
                      $"(target: 44100 Hz → 20.2921)");
            Debug.Log("[WilliamsSynth] SoundBoard GameObject created. " +
                      "DefenderSoundBoard component will be added in P1.5.");

            // Mark the scene dirty so Unity prompts to save.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Select the new GameObject in the hierarchy.
            Selection.activeGameObject = go;

            string rateNote = sr == 44100
                ? "Sample rate is 44100 Hz — optimal."
                : $"Sample rate is {sr} Hz.\n" +
                  "For best fidelity set to 44100 Hz:\n" +
                  "Edit \u25ba Project Settings \u25ba Audio \u25ba System Sample Rate\n" +
                  "(or use WilliamsSynth \u25ba Open Audio Project Settings)";

            EditorUtility.DisplayDialog(
                "WilliamsSynth Setup",
                $"'{GameObjectName}' created in the active scene.\n\n" +
                $"outputSampleRate: {sr} Hz\n" +
                $"cyclesPerSample:  {cps:F4}\n\n" +
                rateNote + "\n\n" +
                "Save the scene (Ctrl/Cmd+S) to persist the change.",
                "OK");
        }

        [MenuItem(MenuPath, validate = true)]
        public static bool SetupSceneValidate() =>
            SceneManager.GetActiveScene().IsValid();

        // ── Open Audio Project Settings ───────────────────────────────────────────

        /// <summary>
        /// Opens Edit > Project Settings and navigates to the Audio panel where the
        /// System Sample Rate can be changed. Saves the user hunting through the menus.
        /// </summary>
        [MenuItem(AudioMenuPath)]
        public static void OpenAudioSettings() =>
            SettingsService.OpenProjectSettings("Project/Audio");
    }
}
