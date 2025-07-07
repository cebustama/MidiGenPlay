namespace MidiGenPlay
{
    /// <summary>
    /// A tab that represents one Track inside GenerateMidiSongPanel.
    /// Left‐click → SelectTrack, Right‐click → RemoveTrack.
    /// </summary>
    public class TrackTabButton : TabButtonBase<GenerateMidiSongPanel>
    {
        protected override void OnLeftClick()
        {
            Panel.SelectTrack(Index);
        }

        protected override void OnRightClick()
        {
            Panel.RemoveTrack(Index);
        }
    }
}