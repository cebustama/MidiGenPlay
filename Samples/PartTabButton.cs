namespace MidiGenPlay 
{
    /// <summary>
    /// A tab that represents one Part inside GenerateMidiSongPanel.
    /// Left‐click → SelectPart, Right‐click → RemovePart.
    /// </summary>
    public class PartTabButton : TabButtonBase<GenerateMidiSongPanel>
    {
        protected override void OnLeftClick()
        {
            Panel.SelectPart(Index);
        }

        protected override void OnRightClick()
        {
            Panel.RemovePart(Index);
        }
    }
}