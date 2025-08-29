using static MidiGenPlay.MusicTheory;

namespace MidiGenPlay.Interfaces
{
    public interface ITrackRoleUIController
    {
        TrackRole Role { get; }

        /// Refresh the controller’s pattern dropdown for this time signature.
        void RefreshPatterns(TimeSignature ts);

        /// Push cfg → UI (dropdowns set with SetValueWithoutNotify; no Save here).
        void LoadIntoUI(SongConfig.PartConfig.TrackConfig cfg);

        /// Pull UI → cfg (instrument + pattern for this role).
        void SaveFromUI(SongConfig.PartConfig.TrackConfig cfg);

        /// Show this role’s panel and the correct instrument dropdown group.
        void Activate(SongConfig.PartConfig.TrackConfig currentCfg);

        /// Hide this role’s panel (and leave instrument groups as needed).
        void Deactivate();
    }
}

