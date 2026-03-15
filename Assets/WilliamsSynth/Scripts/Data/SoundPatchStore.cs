using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WilliamsSynth
{
    /// <summary>
    /// Runtime store for user-defined parameter patches.
    ///
    /// Directly mutates <see cref="SoundParameterTables.GWaveParams"/> and
    /// <see cref="VariParameterTables.VariPresets"/> in-place, so generators pick up
    /// changes on the next <c>Trigger()</c> call without restarting Unity.
    ///
    /// The first call to ApplyGWave / ApplyVari for a given command captures the
    /// current byte array as the "original ROM" snapshot so <c>Reset*()</c> can
    /// restore it exactly — even if the table was patched earlier in the session.
    ///
    /// File format (one entry per line, human-editable):
    ///   GWAVE 0xCMD b0 b1 b2 b3 b4 b5 b6   (7 decimal bytes)
    ///   VARI  0xCMD b0 b1 b2 b3 b4 b5 b6 b7 b8   (9 decimal bytes)
    /// Lines starting with '#' and blank lines are ignored.
    /// </summary>
    public static class SoundPatchStore
    {
        // Snapshots of the pre-patch (ROM) state, keyed by command byte.
        private static readonly Dictionary<byte, byte[]> _gwaveOrig = new Dictionary<byte, byte[]>();
        private static readonly Dictionary<byte, byte[]> _variOrig  = new Dictionary<byte, byte[]>();

        // ── Default save path ─────────────────────────────────────────────────────
        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "sound_patches.txt");

        // ── Apply ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a 7-byte patch into <c>GWaveParams[cmdId]</c> in place.
        /// Captures the current ROM values on the first call for each cmdId.
        /// Returns false if cmdId is out of range or patch is not 7 bytes.
        /// </summary>
        public static bool ApplyGWave(byte cmdId, byte[] patch)
        {
            if (cmdId >= SoundParameterTables.GWaveParams.Length || patch?.Length != 7)
                return false;

            byte[] target = SoundParameterTables.GWaveParams[cmdId];
            if (!_gwaveOrig.ContainsKey(cmdId))
                _gwaveOrig[cmdId] = (byte[])target.Clone();

            Array.Copy(patch, target, 7);
            return true;
        }

        /// <summary>
        /// Writes a 9-byte patch into <c>VariPresets[cmdId - SAW]</c> in place.
        /// Returns false if cmdId is not in $1C–$1F range or patch is not 9 bytes.
        /// </summary>
        public static bool ApplyVari(byte cmdId, byte[] patch)
        {
            int idx = cmdId - SoundCommand.SAW;
            if (idx < 0 || idx >= VariParameterTables.VariPresets.Length || patch?.Length != 9)
                return false;

            byte[] target = VariParameterTables.VariPresets[idx];
            if (!_variOrig.ContainsKey(cmdId))
                _variOrig[cmdId] = (byte[])target.Clone();

            Array.Copy(patch, target, 9);
            return true;
        }

        // ── Reset ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Restores <c>GWaveParams[cmdId]</c> to the captured pre-patch values.
        /// Returns false if cmdId was never patched this session.
        /// </summary>
        public static bool ResetGWave(byte cmdId)
        {
            if (!_gwaveOrig.TryGetValue(cmdId, out byte[] orig)) return false;
            Array.Copy(orig, SoundParameterTables.GWaveParams[cmdId], 7);
            _gwaveOrig.Remove(cmdId);
            return true;
        }

        /// <summary>
        /// Restores <c>VariPresets[cmdId - SAW]</c> to the captured pre-patch values.
        /// Returns false if cmdId was never patched this session.
        /// </summary>
        public static bool ResetVari(byte cmdId)
        {
            if (!_variOrig.TryGetValue(cmdId, out byte[] orig)) return false;
            Array.Copy(orig, VariParameterTables.VariPresets[cmdId - SoundCommand.SAW], 9);
            _variOrig.Remove(cmdId);
            return true;
        }

        public static bool IsGWavePatched(byte cmdId) => _gwaveOrig.ContainsKey(cmdId);
        public static bool IsVariPatched(byte cmdId)  => _variOrig.ContainsKey(cmdId);

        // ── File I/O ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Saves all currently-patched commands to <paramref name="path"/>.
        /// Only commands that have been patched this session (and not subsequently
        /// reset) are written. Overwrites any existing file.
        /// </summary>
        public static void SavePatches(string path)
        {
            using var w = new StreamWriter(path, append: false);
            w.WriteLine("# WilliamsSynth sound patches — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            w.WriteLine("# GWAVE <0xCMD> <7 decimal bytes>");
            w.WriteLine("# VARI  <0xCMD> <9 decimal bytes>");
            w.WriteLine();

            foreach (var kv in _gwaveOrig)
            {
                byte[] p = SoundParameterTables.GWaveParams[kv.Key];
                w.WriteLine($"GWAVE 0x{kv.Key:X2} {string.Join(" ", p)}");
            }
            foreach (var kv in _variOrig)
            {
                byte[] p = VariParameterTables.VariPresets[kv.Key - SoundCommand.SAW];
                w.WriteLine($"VARI 0x{kv.Key:X2} {string.Join(" ", p)}");
            }
        }

        /// <summary>
        /// Loads and applies patches from <paramref name="path"/>.
        /// Returns the number of patches applied, or -1 if the file does not exist.
        /// </summary>
        public static int LoadPatches(string path)
        {
            if (!File.Exists(path)) return -1;

            int count = 0;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string[] tok = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 2 || !TryParseByte(tok[1], out byte cmdId)) continue;

                string type = tok[0].ToUpperInvariant();

                if (type == "GWAVE" && tok.Length == 9)
                {
                    var p = new byte[7];
                    bool ok = true;
                    for (int i = 0; i < 7 && ok; i++)
                        ok = byte.TryParse(tok[i + 2], out p[i]);
                    if (ok && ApplyGWave(cmdId, p)) count++;
                }
                else if (type == "VARI" && tok.Length == 11)
                {
                    var p = new byte[9];
                    bool ok = true;
                    for (int i = 0; i < 9 && ok; i++)
                        ok = byte.TryParse(tok[i + 2], out p[i]);
                    if (ok && ApplyVari(cmdId, p)) count++;
                }
            }
            return count;
        }

        // ── C# snippet generator ─────────────────────────────────────────────────

        /// <summary>
        /// Returns a C# array literal for the current (possibly patched) parameters
        /// of the given command, ready to paste into SoundParameterTables.cs or
        /// VariParameterTables.cs.
        /// </summary>
        public static string ToCSSnippet(byte cmdId, bool isGWave)
        {
            string lbl = SoundCommand.GetLabel(cmdId);
            if (isGWave)
            {
                if (cmdId >= SoundParameterTables.GWaveParams.Length) return "";
                byte[] p = SoundParameterTables.GWaveParams[cmdId];
                return $"// ${cmdId:X2} {lbl}\n"
                     + $"new byte[] {{ {string.Join(", ", Array.ConvertAll(p, b => $"0x{b:X2}"))} }},";
            }
            else
            {
                int idx = cmdId - SoundCommand.SAW;
                if (idx < 0 || idx >= VariParameterTables.VariPresets.Length) return "";
                byte[] p = VariParameterTables.VariPresets[idx];
                return $"// ${cmdId:X2} {lbl}\n"
                     + $"new byte[] {{ {string.Join(", ", Array.ConvertAll(p, b => $"0x{b:X2}"))} }},";
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private static bool TryParseByte(string s, out byte v)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.TryParse(s.Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out v);
            return byte.TryParse(s, out v);
        }
    }
}
