using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MidiGenPlay
{
    public class MIDIMusicSystem : MonoBehaviour
    {
        public MIDISong CurrentSong;
        [Header("References")]
        [SerializeField] private MidiFilePlayer midiFilePlayer;
        [SerializeField] private MidiStreamPlayer midiStreamPlayer;
        [SerializeField] private MidiPlayerGlobal midiPlayerGlobal;

        private MIDIGeneratorManager midiGenerator;

        private void Awake()
        {
            // TODO: Refactor the generator to not extend MonoBehaviour
            midiGenerator = GetComponentInChildren<MIDIGeneratorManager>();
            midiFilePlayer.OnEventNotesMidi.AddListener(NotesToPlay);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                //midiGenerator.GenerateMidiTrackData(InstrumentType.Drums, TrackRole.Rhythm, Genre.Pop, 100, null, true);
                //CurrentSong.GenerateSong(midiGenerator);
                //PlaySongPartWithDummy(CurrentSong.SongParts[0], 3, 1f);
                //PlaySong();
            }
        }

        private void PlaySong()
        {
            StartCoroutine(DelayedPlay(0.5f));
        }

        private IEnumerator DelayedPlay(float delay)
        {
            // Get the first MIDISongPart to play
            MIDISongPart partToPlay = CurrentSong.SongParts[0];

            // Convert the MidiFile to byte array (if not already in that format)
            byte[] midiData;
            using (var memoryStream = new MemoryStream())
            {
                partToPlay.CurrentMidiFile.Write(memoryStream);
                midiData = memoryStream.ToArray();
            }

            // Stop any previous playback to avoid conflicts
            midiFilePlayer.MPTK_Stop();

            // Wait for the specified delay
            yield return new WaitForSeconds(delay);

            // Start playback by directly setting and playing the MIDI data
            midiFilePlayer.MPTK_Play(midiData);
        }

        public void NotesToPlay(List<MPTKEvent> mptkEvents)
        {
            Debug.Log("Received " + mptkEvents.Count + " MIDI Events");
            // Loop on each MIDI events
            foreach (MPTKEvent mptkEvent in mptkEvents)
            {
                // Log if event is a note on
                if (mptkEvent.Command == MPTKCommand.NoteOn)
                    Debug.Log($"Note on Time:{mptkEvent.RealTime} millisecond  Note:{mptkEvent.Value}  Duration:{mptkEvent.Duration} millisecond  Velocity:{mptkEvent.Velocity}");

                //Uncomment to display all MIDI events
                Debug.Log(mptkEvent.ToString());
            }
        }
    }
}
