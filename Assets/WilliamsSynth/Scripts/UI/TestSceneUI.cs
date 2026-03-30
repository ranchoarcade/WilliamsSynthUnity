using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WilliamsSynth
{
    /// <summary>
    /// Runtime test-scene UI for the Williams Defender Sound Board emulator.
    ///
    /// Attach to any GameObject in the scene alongside a DefenderSoundBoard.
    /// On Awake() it programmatically creates a full-screen Canvas with:
    ///
    ///   • Scrollable button grid — one button per game event, grouped by priority.
    ///   • Persistent-sound toggles — Thrust, BG1, Spinner ON/OFF.
    ///   • Raw command input — decimal or 0x… hex, validates 0–31.
    ///   • Sequence builder — custom priority + up to 4 steps + Trigger button.
    ///   • Live priority display — polls CurrentPriority every frame.
    ///
    /// Requires UnityEngine.UI (built-in) — no TextMeshPro or other packages needed.
    /// Canvas scaler reference resolution: 1920×1080, scale with screen size (0.5 match).
    /// </summary>
    [AddComponentMenu("WilliamsSynth/Test Scene UI")]
    public class TestSceneUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Tooltip("The DefenderSoundBoard to drive. Auto-found if null.")]
        [SerializeField] private DefenderSoundBoard _board;

        // ── Live display ──────────────────────────────────────────────────────────
        private Text _priorityText;
        private Text _soundInfoText;

        // ── Raw-command input ─────────────────────────────────────────────────────
        private InputField _rawCmdField;

        // ── Sequence-builder inputs ───────────────────────────────────────────────
        private InputField   _seqPriorityField;
        private InputField[] _seqRepeat = new InputField[4];
        private InputField[] _seqTimer  = new InputField[4];
        private InputField[] _seqCmd    = new InputField[4];

        // ── Sound tuning inputs ───────────────────────────────────────────────────
        private InputField   _tuneGWaveCmdFld;
        private InputField[] _tuneGWaveP   = new InputField[9];  // echo,cyc,decay,wave,prdeca,freqInc,deltaCnt,freqLen,freqOffset
        private Text         _tuneGWaveStat;
        private InputField   _tuneVariCmdFld;
        private InputField[] _tuneVariP    = new InputField[9];  // loPer,hiPer,loDt,hiDt,hiEn,swpDtH,swpDtL,loMod,vamp
        private Text         _tuneVariStat;
        private Text         _tuneSavePath;

        // ── Shared font ───────────────────────────────────────────────────────────
        private Font _font;

        // ── Style constants ───────────────────────────────────────────────────────
        private const int   FontSz    = 13;
        private const float BtnH      = 34f;
        private const float BtnW      = 148f;
        private const float SmBtnW    = 100f;
        private const float RowH      = BtnH + 4f;

        private static readonly Color ColBg       = new Color(0.08f, 0.08f, 0.10f);
        private static readonly Color ColHeader   = new Color(0.13f, 0.13f, 0.17f);
        private static readonly Color ColSection  = new Color(0.11f, 0.14f, 0.22f);
        private static readonly Color ColSeqHi    = new Color(0.40f, 0.12f, 0.12f);  // $FF/$F0
        private static readonly Color ColSeqMed   = new Color(0.18f, 0.26f, 0.14f);  // $E8–$D0
        private static readonly Color ColSeqLo    = new Color(0.14f, 0.20f, 0.30f);  // $C8–$C0
        private static readonly Color ColUtil     = new Color(0.22f, 0.22f, 0.36f);
        private static readonly Color ColRaw      = new Color(0.36f, 0.22f, 0.10f);
        private static readonly Color ColBuilder  = new Color(0.24f, 0.16f, 0.36f);
        private static readonly Color ColInput    = new Color(0.18f, 0.18f, 0.24f);
        private static readonly Color ColTextMain = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color ColTextDim  = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ColPriority = new Color(0.75f, 0.92f, 0.55f);

        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_board == null)
                _board = FindObjectOfType<DefenderSoundBoard>();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            EnsureEventSystem();
            BuildUI();
        }

        private void Update()
        {
            if (_priorityText == null || _board == null) return;
            byte pri = _board.CurrentPriority;
            _priorityText.text = pri == 0
                ? "Priority: idle"
                : $"Priority: 0x{pri:X2}";
        }

        // ── Event system ──────────────────────────────────────────────────────────
        // The new Input System does NOT auto-attach InputSystemUIInputModule to a
        // programmatically created EventSystem (only the Editor menu does that).
        // We must add it explicitly; without it the EventSystem generates no pointer
        // events and buttons are unresponsive.
        private static void EnsureEventSystem()
        {
            var es = FindObjectOfType<EventSystem>();
            if (es != null)
            {
                // Scene already has an EventSystem — make sure it has an input module.
                if (es.GetComponent<BaseInputModule>() == null)
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ── Canvas + layout construction ──────────────────────────────────────────
        private void BuildUI()
        {
            // ── Canvas ────────────────────────────────────────────────────────────
            var canvasGO = new GameObject("TestSceneCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen background
            var bg = NewPanel("BG", canvasGO, ColBg);
            StretchFill(bg);

            // ── Header bar (60 px, top-anchored) ─────────────────────────────────
            var headerGO = NewPanel("Header", canvasGO, ColHeader);
            var headerRT = headerGO.GetComponent<RectTransform>();
            headerRT.anchorMin       = new Vector2(0, 1);
            headerRT.anchorMax       = new Vector2(1, 1);
            headerRT.pivot           = new Vector2(0.5f, 1);
            headerRT.sizeDelta       = new Vector2(0, 60);
            headerRT.anchoredPosition = Vector2.zero;

            var hHLG = headerGO.AddComponent<HorizontalLayoutGroup>();
            hHLG.padding             = new RectOffset(16, 16, 0, 0);
            hHLG.spacing             = 14;
            hHLG.childAlignment      = TextAnchor.MiddleLeft;
            hHLG.childForceExpandWidth  = false;
            hHLG.childForceExpandHeight = true;

            var titleTxt = MakeText("Williams Defender — Sound Board", headerGO, 18, ColTextMain);
            titleTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            _priorityText = MakeText("Priority: idle", headerGO, 16, ColPriority);
            _priorityText.gameObject.AddComponent<LayoutElement>().preferredWidth = 230;

            MakeButton("Stop All", headerGO, ColRaw, 120, () => _board?.StopAll());

            // ── Scroll view (fills below header) ─────────────────────────────────
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0,  0);
            scrollRT.offsetMax = new Vector2(0, -60);

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;
            scrollRect.scrollSensitivity = 30f;

            // Viewport
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            // RectMask2D clips by rect bounds — no stencil buffer needed.
            // (Mask + Color.clear Image silently breaks: the transparent Image is culled
            //  so nothing ever writes to the stencil, making all children invisible.)
            vpGO.AddComponent<RectMask2D>();

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin       = new Vector2(0, 1);
            contentRT.anchorMax       = new Vector2(1, 1);
            contentRT.pivot           = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta        = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding             = new RectOffset(12, 12, 10, 16);
            vlg.spacing             = 3;
            vlg.childAlignment      = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            contentGO.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content  = contentRT;
            scrollRect.viewport = vpRT;

            // ── Populate content ──────────────────────────────────────────────────
            BuildSequenceSection(contentGO);
            BuildSoundInfoSection(contentGO);
            BuildPersistentSection(contentGO);
            BuildRawCommandSection(contentGO);
            BuildSequenceBuilderSection(contentGO);
            BuildTuningSection(contentGO);

            // Force an immediate layout pass so ContentSizeFitter computes the correct
            // content height before the first frame renders.
            Canvas.ForceUpdateCanvases();
        }

        // ── Section: Sequences ────────────────────────────────────────────────────
        // Button labels: "English Name\nDEFA7LABEL/CMD" — second line is the original
        // assembly label from DEFA7.SRC (sequence) and VSNDRM1.SRC (sound command).
        // Each callback calls ShowCommandInfo so the SOUND INFO panel updates on click.
        private void BuildSequenceSection(GameObject c)
        {
            SectionHeader(c, "SEQUENCES");

            ButtonRow(c, "$FF", ColSeqHi, new (string, Action)[]
            {
                ("Coin Insert\nCNSND/SCREAM",  () => { ShowCommandInfo(SoundCommand.SCREAM); _board?.TriggerCoinInsert(); }),
                ("Free Ship\nRPSND/QUASAR",    () => { ShowCommandInfo(SoundCommand.QUASAR); _board?.TriggerFreeShip(); }),
            });
            ButtonRow(c, "$F0", ColSeqHi, new (string, Action)[]
            {
                ("Player Death\nPDSND/BONV+RADIO",  () => { ShowCommandInfo(SoundCommand.BONV, SoundCommand.RADIO); _board?.TriggerPlayerDeath(); }),
                ("Start 1P\nST1SND/SV3",            () => { ShowCommandInfo(SoundCommand.SV3); _board?.TriggerStart1Player(); }),
                ("Start 2P\nST2SND/ED10",           () => { ShowCommandInfo(SoundCommand.ED10); _board?.TriggerStart2Player(); }),
            });
            ButtonRow(c, "$E8", ColSeqMed, new (string, Action)[]
            {
                ("Smart Bomb\nSBSND/BONV+RADIO",          () => { ShowCommandInfo(SoundCommand.BONV, SoundCommand.RADIO); _board?.TriggerSmartBomb(); }),
                ("Terrain Blow\nTBSND/APPEAR+BONV+RADIO", () => { ShowCommandInfo(SoundCommand.APPEAR, SoundCommand.BONV, SoundCommand.RADIO); _board?.TriggerTerrainBlow(); }),
            });
            ButtonRow(c, "$E0", ColSeqMed, new (string, Action)[]
            {
                ("Astro Catch\nACSND/SPNRV",   () => { ShowCommandInfo(SoundCommand.SPNRV); _board?.TriggerAstronautCatch(); }),
                ("Astro Land\nALSND/CABSHK",   () => { ShowCommandInfo(SoundCommand.CABSHK); _board?.TriggerAstronautLand(); }),
                ("Astro Hit\nAHSND/BONV",      () => { ShowCommandInfo(SoundCommand.BONV); _board?.TriggerAstronautHit(); }),
            });
            ButtonRow(c, "$D8", ColSeqMed, new (string, Action)[]
            {
                ("Astro Scream\nASCSND/ORGANT",  () => { ShowCommandInfo(SoundCommand.ORGANT); _board?.TriggerAstronautScream(); }),
            });
            ButtonRow(c, "$D0", ColSeqMed, new (string, Action)[]
            {
                ("Appear\nAPSND/THRUST",         () => { ShowCommandInfo(SoundCommand.THRUST); _board?.TriggerAppear(); }),
                ("Probe Hit\nPRHSND/BBSV",       () => { ShowCommandInfo(SoundCommand.BBSV); _board?.TriggerProbeHit(); }),
                ("Schitz Hit\nSCHSND/RADIO",     () => { ShowCommandInfo(SoundCommand.RADIO); _board?.TriggerSchitzHit(); }),
                ("UFO Hit\nUFHSND/PROTV",        () => { ShowCommandInfo(SoundCommand.PROTV); _board?.TriggerUFOHit(); }),
                ("Tie Hit\nTIHSND/HBDV",         () => { ShowCommandInfo(SoundCommand.HBDV); _board?.TriggerTieHit(); }),
                ("Lander Dead\nLHSND/HBEV",      () => { ShowCommandInfo(SoundCommand.HBEV); _board?.TriggerLanderDestroyed(); }),
                ("Lander Pickup\nLPKSND/ED10",   () => { ShowCommandInfo(SoundCommand.ED10); _board?.TriggerLanderPickup(); }),
            });
            ButtonRow(c, "$C8", ColSeqLo, new (string, Action)[]
            {
                ("Lander Suck\nLSKSND/BG1",   () => { ShowCommandInfo(SoundCommand.BG1); _board?.TriggerLanderSuck(); }),
            });
            ButtonRow(c, "$C0", ColSeqLo, new (string, Action)[]
            {
                ("Swarmer Hit\nSWHSND/PROTV",     () => { ShowCommandInfo(SoundCommand.PROTV); _board?.TriggerSwarmerHit(); }),
                ("Laser\nLASSND/LASER",           () => { ShowCommandInfo(SoundCommand.LASER); _board?.TriggerLaserFire(); }),
                ("Hyperspace\n(direct)/HYPER",     () => { ShowCommandInfo(SoundCommand.HYPER); _board?.TriggerHyperspace(); }),
                ("Lander Grab\nLGSND/HYPER",       () => { ShowCommandInfo(SoundCommand.HYPER); _board?.TriggerLanderGrab(); }),
                ("Lander Shoot\nLSHSND/DP1V",      () => { ShowCommandInfo(SoundCommand.DP1V); _board?.TriggerLanderShoot(); }),
                ("Schitzo Shoot\nSSHSND/CLDWNV",   () => { ShowCommandInfo(SoundCommand.CLDWNV); _board?.TriggerSchitzoShoot(); }),
                ("UFO Shoot\nUSHSND/DP1V",         () => { ShowCommandInfo(SoundCommand.DP1V); _board?.TriggerUFOShoot(); }),
                ("Swarmer Shoot\nSWSSND/ED12",     () => { ShowCommandInfo(SoundCommand.ED12); _board?.TriggerSwarmerShoot(); }),
            });
        }

        // ── Section: Sound info ───────────────────────────────────────────────────
        private void BuildSoundInfoSection(GameObject c)
        {
            SectionHeader(c, "SOUND INFO  (updates when a sequence button is pressed)");

            var panel = NewPanel("SoundInfoPanel", c, new Color(0.06f, 0.10f, 0.08f));
            panel.AddComponent<LayoutElement>().preferredHeight = 82;

            var txtGO = new GameObject("SoundInfoText");
            txtGO.transform.SetParent(panel.transform, false);
            var tRT = txtGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(10, 4);
            tRT.offsetMax = new Vector2(-10, -4);
            _soundInfoText = txtGO.AddComponent<Text>();
            _soundInfoText.text               = "(press a sequence button to see generator type and ROM parameters)";
            _soundInfoText.font               = _font;
            _soundInfoText.fontSize           = FontSz - 2;
            _soundInfoText.color              = ColTextDim;
            _soundInfoText.alignment          = TextAnchor.UpperLeft;
            _soundInfoText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _soundInfoText.verticalOverflow   = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// Formats generator type + ROM parameter table entries for one or more command IDs
        /// and writes them into the SOUND INFO panel (one line per command).
        /// </summary>
        private void ShowCommandInfo(params byte[] cmdIds)
        {
            if (_soundInfoText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (byte cmdId in cmdIds)
            {
                string lbl     = SoundCommand.GetLabel(cmdId);
                string genType = GetGeneratorType(cmdId);
                sb.Append($"${cmdId:X2} {lbl,-6}  [{genType,-7}]  ");

                bool isGWave = cmdId < SoundParameterTables.GWaveParams.Length
                            && SoundParameterTables.GWaveParams[cmdId][5] > 0;

                if (isGWave)
                {
                    byte[] p     = SoundParameterTables.GWaveParams[cmdId];
                    int echo     = (p[0] >> 4) & 0xF;
                    int cyc      = p[0] & 0xF;
                    int decay    = (p[1] >> 4) & 0xF;
                    int wave     = p[1] & 0xF;
                    int prdeca   = p[2];
                    int freqInc  = (sbyte)p[3];
                    int deltaCnt = p[4];
                    int freqLen  = p[5];
                    string tbl   = GetFreqTableName(p[6]);
                    sb.Append($"freq: {tbl} ({freqLen} bytes)  freqInc={freqInc:+#;-#;0}  deltaCnt={deltaCnt}  echo={echo} cyc={cyc}  decay={decay} wave={wave}  prdeca={prdeca}");
                }
                else if (cmdId >= SoundCommand.SAW && cmdId <= SoundCommand.CABSHK)
                {
                    byte[] p = VariParameterTables.VariPresets[cmdId - SoundCommand.SAW];
                    sb.Append($"loPer=${p[0]:X2} hiPer=${p[1]:X2} loDt=${p[2]:X2} hiDt=${p[3]:X2} hiEn=${p[4]:X2} swpDt=${p[5]:X2}{p[6]:X2} loMod=${p[7]:X2} amp=${p[8]:X2}");
                }
                else if (cmdId == SoundCommand.ORGANT)
                    sb.Append("34 notes  (Bach Toccata — TACC=4)");
                else if (cmdId == SoundCommand.ORGANN)
                    sb.Append("3 notes  (Phantom — PHANC=3)");
                else
                    sb.Append("(generator-internal parameters)");

                sb.AppendLine();
            }
            _soundInfoText.color = ColTextMain;
            _soundInfoText.text  = sb.ToString().TrimEnd();
        }

        private static string GetGeneratorType(byte cmdId)
        {
            if (cmdId < SoundParameterTables.GWaveParams.Length
             && SoundParameterTables.GWaveParams[cmdId][5] > 0)
                return "GWAVE";
            if (cmdId >= SoundCommand.SAW && cmdId <= SoundCommand.CABSHK)
                return "VARI";
            return cmdId switch
            {
                SoundCommand.LASER   => "NOISE",
                SoundCommand.APPEAR  => "FNOISE",
                SoundCommand.THRUST  => "FNOISE",
                SoundCommand.CANNON  => "FNOISE",
                SoundCommand.RADIO   => "RADIO",
                SoundCommand.HYPER   => "HYPER",
                SoundCommand.SCREAM  => "SCREAM",
                SoundCommand.ORGANT  => "ORGAN",
                SoundCommand.ORGANN  => "ORGAN",
                SoundCommand.BG1     => "BG/loop",
                SoundCommand.BG2INC  => "BG/loop",
                SoundCommand.BGEND   => "(stop)",
                SoundCommand.Silence => "(silence)",
                _                    => "(?)"
            };
        }

        private static string GetFreqTableName(int offset) => offset switch
        {
            FrequencyTables.BONSND => "BONSND",
            FrequencyTables.HBTSND => "HBTSND",
            FrequencyTables.SPNSND => "SPNSND",
            FrequencyTables.TRBPAT => "TRBPAT",
            FrequencyTables.HBDSND => "HBDSND",
            FrequencyTables.SWPAT  => "SWPAT/BBSND",   // SWPAT == BBSND == 71
            FrequencyTables.HBESND => "HBESND",
            FrequencyTables.SPNR   => "SPNR",
            FrequencyTables.COOLDN => "COOLDN",
            FrequencyTables.STDSND => "STDSND",
            FrequencyTables.ED10FP => "ED10FP",
            FrequencyTables.ED13FP => "ED13FP",
            _                      => $"@{offset}"
        };

        // ── Section: Sound tuning ────────────────────────────────────────────────
        // Live-edit GWAVE (7-byte) and VARI (9-byte) parameter tables in place.
        // Changes take effect on the next Trigger() call — no restart required.
        // Save / Load persists patches to Application.persistentDataPath.
        private void BuildTuningSection(GameObject c)
        {
            SectionHeader(c, "SOUND TUNING  (see Docs/SoundTuningReference.md for field descriptions)");

            // ── GWAVE ────────────────────────────────────────────────────────────
            var gHdr = HRow("TGW_H", c, RowH);
            MakeText("GWAVE", gHdr, FontSz, new Color(0.55f, 0.90f, 0.65f))
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 58;
            MakeText("Cmd:", gHdr, FontSz - 1, ColTextDim)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 30;
            _tuneGWaveCmdFld = InputFld("GWCmd", gHdr, 64, "0x07");
            MakeButton("Load",        gHdr, ColUtil, 58,  OnLoadGWave);
            MakeButton("Reset→ROM",   gHdr, ColRaw,  90,  OnResetGWave);
            _tuneGWaveStat = MakeText("ROM", gHdr, FontSz - 1, ColTextDim);
            _tuneGWaveStat.gameObject.AddComponent<LayoutElement>().preferredWidth = 72;

            // 9 logical fields: echo | cyc | decay | wave | prdeca | freqInc | deltaCnt | freqLen | freqOffset
            string[] gwLbls = { "echo",  "cyc",   "decay", "wave(0-6)", "prdeca", "freqInc", "Δcnt",  "freqLen", "freqOff" };
            float[]  gwW    = {  34f,     28f,     38f,     66f,         46f,      52f,       34f,     52f,       52f };
            var gPRow = HRow("TGW_P", c, RowH);
            for (int i = 0; i < 9; i++)
            {
                MakeText(gwLbls[i], gPRow, FontSz - 2, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = gwW[i];
                _tuneGWaveP[i] = InputFld($"GW{i}", gPRow, 50, "");
            }

            var gAct = HRow("TGW_A", c, RowH);
            MakeButton("Apply & Test", gAct, ColSeqMed, 112, () => OnApplyGWave(true));
            MakeButton("Apply Only",   gAct, ColUtil,    90, () => OnApplyGWave(false));
            MakeButton("Copy C#",      gAct, ColSection,  76, OnCopyGWaveCS);
            MakeText("  freqOff: 0=BONSND 13=HBTSND 27=SPNSND 40=TRBPAT 49=HBDSND 71=SWPAT 91=HBESND 105=SPNR 106=COOLDN 109=STDSND 148=ED10FP 154=ED13FP",
                     gAct, FontSz - 3, ColTextDim);

            // ── VARI ─────────────────────────────────────────────────────────────
            var vHdr = HRow("TVR_H", c, RowH);
            MakeText("VARI", vHdr, FontSz, new Color(0.60f, 0.75f, 0.95f))
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 58;
            MakeText("Cmd:", vHdr, FontSz - 1, ColTextDim)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 30;
            _tuneVariCmdFld = InputFld("VRCmd", vHdr, 64, "0x1E");
            MakeButton("Load",        vHdr, ColUtil, 58,  OnLoadVari);
            MakeButton("Reset→ROM",   vHdr, ColRaw,  90,  OnResetVari);
            _tuneVariStat = MakeText("ROM", vHdr, FontSz - 1, ColTextDim);
            _tuneVariStat.gameObject.AddComponent<LayoutElement>().preferredWidth = 72;

            // 9 fields — loDt/hiDt/loMod are signed; swpDt is 16-bit split into H+L bytes
            string[] vrLbls = { "loPer", "hiPer", "loDt",  "hiDt",  "hiEn",  "swpH",  "swpL",  "loMod", "vamp" };
            float[]  vrW    = {  40f,     40f,     32f,     32f,     36f,     36f,     36f,     42f,     34f };
            var vPRow = HRow("TVR_P", c, RowH);
            for (int i = 0; i < 9; i++)
            {
                MakeText(vrLbls[i], vPRow, FontSz - 2, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = vrW[i];
                _tuneVariP[i] = InputFld($"VR{i}", vPRow, 50, "");
            }

            var vAct = HRow("TVR_A", c, RowH);
            MakeButton("Apply & Test", vAct, ColSeqMed, 112, () => OnApplyVari(true));
            MakeButton("Apply Only",   vAct, ColUtil,    90, () => OnApplyVari(false));
            MakeButton("Copy C#",      vAct, ColSection,  76, OnCopyVariCS);
            MakeText("  loDt/hiDt/loMod signed (−128–127)  |  swpDt 16-bit = swpH×256+swpL  |  $1C=SAW  $1D=FOSHIT  $1E=QUASAR  $1F=CABSHK",
                     vAct, FontSz - 3, ColTextDim);

            // ── Save / Load ───────────────────────────────────────────────────────
            var slRow = HRow("TSL", c, RowH);
            MakeButton("Save Patches", slRow, ColRaw,  110, OnSavePatches);
            MakeButton("Load Patches", slRow, ColUtil, 110, OnLoadPatches);
            _tuneSavePath = MakeText("  " + SoundPatchStore.DefaultPath, slRow, FontSz - 3, ColTextDim);
        }

        // ── Tuning handlers ───────────────────────────────────────────────────────

        private void OnLoadGWave()
        {
            if (!TryParseTuneCmd(_tuneGWaveCmdFld, out byte cmd)) return;
            if (cmd >= SoundParameterTables.GWaveParams.Length) return;
            byte[] p = SoundParameterTables.GWaveParams[cmd];
            _tuneGWaveP[0].text = ((p[0] >> 4) & 0xF).ToString();   // echo
            _tuneGWaveP[1].text = (p[0] & 0xF).ToString();           // cyc
            _tuneGWaveP[2].text = ((p[1] >> 4) & 0xF).ToString();    // decay
            _tuneGWaveP[3].text = (p[1] & 0xF).ToString();           // wave
            _tuneGWaveP[4].text = p[2].ToString();                    // prdeca
            _tuneGWaveP[5].text = ((sbyte)p[3]).ToString();           // freqInc (signed)
            _tuneGWaveP[6].text = p[4].ToString();                    // deltaCnt
            _tuneGWaveP[7].text = p[5].ToString();                    // freqLen
            _tuneGWaveP[8].text = p[6].ToString();                    // freqOffset
            UpdateGWaveStat(cmd);
        }

        private void OnApplyGWave(bool test)
        {
            if (!TryParseTuneCmd(_tuneGWaveCmdFld, out byte cmd)) return;
            if (!TryBuildGWavePatch(out byte[] patch))
            {
                Debug.LogWarning("[TuneUI] Invalid GWAVE params — check ranges.");
                return;
            }
            SoundPatchStore.ApplyGWave(cmd, patch);
            UpdateGWaveStat(cmd);
            ShowCommandInfo(cmd);
            if (test) { _board?.StopAll(); _board?.SendSoundCommand(cmd); }
        }

        private void OnResetGWave()
        {
            if (!TryParseTuneCmd(_tuneGWaveCmdFld, out byte cmd)) return;
            SoundPatchStore.ResetGWave(cmd);
            OnLoadGWave();   // reload fields from the now-restored table
        }

        private void OnCopyGWaveCS()
        {
            if (!TryParseTuneCmd(_tuneGWaveCmdFld, out byte cmd)) return;
            GUIUtility.systemCopyBuffer = SoundPatchStore.ToCSSnippet(cmd, isGWave: true);
            Debug.Log($"[TuneUI] Copied GWAVE C# snippet for 0x{cmd:X2} to clipboard.");
        }

        private void OnLoadVari()
        {
            if (!TryParseTuneCmd(_tuneVariCmdFld, out byte cmd)) return;
            int idx = cmd - SoundCommand.SAW;
            if (idx < 0 || idx >= VariParameterTables.VariPresets.Length) return;
            byte[] p = VariParameterTables.VariPresets[idx];
            _tuneVariP[0].text = p[0].ToString();              // loPer
            _tuneVariP[1].text = p[1].ToString();              // hiPer
            _tuneVariP[2].text = ((sbyte)p[2]).ToString();     // loDt (signed)
            _tuneVariP[3].text = ((sbyte)p[3]).ToString();     // hiDt (signed)
            _tuneVariP[4].text = p[4].ToString();              // hiEn
            _tuneVariP[5].text = p[5].ToString();              // swpDtH
            _tuneVariP[6].text = p[6].ToString();              // swpDtL
            _tuneVariP[7].text = ((sbyte)p[7]).ToString();     // loMod (signed)
            _tuneVariP[8].text = p[8].ToString();              // vamp
            UpdateVariStat(cmd);
        }

        private void OnApplyVari(bool test)
        {
            if (!TryParseTuneCmd(_tuneVariCmdFld, out byte cmd)) return;
            if (!TryBuildVariPatch(out byte[] patch))
            {
                Debug.LogWarning("[TuneUI] Invalid VARI params — check ranges.");
                return;
            }
            SoundPatchStore.ApplyVari(cmd, patch);
            UpdateVariStat(cmd);
            ShowCommandInfo(cmd);
            if (test) { _board?.StopAll(); _board?.SendSoundCommand(cmd); }
        }

        private void OnResetVari()
        {
            if (!TryParseTuneCmd(_tuneVariCmdFld, out byte cmd)) return;
            SoundPatchStore.ResetVari(cmd);
            OnLoadVari();
        }

        private void OnCopyVariCS()
        {
            if (!TryParseTuneCmd(_tuneVariCmdFld, out byte cmd)) return;
            GUIUtility.systemCopyBuffer = SoundPatchStore.ToCSSnippet(cmd, isGWave: false);
            Debug.Log($"[TuneUI] Copied VARI C# snippet for 0x{cmd:X2} to clipboard.");
        }

        private void OnSavePatches()
        {
            try
            {
                SoundPatchStore.SavePatches(SoundPatchStore.DefaultPath);
                string msg = "Saved → " + SoundPatchStore.DefaultPath;
                Debug.Log("[TuneUI] " + msg);
                if (_tuneSavePath != null) _tuneSavePath.text = "  " + msg;
            }
            catch (Exception ex) { Debug.LogError("[TuneUI] Save failed: " + ex.Message); }
        }

        private void OnLoadPatches()
        {
            try
            {
                int n = SoundPatchStore.LoadPatches(SoundPatchStore.DefaultPath);
                if (n < 0)
                {
                    string missing = "No patch file at " + SoundPatchStore.DefaultPath;
                    Debug.LogWarning("[TuneUI] " + missing);
                    if (_tuneSavePath != null) _tuneSavePath.text = "  " + missing;
                    return;
                }
                string msg = $"Loaded {n} patch(es) ← {SoundPatchStore.DefaultPath}";
                Debug.Log("[TuneUI] " + msg);
                if (_tuneSavePath != null) _tuneSavePath.text = "  " + msg;
            }
            catch (Exception ex) { Debug.LogError("[TuneUI] Load failed: " + ex.Message); }
        }

        // ── Tuning helpers ────────────────────────────────────────────────────────

        private static bool TryParseTuneCmd(InputField field, out byte cmd)
        {
            cmd = 0;
            if (field == null) return false;
            string s = field.text.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.TryParse(s.Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out cmd);
            return byte.TryParse(s, out cmd);
        }

        private void UpdateGWaveStat(byte cmd)
        {
            if (_tuneGWaveStat == null) return;
            bool patched = SoundPatchStore.IsGWavePatched(cmd);
            _tuneGWaveStat.text  = patched ? "PATCHED" : "ROM";
            _tuneGWaveStat.color = patched ? new Color(0.40f, 0.92f, 0.40f) : ColTextDim;
        }

        private void UpdateVariStat(byte cmd)
        {
            if (_tuneVariStat == null) return;
            bool patched = SoundPatchStore.IsVariPatched(cmd);
            _tuneVariStat.text  = patched ? "PATCHED" : "ROM";
            _tuneVariStat.color = patched ? new Color(0.40f, 0.92f, 0.40f) : ColTextDim;
        }

        /// <summary>
        /// Reads the 9 GWAVE UI fields and packs them into the 7-byte wire format.
        /// echo+cyc → byte[0] nibbles; decay+wave → byte[1] nibbles; freqInc is signed.
        /// </summary>
        private bool TryBuildGWavePatch(out byte[] patch)
        {
            patch = null;
            try
            {
                int echo     = int.Parse(_tuneGWaveP[0].text.Trim());
                int cyc      = int.Parse(_tuneGWaveP[1].text.Trim());
                int decay    = int.Parse(_tuneGWaveP[2].text.Trim());
                int wave     = int.Parse(_tuneGWaveP[3].text.Trim());
                int prdeca   = int.Parse(_tuneGWaveP[4].text.Trim());
                int freqInc  = int.Parse(_tuneGWaveP[5].text.Trim());
                int deltaCnt = int.Parse(_tuneGWaveP[6].text.Trim());
                int freqLen  = int.Parse(_tuneGWaveP[7].text.Trim());
                int freqOff  = int.Parse(_tuneGWaveP[8].text.Trim());

                if ((uint)echo > 15 || (uint)cyc > 15)         return false;
                if ((uint)decay > 15 || (uint)wave > 6)         return false;
                if ((uint)prdeca > 255)                          return false;
                if (freqInc < -128 || freqInc > 127)             return false;
                if ((uint)deltaCnt > 255)                        return false;
                if ((uint)freqLen > 163 || (uint)freqOff > 162) return false;

                patch    = new byte[7];
                patch[0] = (byte)((echo << 4) | (cyc & 0xF));
                patch[1] = (byte)((decay << 4) | (wave & 0xF));
                patch[2] = (byte)prdeca;
                patch[3] = unchecked((byte)(sbyte)freqInc);
                patch[4] = (byte)deltaCnt;
                patch[5] = (byte)freqLen;
                patch[6] = (byte)freqOff;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Reads the 9 VARI UI fields into the 9-byte wire format.
        /// loDt, hiDt, loMod are signed (−128–127); all others unsigned (0–255).
        /// </summary>
        private bool TryBuildVariPatch(out byte[] patch)
        {
            patch = null;
            try
            {
                int loPer = int.Parse(_tuneVariP[0].text.Trim());
                int hiPer = int.Parse(_tuneVariP[1].text.Trim());
                int loDt  = int.Parse(_tuneVariP[2].text.Trim());
                int hiDt  = int.Parse(_tuneVariP[3].text.Trim());
                int hiEn  = int.Parse(_tuneVariP[4].text.Trim());
                int swpH  = int.Parse(_tuneVariP[5].text.Trim());
                int swpL  = int.Parse(_tuneVariP[6].text.Trim());
                int loMod = int.Parse(_tuneVariP[7].text.Trim());
                int vamp  = int.Parse(_tuneVariP[8].text.Trim());

                if ((uint)loPer > 255 || (uint)hiPer > 255) return false;
                if (loDt < -128 || loDt > 127)               return false;
                if (hiDt < -128 || hiDt > 127)               return false;
                if ((uint)hiEn > 255)                         return false;
                if ((uint)swpH > 255 || (uint)swpL > 255)   return false;
                if (loMod < -128 || loMod > 127)              return false;
                if ((uint)vamp > 255)                         return false;

                patch    = new byte[9];
                patch[0] = (byte)loPer;
                patch[1] = (byte)hiPer;
                patch[2] = unchecked((byte)(sbyte)loDt);
                patch[3] = unchecked((byte)(sbyte)hiDt);
                patch[4] = (byte)hiEn;
                patch[5] = (byte)swpH;
                patch[6] = (byte)swpL;
                patch[7] = unchecked((byte)(sbyte)loMod);
                patch[8] = (byte)vamp;
                return true;
            }
            catch { return false; }
        }

        // ── Section: Persistent sounds ────────────────────────────────────────────
        private void BuildPersistentSection(GameObject c)
        {
            SectionHeader(c, "PERSISTENT SOUNDS");

            ButtonRow(c, "Loop", ColUtil, new (string, Action)[]
            {
                ("Thrust ON",   () => _board?.TriggerThrust(true)),
                ("Thrust OFF",  () => _board?.TriggerThrust(false)),
                ("BG1 ON",      () => _board?.SetBackground(true)),
                ("BG1 OFF",     () => _board?.SetBackground(false)),
                ("Spinner ON",  () => _board?.SetSpinner(true)),
                ("Spinner OFF", () => _board?.SetSpinner(false)),
            });
        }

        // ── Section: Raw command ──────────────────────────────────────────────────
        private void BuildRawCommandSection(GameObject c)
        {
            SectionHeader(c, "RAW COMMAND  (decimal 0-31 or 0x00-0x1F hex)");

            var row = HRow("RawRow", c, RowH);
            MakeText("Command:", row, FontSz, ColTextDim)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 86;
            _rawCmdField = InputFld("RawCmd", row, 90, "0x10");
            MakeButton("Send", row, ColRaw, SmBtnW, OnSendRaw);
            // quick-fire hints
            MakeText("  e.g.  0x11=LITE  0x12=BONV  0x14=LASER  0x1A=SCREAM",
                     row, FontSz - 2, ColTextDim);
        }

        private void OnSendRaw()
        {
            if (_board == null || _rawCmdField == null) return;
            var s = _rawCmdField.text.Trim();
            byte cmd;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!byte.TryParse(s.Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out cmd))
                {
                    Debug.LogWarning("[TestUI] Invalid hex command: " + s);
                    return;
                }
            }
            else if (!byte.TryParse(s, out cmd))
            {
                Debug.LogWarning("[TestUI] Invalid decimal command: " + s);
                return;
            }
            if (cmd > 31)
            {
                Debug.LogWarning($"[TestUI] Command 0x{cmd:X2} > 0x1F — clamped to 0x1F.");
                cmd = 0x1F;
            }
            _board.StopAll();
            _board.SendSoundCommand(cmd);
        }

        // ── Section: Sequence builder ─────────────────────────────────────────────
        private void BuildSequenceBuilderSection(GameObject c)
        {
            SectionHeader(c, "SEQUENCE BUILDER  (priority + up to 4 steps)");

            // Priority row
            var priRow = HRow("SeqPri", c, RowH);
            MakeText("Priority (0–255):", priRow, FontSz, ColTextDim)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            _seqPriorityField = InputFld("Pri", priRow, 70, "192");
            MakeText("  Higher = more urgent  ($FF=coin, $F0=death, $C0=laser)",
                     priRow, FontSz - 2, ColTextDim);

            // Step rows
            for (int i = 0; i < 4; i++)
            {
                int idx = i;   // capture for lambda
                var sr = HRow($"Step{i}", c, RowH);

                MakeText($"Step {i + 1}:", sr, FontSz, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 52;

                MakeText("Rep", sr, FontSz - 1, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 28;
                _seqRepeat[idx] = InputFld($"R{i}", sr, 52, i == 0 ? "1" : "");

                MakeText("Timer(frm)", sr, FontSz - 1, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 76;
                _seqTimer[idx] = InputFld($"T{i}", sr, 52, i == 0 ? "8" : "");

                MakeText("Cmd(hex)", sr, FontSz - 1, ColTextDim)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 64;
                _seqCmd[idx] = InputFld($"C{i}", sr, 52, i == 0 ? "11" : "");
            }

            // Trigger row
            var trigRow = HRow("SeqTrigger", c, RowH + 4);
            MakeButton("Trigger Sequence", trigRow, ColBuilder, 200, OnTriggerBuilt);
            MakeText("  (empty Cmd or Rep rows are skipped)", trigRow, FontSz - 2, ColTextDim);
        }

        private void OnTriggerBuilt()
        {
            if (_board == null) return;

            if (!byte.TryParse(_seqPriorityField.text.Trim(), out byte priority))
            {
                Debug.LogWarning("[TestUI] Invalid priority — must be 0–255 decimal.");
                return;
            }

            var steps = new List<SoundStep>();
            for (int i = 0; i < 4; i++)
            {
                var repStr = _seqRepeat[i].text.Trim();
                var tmrStr = _seqTimer[i].text.Trim();
                var cmdStr = _seqCmd[i].text.Trim();

                if (string.IsNullOrEmpty(repStr) && string.IsNullOrEmpty(cmdStr))
                    continue;   // skip empty rows

                if (!int.TryParse(repStr, out int rep) || rep <= 0) rep = 1;
                if (!int.TryParse(tmrStr, out int tmr)) tmr = 8;

                byte cmdId;
                if (cmdStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    byte.TryParse(cmdStr.Substring(2),
                        System.Globalization.NumberStyles.HexNumber, null, out cmdId);
                else
                    byte.TryParse(cmdStr, out cmdId);

                steps.Add(new SoundStep(rep, tmr, cmdId));
            }

            if (steps.Count == 0)
            {
                Debug.LogWarning("[TestUI] Sequence builder: no valid steps entered.");
                return;
            }

            var seq = new SoundSequence(priority, steps.ToArray());
            Debug.Log($"[TestUI] Triggering custom sequence: priority=0x{priority:X2}, " +
                      $"{steps.Count} step(s).");
            _board.StopAll();
            _board.TriggerSequence(seq);
        }

        // ── Layout helpers ────────────────────────────────────────────────────────

        private void SectionHeader(GameObject parent, string title)
        {
            var go  = NewPanel("Hdr_" + title, parent, ColSection);
            var le  = go.AddComponent<LayoutElement>();
            le.preferredHeight = 28;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding        = new RectOffset(10, 4, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            MakeText("-- " + title, go, FontSz - 1, new Color(0.55f, 0.75f, 1.0f));
        }

        private void ButtonRow(GameObject parent, string label, Color btnColor,
                               (string name, Action action)[] buttons)
        {
            var row = HRow("Row_" + label, parent, RowH);

            var lbl = MakeText(label, row, FontSz - 1, ColTextDim);
            lbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 36;

            foreach (var (name, action) in buttons)
                MakeButton(name, row, btnColor, BtnW, action);
        }

        private GameObject HRow(string name, GameObject parent, float height)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var le  = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = 5;
            hlg.childAlignment      = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            return go;
        }

        // ── Primitive widget factories ────────────────────────────────────────────

        private GameObject NewPanel(string name, GameObject parent, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private Text MakeText(string content, GameObject parent, int size, Color color)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text               = content;
            t.font               = _font;
            t.fontSize           = size;
            t.color              = color;
            t.alignment          = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return t;
        }

        private Button MakeButton(string label, GameObject parent, Color bgColor,
                                  float width, Action onClick)
        {
            var go  = new GameObject("Btn_" + label);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = bgColor * 1.45f;
            colors.pressedColor     = bgColor * 0.65f;
            colors.selectedColor    = bgColor;
            btn.colors = colors;
            if (onClick != null)
                btn.onClick.AddListener(() => { _board?.StopAll(); onClick(); });

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = width;
            le.preferredHeight = BtnH;

            // Label child
            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var tRT = txtGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(3, 2);
            tRT.offsetMax = new Vector2(-3, -2);
            var t = txtGO.AddComponent<Text>();
            t.text               = label;
            t.font               = _font;
            t.fontSize           = label.Contains('\n') ? FontSz - 2 : FontSz;
            t.color              = Color.white;
            t.alignment          = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return btn;
        }

        private InputField InputFld(string name, GameObject parent, float width, string placeholder)
        {
            var go  = new GameObject("Inp_" + name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = ColInput;
            var fld = go.AddComponent<InputField>();
            var le  = go.AddComponent<LayoutElement>();
            le.preferredWidth  = width;
            le.preferredHeight = BtnH;

            // Placeholder text
            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phRT = phGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(4, 2); phRT.offsetMax = new Vector2(-4, -2);
            var phT = phGO.AddComponent<Text>();
            phT.text      = placeholder;
            phT.font      = _font;
            phT.fontSize  = FontSz;
            phT.color     = new Color(0.45f, 0.45f, 0.45f);
            phT.alignment = TextAnchor.MiddleCenter;
            fld.placeholder = phT;

            // Input text
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(4, 2); txtRT.offsetMax = new Vector2(-4, -2);
            var txtT = txtGO.AddComponent<Text>();
            txtT.font      = _font;
            txtT.fontSize  = FontSz;
            txtT.color     = ColTextMain;
            txtT.alignment = TextAnchor.MiddleCenter;
            fld.textComponent = txtT;

            return fld;
        }

        private static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin       = Vector2.zero;
            rt.anchorMax       = Vector2.one;
            rt.offsetMin       = Vector2.zero;
            rt.offsetMax       = Vector2.zero;
        }
    }
}
