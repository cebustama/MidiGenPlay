using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.MusicTheory
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
            { TempoRange.VerySlow, (61, 90) },
            { TempoRange.Slow, (91, 120) },
            { TempoRange.Moderate, (121, 160) },
            { TempoRange.Fast, (161, 200) },
            { TempoRange.VeryFast, (201, 240) }
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
            /*UnityEngine.Debug.Log($"Starting Note Name {startingNoteName} +" +
                $"Starting Octave {startingOctave}");*/

            if (startingOctave == 9) startingOctave = 8; // TEMP FOR SAFETY
            Note startingNote = Note.Get(startingNoteName, startingOctave);
            List<Note> ascendingNotes =
                ScaleUtilities.GetAscendingNotes(scale, startingNote).ToList();

            int numberOfIntervals = scale.Intervals.Count();

            /*UnityEngine.Debug.Log($"Scale Notes #{ascendingNotes.Count}, " +
                $"Note #{numberOfNotes}, Interval #{numberOfIntervals}");*/

            List<Note> notes = new List<Note>();
            for (int i = 0; i < numberOfNotes; i++)
            {
                int noteIndex = i % numberOfIntervals; // Wrap around the scale if needed
                if (noteIndex >= ascendingNotes.Count) noteIndex %= ascendingNotes.Count;
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

        public static Chord GetChordFromQuality(NoteName rootNote, 
            Melanchall.DryWetMidi.MusicTheory.ChordQuality quality)
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

        public static NoteName AddSemitones(NoteName root, int semitones)
        {
            int v = (((int)root) + semitones) % 12; if (v < 0) v += 12;
            return (NoteName)v; // DryWetMIDI NoteName uses 12-TET with sharps
        }

        public static NoteName[] GetChordNoteNames(NoteName root, ChordQuality q)
        {
            var ivs = GetIntervalsForQuality(q);
            var names = new NoteName[ivs.Length];
            for (int i = 0; i < ivs.Length; i++) names[i] = AddSemitones(root, ivs[i]);
            return names;
        }

        public static string DescribeScale(Tonality mode, NoteName root)
        {
            var scale = GetScaleFromTonality(mode, root);
            var names = GetNotesFromScale(scale, root, 4, 7).Select(n => n.NoteName.ToString());
            return $"{mode} ({root}): " + string.Join(" ", names);
        }

        public static NoteName[] ChordPitchClasses(
            Tonality tonality,
            NoteName partRoot,
            ScaleDegree degree,
            ChordQuality quality)
        {
            // degree root for the current tonality/root (any octave)
            var scale = GetScaleFromTonality(tonality, partRoot);
            var scaleNames = 
                GetNotesFromScale(scale, partRoot, 4, 7).Select(n => n.NoteName).ToArray();
            var degreeRoot = scaleNames[(int)degree];

            // chord pitch classes (names) for that degree+quality
            return GetChordNoteNames(degreeRoot, quality);
        }

        // --- Enharmonic spelling helpers (labels) ---

        public static int PitchClass(NoteName n) => ((int)n) % 12;

        private static int NaturalPcForLetter(char L) => L switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => 0
        };

        public static char LetterOf(NoteName nn) => nn switch
        {
            NoteName.C or NoteName.CSharp => 'C',
            NoteName.D or NoteName.DSharp => 'D',
            NoteName.E => 'E',
            NoteName.F or NoteName.FSharp => 'F',
            NoteName.G or NoteName.GSharp => 'G',
            NoteName.A or NoteName.ASharp => 'A',
            NoteName.B => 'B',
            _ => 'C'
        };

        public static char LetterForDegree(NoteName keyRoot, int degreeIndex)
        {
            const string cycle = "CDEFGAB";
            int rootIdx = cycle.IndexOf(LetterOf(keyRoot));
            if (rootIdx < 0) rootIdx = 0;
            return cycle[(rootIdx + (degreeIndex % 7) + 7) % 7];
        }

        /// Label a diatonic note for a given degree using ♭/♯ as needed.
        /// Example (C Phrygian): degree 1→'D', actual pc=1 => "D♭"
        public static string SpellNoteForDegree(
            NoteName actualNote,
            NoteName keyRoot,
            int degreeIndex)
        {
            char L = LetterForDegree(keyRoot, degreeIndex);
            int nat = NaturalPcForLetter(L);
            int pc = PitchClass(actualNote);
            int delta = (pc - nat + 12) % 12; // 0,1,11 in diatonic contexts

            return delta switch
            {
                0 => L.ToString(),
                1 => $"{L}♯",
                11 => $"{L}♭",
                _ => actualNote.ToString() // fallback (non-diatonic/double-accidental)
            };
        }

        // === Modal color helpers (major-family vs minor-family) ===
        public static List<ScaleDegree> GetCharacteristicDegrees(Tonality mode, NoteName root)
        {
            // Compare major-family modes to Ionian; minor-family to Aeolian
            var baseline = mode switch
            {
                Tonality.Ionian or Tonality.Lydian or Tonality.Mixolydian => Tonality.Ionian,
                _ => Tonality.Aeolian
            };

            var refNotes = GetTonalityNoteNames(baseline, root); // diatonic steps 0..6
            var modeNotes = GetTonalityNoteNames(mode, root);

            var list = new List<ScaleDegree>(2);
            for (int i = 0; i < 7; i++)
            {
                if (refNotes[i] != modeNotes[i]) // pitch-class differs at this degree
                    list.Add((ScaleDegree)i);
            }
            return list;
        }

        // Build a simple weight table per degree (index 0..6)
        public static float[] BuildDegreeWeights(
            Tonality mode,
            NoteName root,
            float baseW = 1f,
            float rootBonus = 3f,
            float domBonus = 1.5f,
            float charBonus = 2f)
        {
            var w = new float[7];
            for (int i = 0; i < 7; i++) w[i] = baseW;

            w[(int)ScaleDegree.Tonic] += rootBonus;
            w[(int)ScaleDegree.Dominant] += domBonus;

            foreach (var d in GetCharacteristicDegrees(mode, root))
                w[(int)d] += charBonus;

            return w;
        }

        // Bare roman for quick logs (no quality)
        public static string RomanBare(ScaleDegree d) => d switch
        {
            ScaleDegree.Tonic => "I",
            ScaleDegree.Supertonic => "II",
            ScaleDegree.Mediant => "III",
            ScaleDegree.Subdominant => "IV",
            ScaleDegree.Dominant => "V",
            ScaleDegree.Submediant => "VI",
            ScaleDegree.LeadingTone => "VII",
            _ => "?"
        };
    }
}