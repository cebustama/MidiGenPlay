using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Interfaces
{
    public interface IInstrumentRepository
    {
        void Refresh();
        IReadOnlyList<MIDIInstrumentSO> GetMelodicInstruments();
        IReadOnlyList<MIDIPercussionInstrumentSO> GetPercussionInstruments();
    }
}