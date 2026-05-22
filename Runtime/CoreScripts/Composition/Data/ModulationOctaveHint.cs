namespace MidiGenPlay.Composition
{
    /// <summary>
    /// One-shot directional intent for the first chord of a post-modulation render.
    ///
    /// Consumers (e.g. ALWTTT's ModulationEffect.apply()) set this on
    /// SongConfig.PartConfig together with PartConfig.PreviousRootNote BEFORE the
    /// part is rendered. The composer consumes both transients on entry, applies
    /// the directional override to the first chord of the render, and clears the
    /// transients. Default <see cref="Auto"/> preserves current voice-leading
    /// behavior bit-identically.
    /// </summary>
    public enum ModulationOctaveHint
    {
        Auto = 0,
        Up = 1,
        Down = 2,
    }
}