using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// D-SMOKE-RT-5=A: single source of truth for a Composition Smoke render,
    /// referenced by BOTH the editor window and the runtime runner so their
    /// inputs cannot drift. Holds everything that affects output: the engine
    /// config, the shared Part context, the track rows, the seed, and the
    /// metronome-strip toggle.
    ///
    /// Runner-only knobs deliberately stay on the runner and are NOT here:
    /// RT-4=A Root/BPM randomization (breaks window parity by design) and
    /// renderOnStart (a trigger, not a render input).
    ///
    /// Assign the same asset to the window (Save/Load buttons) and the runner's
    /// 'setup' field; both then render byte-identical inputs by construction —
    /// no more matching ~15 fields across two inspectors by hand.
    ///
    /// Runtime-safe: no UnityEditor. Dev/test tooling only.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SmokeSetup_",
        menuName = "MidiGenPlay/Smoke/Smoke Setup")]
    public class SmokeSetupSO : ScriptableObject
    {
        [Header("Engine")]
        public MidiGenPlayConfig config;

        [Header("Part context (shared by all tracks)")]
        public SmokePartContext partContext = new SmokePartContext();

        [Tooltip("List the Backing entry FIRST: Bassline/Melody read chords " +
                 "from the Backing row's progression (finding C4).")]
        public List<SmokeEntry> entries = new List<SmokeEntry>();

        [Header("Determinism")]
        public bool overrideSeed = false;
        public int seed = 12345;

        [Header("Output")]
        public bool stripMetronome = false; // D-SMOKE-MT-5=A
    }
}