using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory;

namespace MidiGenPlay
{
    public static partial class MusicTheory
    {
        public enum TempoRange
        {
            VerySlow,  // e.g., Largo, Adagio
            Slow,      // e.g., Andante, Moderato
            Moderate,  // e.g., Allegretto, Allegro
            Fast,      // e.g., Vivace
            VeryFast   // e.g., Presto, Prestissimo
        }

        public enum TempoRule
        {
            Any,             // Any value within the range
            MultiplesOfTen,   // Only multiples of 10
            MultiplesOfFive,  // Only multiples of 5
            OnlyEven          // Only even numbers
        }

        private static readonly Dictionary<TempoRange, (int Min, int Max)> TempoRanges = new()
        {
            { TempoRange.VerySlow, (40, 60) },
            { TempoRange.Slow, (61, 90) },
            { TempoRange.Moderate, (91, 120) },
            { TempoRange.Fast, (121, 160) },
            { TempoRange.VeryFast, (161, 200) }
        };   

        public static Scale GetScaleFromTonality(Tonality tonality, NoteName rootNote)
        {
            if (!TonalityIntervals.TryGetValue(tonality, out var intervals))
                throw new ArgumentException($"Tonality {tonality} is not defined.");

            return new Scale(intervals, rootNote);
        }

        public static List<NoteName> GetTonalityNoteNames(Tonality tonality, NoteName rootNote)
        {
            Scale scale = GetScaleFromTonality(tonality, rootNote);
            List<NoteName> notes = new List<NoteName>();
            int intervalCount = TonalityIntervals[tonality].Length;
            for (int i = 0; i < intervalCount; i++)
            {
                notes.Add(scale.GetStep(i));
            }
            return notes;
        }

        public static List<Chord> GetTonalityChords(Tonality tonality, NoteName rootNote, List<int> steps)
        {
            var notes = GetTonalityNoteNames(tonality, rootNote);

            List<Chord> tonalityChords = new List<Chord>();
            for (int n = 0; n < notes.Count; n++)
            {
                NoteName chordRootNote = notes[n];
                NoteName[] aboveNotes = new NoteName[steps.Count];
                for (int s = 0; s < steps.Count; s++)
                {
                    aboveNotes[s] = notes[(n + steps[s]) % notes.Count];
                }
                tonalityChords.Add(new Chord(chordRootNote, aboveNotes));
            }

            return tonalityChords;
        }
        
        public static List<Chord> GetTonalityDiatonicTriads(Tonality tonality, NoteName rootNote)
        {
            return GetTonalityChords(tonality, rootNote, new List<int> { 3, 5 } );
        }

        public static List<Chord> GetTonalitySeventhChords(Tonality tonality, NoteName rootNote)
        {
            return GetTonalityChords(tonality, rootNote, new List<int> { 3, 5, 7 });
        }

        public static List<Note> GetNotesFromTonality(Tonality tonality, NoteName rootNote, int startingOctave)
        {
            if (!TonalityIntervals.TryGetValue(tonality, out var intervals))
                throw new ArgumentException($"Tonality {tonality} is not defined.");

            var scale = new Scale(intervals, rootNote);
            return GetNotesFromScale(scale, rootNote, startingOctave, intervals.Length);
        }

        public static List<Chord> GetChordsFromTonality(Tonality tonality, NoteName rootNote, int startingOctave)
        {
            if (!TonalityIntervals.TryGetValue(tonality, out var intervals))
                throw new ArgumentException($"Tonality {tonality} is not defined.");

            var scale = new Scale(intervals, rootNote);
            return GetDiatonicChordsFromScale(scale, startingOctave);
        }

        public static int GetBPMFromRange(TempoRange tempoRange, TempoRule rule)
        {
            // Validate the tempo range
            if (!TempoRanges.TryGetValue(tempoRange, out var range))
                throw new ArgumentException($"Invalid TempoRange: {tempoRange}");

            // Generate valid BPM values based on the rule
            var validBPMs = Enumerable.Range(range.Min, range.Max - range.Min + 1)
                .Where(bpm =>
                {
                    return rule switch
                    {
                        TempoRule.MultiplesOfTen => bpm % 10 == 0,
                        TempoRule.MultiplesOfFive => bpm % 5 == 0,
                        TempoRule.OnlyEven => bpm % 2 == 0,
                        TempoRule.Any => true,
                        _ => throw new ArgumentException($"Unknown TempoRule: {rule}")
                    };
                })
                .ToList();

            // Ensure the list contains valid BPMs
            if (!validBPMs.Any())
                throw new InvalidOperationException($"No valid BPMs found for TempoRange: {tempoRange} with rule: {rule}");

            // Return a random BPM from the valid options
            var random = new System.Random();
            return validBPMs[random.Next(validBPMs.Count)];
        }

        public static List<Note> GetNotesFromScale(
            Scale scale, NoteName startingNoteName, int startingOctave, int numberOfNotes)
        {
            Note startingNote = Note.Get(startingNoteName, startingOctave);
            List<Note> ascendingNotes = ScaleUtilities.GetAscendingNotes(scale, startingNote).ToList();

            int numberOfIntervals = scale.Intervals.Count();

            List<Note> notes = new List<Note>();
            for (int i = 0; i < numberOfNotes; i++)
            {
                int noteIndex = i % numberOfIntervals; // Wrap around the scale if needed
                Note currentNote = ascendingNotes[noteIndex];

                // Adjust the octave if we are wrapping to a higher octave
                int octaveAdjustment = (i / numberOfIntervals);
                currentNote = Note.Get(currentNote.NoteName, currentNote.Octave + octaveAdjustment);

                notes.Add(currentNote);
            }

            return notes;
        }

        public static bool GetNoteFromScale(Scale scale, ScaleDegree degree, NoteName rootNote, int octave, out Note note)
        {
            // Ensure the degree is within valid range
            if ((int)degree < 0 || (int)degree >= scale.Intervals.Count())
            {
                UnityEngine.Debug.LogWarning($"Invalid scale degree {degree} for scale {scale}.");
                note = null;
                return false;
            }

            // Retrieve the scale notes
            List<Note> scaleNotes = GetNotesFromScale(scale, rootNote, octave, scale.Intervals.Count());

            // Get the note corresponding to the scale degree (1-based index)
            note = scaleNotes[(int)degree]; // Convert 1-based to 0-based index

            return true;
        }


        // TODO: Get chords from scale with option to include third, fifth and seventh intervals

        public static List<Note> GetOctaveFromScale(Scale scale, int octaveNumber)
        {
            // Start with the root note of the scale at the specified starting octave
            NoteName rootNoteName = scale.RootNote;

            // Use GetNotesFromScale to generate 8 notes starting from the specified octave
            List<Note> notes = GetNotesFromScale(scale, rootNoteName, octaveNumber, 8);

            return notes;
        }

        public static List<Chord> GetDiatonicChordsFromScale(Scale scale, int startingOctave)
        {
            List<Chord> diatonicChords = new List<Chord>();

            // Use GetNotesFromScale to generate the notes of the scale, starting from the specified octave
            List<Note> scaleNotes = GetNotesFromScale(scale, scale.RootNote, startingOctave, 8);
            int intervalCount = scale.Intervals.Count();

            for (int i = 0; i < intervalCount; i++)
            {
                // Get the root note of the chord
                Note rootNote = scaleNotes[i];

                // Calculate the third, fifth, and seventh intervals relative to the current root note
                Note third = GetNoteAtInterval(scale, scaleNotes, i, 2);
                Note fifth = GetNoteAtInterval(scale, scaleNotes, i, 4);
                Note seventh = GetNoteAtInterval(scale, scaleNotes, i, 6);

                // Create the chord using the calculated notes
                // TODO: How to store the specific octaves?
                diatonicChords.Add(new Chord(new NoteName[]
                {
                    rootNote.NoteName,
                    third.NoteName,
                    fifth.NoteName,
                    seventh.NoteName
                }));
            }

            return diatonicChords;
        }

        private static Note GetNoteAtInterval(Scale scale, List<Note> scaleNotes, int startIndex, int interval)
        {
            // Calculate the index in the scale based on the interval
            int intervalCount = scale.Intervals.Count();
            int targetIndex = (startIndex + interval) % intervalCount;

            // Determine how many octaves to shift up
            int octaveShift = (startIndex + interval) / intervalCount;

            // Get the corresponding note and adjust the octave if necessary
            Note baseNote = scaleNotes[targetIndex];
            return Note.Get(baseNote.NoteName, baseNote.Octave + octaveShift);
        }

        public static NoteName GetRandomNote()
        {
            return ChooseFromEnumUniform<NoteName>();
        }

        private static T ChooseFromEnumUniform<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            System.Random random = new System.Random();
            int index = random.Next(0, values.Length); // Random index from the enum values
            return (T)values.GetValue(index);
        }

        public static Chord GetChordFromString(string chord)
        {
            Chord ch = Chord.Parse(chord);
            return ch;
        }

        public static Chord GetChordFromQuality(NoteName rootNote, ChordQuality quality)
        {
            return Chord.GetByTriad(rootNote, quality);
        }

        public static Dictionary<ScaleDegree, Chord> GetChordsDegreeDictionary(List<Chord> chords)
        {
            // TODO: Catch exception for when not enough chords

            Dictionary<ScaleDegree, Chord> chordsByDegree =
                new Dictionary<ScaleDegree, Chord>()
                {
                    { ScaleDegree.Tonic, chords[0] },
                    { ScaleDegree.Supertonic, chords[1] },
                    { ScaleDegree.Mediant, chords[2] },
                    { ScaleDegree.Subdominant, chords[3] },
                    { ScaleDegree.Dominant, chords[4] },
                    { ScaleDegree.Submediant, chords[5] },
                    { ScaleDegree.LeadingTone, chords[6] }
                };

            return chordsByDegree;
        }
    }
}