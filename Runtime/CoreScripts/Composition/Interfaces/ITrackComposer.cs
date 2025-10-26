namespace MidiGenPlay.Composition
{
    using Melanchall.DryWetMidi.Core;

    public interface ITrackComposerFactory
    {
        ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx);
    }

    public interface ITrackComposer
    {
        MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm, int channel,
            MidiGenerator.GenContext ctx); // pass helpers (scales, rng, voicer, etc.)
    }
}
