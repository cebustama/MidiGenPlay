using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Common;
using MusicTheoryNote = Melanchall.DryWetMidi.MusicTheory.Note;
using MusicTheoryChord = Melanchall.DryWetMidi.MusicTheory.Chord;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay
{
    public class EnhancedPatternBuilder
    {
        private readonly PatternBuilder patternBuilder;
        //private readonly IInstrumentPatternStrategy instrumentPatternStrategy;
        private readonly InstrumentType instrumentType;
        private readonly TrackRole role;
        private readonly Genre genre;

        public EnhancedPatternBuilder(
            InstrumentType instrumentType, TrackRole role, Genre genre)
        {
            patternBuilder = new PatternBuilder();
            this.instrumentType = instrumentType;
            this.role = role;
            this.genre = genre;
        }

        public EnhancedPatternBuilder AddNote(
            MusicTheoryNote note,
            ITimeSpan length,
            SevenBitNumber? velocity = null)
        {
            patternBuilder.Note(note, length);
            return this;
        }

        public EnhancedPatternBuilder AddChord(
            MusicTheoryChord chord,
            ITimeSpan chordLength,
            Octave octave,
            SevenBitNumber? velocity = null)
        {
            var notes = chord.ResolveNotes(octave);
            patternBuilder.Chord(notes, length: chordLength);
            return this;
        }

        public void Generate(int measures, 
            List<MusicTheoryChord> possibleChords, 
            int minOctave = 1, int maxOctave = 9)
        {
            switch (instrumentType)
            {
                case InstrumentType.Vocals:
                case InstrumentType.Piano:

                    switch (role)
                    {
                        case TrackRole.Backing:
                            UnityEngine.Debug.Log("Generating backing track for piano/voice.");
                            // Whole note chords, one per measure
                            for (int i = 0; i < measures; i++)
                            {
                                MusicTheoryChord randomChord = possibleChords[UnityEngine.Random.Range(0, possibleChords.Count)];
                                AddChord(randomChord, chordLength: MusicalTimeSpan.Whole, octave: Octave.Middle);
                            }

                            break;

                        case TrackRole.Lead:

                            UnityEngine.Debug.Log("Generating lead track for piano/voice.");
                            // Quarter notes, four per measure
                            float skipChance = 0.2f; // 30% chance to skip a note

                            for (int i = 0; i < measures; i++)
                            {
                                // Choose a random chord for the current measure
                                MusicTheoryChord randomChord = possibleChords[UnityEngine.Random.Range(0, possibleChords.Count)];
                                List<MusicTheoryNote> chordNotes = randomChord.ResolveNotes(Octave.Middle).ToList();

                                // Add four quarter notes, randomly selecting a note from the chord each time
                                for (int j = 0; j < 4; j++)
                                {
                                    // Randomly decide whether to skip the note
                                    if (UnityEngine.Random.value < skipChance)
                                    {
                                        // Skip this note
                                        continue;
                                    }

                                    // Choose a random note from the chord
                                    MusicTheoryNote randomNote = chordNotes[UnityEngine.Random.Range(0, chordNotes.Count)];

                                    AddNote(randomNote, MusicalTimeSpan.Quarter);
                                }
                            }

                            break;
                    }

                    break;

                case InstrumentType.Guitar:
                case InstrumentType.AcousticGuitar:

                    break;

                case InstrumentType.Bass:

                    switch (role)
                    {
                        case TrackRole.Backing:

                            break;
                        case TrackRole.Lead:

                            break;
                    }

                    break;

                case InstrumentType.Drums:

                    switch (role)
                    {
                        case TrackRole.Rhythm:
                            UnityEngine.Debug.Log("Generating rhythm track for drums.");
                            GenerateRhythmTrack(measures);
                            break;
                    }

                    break;
            }
        }

        private Octave GetRandomOctave(int minOctave, int maxOctave)
        {
            int randomOctave = UnityEngine.Random.Range(minOctave, maxOctave + 1);
            return Octave.Get(randomOctave);
        }

        public Pattern Build()
        {
            return patternBuilder.Build();
        }

        public void GenerateRhythmTrack(int measures)
        {
            var kick = Notes.C2;
            var snare = Notes.D2;
            var hiHatClosed = Notes.FSharp2;

            string pianoRollPattern = @$"
        {hiHatClosed}   ||||||||
          {snare}       --|---|-
           {kick}       |---|---";

            patternBuilder
                .SetNoteLength(MusicalTimeSpan.Eighth)
                .PianoRoll(pianoRollPattern)
                .Repeat(measures - 1);
        }
    }
}


