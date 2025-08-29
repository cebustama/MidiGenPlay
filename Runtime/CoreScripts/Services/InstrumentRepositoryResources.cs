using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Services
{
    public class InstrumentRepositoryResources : IInstrumentRepository
    {
        private const string INSTR_PATH = "ScriptableObjects/MIDI Instruments";

        private List<MIDIInstrumentSO> melodic = new();
        private List<MIDIPercussionInstrumentSO> percussion = new();

        public void Refresh()
        {
            var all = Resources.LoadAll<MIDIInstrumentSO>(INSTR_PATH).ToList();

            percussion = all.OfType<MIDIPercussionInstrumentSO>().ToList();
            melodic = all.Where(i => !(i is MIDIPercussionInstrumentSO)).ToList();
        }

        public IReadOnlyList<MIDIInstrumentSO> GetMelodicInstruments() => 
            melodic;
        public IReadOnlyList<MIDIPercussionInstrumentSO> GetPercussionInstruments() => 
            percussion;
    }
}