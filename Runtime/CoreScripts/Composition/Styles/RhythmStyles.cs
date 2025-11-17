using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;

using System;

using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Composition
{
    public interface IRhythmStyle
    {
        string Id { get; }                    // unique id (e.g., "waltz_ride_backbeat")
        string DisplayName { get; }           // UI-friendly (optional)
        TimeSignature Meter { get; }          // the meter this style targets
        float BaseWeight { get; }             // default selection weight
        MidiFile Compose(MIDIPercussionInstrumentSO kit,
            int bpm, int measures, int channel, RhythmRecipe recipe);
    }

    public sealed class Waltz3_4Style : IRhythmStyle
    {
        public string Id => "waltz_ride_backbeat";
        public string DisplayName => "Waltz (Ride, K1 S2,3)";
        public TimeSignature Meter => TimeSignature.ThreeFour;
        public float BaseWeight => 1f;

        public MidiFile Compose(
            MIDIPercussionInstrumentSO kit, int bpm, int measures, int channel, RhythmRecipe recipe)
        {
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Maps: ride bell → ride1 → ride2 → CHH fallback
            Melanchall.DryWetMidi.MusicTheory.Note ride;
            if (!kit.TryGetMappedNote(GeneralMidiPercussion.RideBell, out ride) &&
                !kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal1, out ride) &&
                !kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal2, out ride))
            {
                kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out ride);
            }

            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);

            var vRide = (SevenBitNumber)62;   // gentle ping
            var vKick = (SevenBitNumber)96;
            var vSnare = (SevenBitNumber)90;

            measures = Math.Max(1, measures);
            for (int m = 0; m < measures; m++)
                for (int b = 0; b < 3; b++) // 3/4
                {
                    double whenBeats = m * 3 + b;

                    // Ride on every beat
                    pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                    pb.Note(ride, MusicalTimeSpan.Quarter, vRide);

                    // Kick on beat 1
                    if (b == 0)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(kick, MusicalTimeSpan.Quarter, vKick);
                    }

                    // Snare on beats 2 & 3
                    if (b == 1 || b == 2)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(snare, MusicalTimeSpan.Quarter, vSnare);
                    }
                }

            // Return the file WITHOUT stamping bank/patch/channel. The composer will handle that,
            // so all styles can stay pure and focused on note placement.
            return pb.Build().ToFile(tempoMap);
        }
    }

    public sealed class RockBackbeat4_4Style : IRhythmStyle
    {
        public string Id => "rock_backbeat_4_4";
        public string DisplayName => "Rock Backbeat (4/4)";
        public TimeSignature Meter => TimeSignature.FourFour;
        public float BaseWeight => 1f;

        public MidiFile Compose(
            MIDIPercussionInstrumentSO kit, int bpm, int measures, int channel, RhythmRecipe recipe)
        {
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Mappings (with safe fallbacks)
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);

            // Hats: prefer CHH for beat grid, OHH only for the last beat of even bars
            var hasCHH = kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out var chh);
            var hasOHH = kit.TryGetMappedNote(GeneralMidiPercussion.OpenHiHat, out var ohh);
            if (!hasCHH)
            {
                // Fallback to ride if no CHH
                hasCHH = kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal1, out chh)
                      || kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal2, out chh)
                      || kit.TryGetMappedNote(GeneralMidiPercussion.RideBell, out chh);
            }

            // Velocities (tweak to taste)
            var vHat = (SevenBitNumber)60;
            var vOHat = (SevenBitNumber)75;
            var vKick = (SevenBitNumber)100;
            var vSnare = (SevenBitNumber)96;

            measures = Math.Max(1, measures);
            for (int m = 0; m < measures; m++)
            {
                bool isEvenBar = ((m + 1) % 2) == 0; // human 1-based: bar 2, 4, 6...
                for (int b = 0; b < 4; b++) // 4/4 beats
                {
                    double whenBeats = m * 4 + b;

                    // Closed hat on every beat
                    if (hasCHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(chh, MusicalTimeSpan.Quarter, vHat);
                    }

                    // Kick on beat 1
                    if (b == 0)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(kick, MusicalTimeSpan.Quarter, vKick);
                    }

                    // Snare on beat 3
                    if (b == 2)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(snare, MusicalTimeSpan.Quarter, vSnare);
                    }

                    // Open HH on the LAST beat of even bars (beat 4), if available
                    if (isEvenBar && b == 3 && hasOHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(ohh, MusicalTimeSpan.Quarter, vOHat);
                    }
                }
            }

            // Return file *without* stamping; the composer will stamp bank/patch & channel.
            return pb.Build().ToFile(tempoMap);
        }
    }

    public sealed class Shuffle6_8Style : IRhythmStyle
    {
        public string Id => "shuffle_6_8_default";
        public string DisplayName => "Shuffle (6/8)";
        public TimeSignature Meter => TimeSignature.SixEight;
        public float BaseWeight => 1f;

        public MidiFile Compose(
            MIDIPercussionInstrumentSO kit, int bpm, int measures, int channel, RhythmRecipe recipe)
        {
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Mappings with sensible fallbacks
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);

            // Prefer ride for compound feels; fallback to CHH if no ride
            Melanchall.DryWetMidi.MusicTheory.Note rideOrHat;
            if (!kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal1, out rideOrHat) &&
                !kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal2, out rideOrHat) &&
                !kit.TryGetMappedNote(GeneralMidiPercussion.RideBell, out rideOrHat))
            {
                kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out rideOrHat);
            }

            var hasOHH = kit.TryGetMappedNote(GeneralMidiPercussion.OpenHiHat, out var ohh);

            // Velocities (accent the two big pulses: 1 and 4)
            var vAccent = (SevenBitNumber)72;
            var vNormal = (SevenBitNumber)58;
            var vKick = (SevenBitNumber)100;
            var vSnare = (SevenBitNumber)96;
            var vOpenHat = (SevenBitNumber)75;

            measures = Math.Max(1, measures);

            for (int m = 0; m < measures; m++)
            {
                bool isEvenBar = ((m + 1) % 2) == 0;

                // 6 eighths per bar: indices 0..5  (accent on 0 and 3)
                for (int e = 0; e < 6; e++)
                {
                    double whenBeats = m * 6 + e; // place at eighth-note grid

                    // Ride/HH on every eighth (accent on 1 & 4 → e==0 and e==3)
                    var vHat = (e == 0 || e == 3) ? vAccent : vNormal;
                    pb.MoveToTime(MusicalTimeSpan.Eighth.Multiply(whenBeats));
                    pb.Note(rideOrHat, MusicalTimeSpan.Eighth, vHat);

                    // Kick on 1st eighth (downbeat)
                    if (e == 0)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Eighth.Multiply(whenBeats));
                        pb.Note(kick, MusicalTimeSpan.Eighth, vKick);
                    }

                    // Snare on 4th eighth (backbeat of 6/8)
                    if (e == 3)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Eighth.Multiply(whenBeats));
                        pb.Note(snare, MusicalTimeSpan.Eighth, vSnare);
                    }

                    // Optional: open HH on last eighth of even bars
                    if (isEvenBar && e == 5 && hasOHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Eighth.Multiply(whenBeats));
                        pb.Note(ohh, MusicalTimeSpan.Eighth, vOpenHat);
                    }
                }
            }

            // Return file *without* stamping program/channel; the composer will handle that.
            return pb.Build().ToFile(tempoMap);
        }
    }

    public sealed class Backbeat5_4Style : IRhythmStyle
    {
        public string Id => "backbeat_5_4_3plus2";
        public string DisplayName => "Backbeat (5/4 · 3+2)";
        public TimeSignature Meter => TimeSignature.FiveFour;
        public float BaseWeight => 1f;

        public MidiFile Compose(
            MIDIPercussionInstrumentSO kit, int bpm, int measures, int channel, RhythmRecipe recipe)
        {
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Core mappings (with safe fallbacks for hats)
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);

            // Prefer CHH; fallback to ride if no CHH
            var hasCHH = kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out var chh);
            if (!hasCHH)
            {
                hasCHH = kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal1, out chh)
                      || kit.TryGetMappedNote(GeneralMidiPercussion.RideCymbal2, out chh)
                      || kit.TryGetMappedNote(GeneralMidiPercussion.RideBell, out chh);
            }
            var hasOHH = kit.TryGetMappedNote(GeneralMidiPercussion.OpenHiHat, out var ohh);

            // Velocities
            var vHat = (SevenBitNumber)60;
            var vOHat = (SevenBitNumber)75;
            var vKick = (SevenBitNumber)100;
            var vSnare = (SevenBitNumber)96;

            measures = Math.Max(1, measures);

            for (int m = 0; m < measures; m++)
            {
                bool isEvenBar = ((m + 1) % 2) == 0; // 1-based bars: 2,4,6...
                for (int b = 0; b < 5; b++) // 5/4 beats: 0..4
                {
                    double whenBeats = m * 5 + b;

                    // Closed hat (or ride fallback) on every quarter
                    if (hasCHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(chh, MusicalTimeSpan.Quarter, vHat);
                    }

                    // Kick on beat 1 (index 0)
                    if (b == 0)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(kick, MusicalTimeSpan.Quarter, vKick);
                    }

                    // Snare on beat 4 (index 3) -> 3+2 grouping backbeat
                    if (b == 3)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(snare, MusicalTimeSpan.Quarter, vSnare);
                    }

                    // Optional: open hat on last beat (5th) of even bars
                    if (isEvenBar && b == 4 && hasOHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(ohh, MusicalTimeSpan.Quarter, vOHat);
                    }
                }
            }

            // Return file *without* stamping; composer will stamp bank/patch & channel.
            return pb.Build().ToFile(tempoMap);
        }
    }
}