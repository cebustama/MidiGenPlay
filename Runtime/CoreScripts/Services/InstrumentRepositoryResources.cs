using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Services
{
    public class InstrumentRepositoryResources : IInstrumentRepository
    {
        private readonly MidiGenPlayConfig cfg;

        private List<MIDIInstrumentSO> melodic = new();
        private List<MIDIPercussionInstrumentSO> percussion = new();

        public InstrumentRepositoryResources(MidiGenPlayConfig settings = null)
        {
            cfg = settings ?? MidiGenPlayConfig.FindInResources()
                        ?? ScriptableObject.CreateInstance<MidiGenPlayConfig>();
        }

        public void Refresh()
        {
            // Load both sources; de-dupe
            int pkgC, locC;
            var all = LoadBoth<MIDIInstrumentSO>(
                cfg.PackageInstrumentsPath,   // package hard-coded inside config
                cfg.resourcesInstrumentsPath, // local configurable root
                out pkgC, out locC);

            percussion = all.OfType<MIDIPercussionInstrumentSO>().ToList();
            melodic = all.Where(i => !(i is MIDIPercussionInstrumentSO)).ToList();

            if (cfg.logRepository)
                Debug.Log($"[InstrRepo] Instruments: pkg={pkgC}, local={locC}, " +
                          $"total={all.Count} (mel:{melodic.Count} perc:{percussion.Count})");
        }

        public IReadOnlyList<MIDIInstrumentSO> GetMelodicInstruments() => melodic;
        public IReadOnlyList<MIDIPercussionInstrumentSO> GetPercussionInstruments() => percussion;

        private static List<T> LoadBoth<T>(string pkgPath, string localPath,
                                           out int pkgCount, out int localCount)
            where T : UnityEngine.Object
        {
            var result = new List<T>();
            var seen = new HashSet<T>();

            var pkg = Resources.LoadAll<T>(pkgPath) ?? System.Array.Empty<T>();
            foreach (var x in pkg) if (x && seen.Add(x)) result.Add(x);
            pkgCount = pkg.Length;

            localCount = 0;
            if (!string.Equals(pkgPath, localPath, System.StringComparison.Ordinal))
            {
                var loc = Resources.LoadAll<T>(localPath) ?? System.Array.Empty<T>();
                foreach (var x in loc) if (x && seen.Add(x)) result.Add(x);
                localCount = loc.Length;
            }

            return result;
        }
    }
}
