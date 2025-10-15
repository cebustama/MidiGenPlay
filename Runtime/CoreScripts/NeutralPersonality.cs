using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>Neutral defaults so you can opt-in per musician gradually.</summary>
    public sealed class NeutralPersonality : IMusicianPersonality
    {
        public string MusicianId { get; }
        public float Density01 { get; }
        public int RangeLow { get; }
        public int RangeHigh { get; }
        public string PreferredScaleId { get; }
        public float Ornamentation01 { get; }
        public float VelocityBias01 { get; }

        public NeutralPersonality(string musicianId,
                                  float density01 = 0.5f,
                                  int rangeLow = 48,      // C3
                                  int rangeHigh = 72,     // C5
                                  string preferredScaleId = "", // empty = no bias
                                  float ornamentation01 = 0.5f,
                                  float velocityBias01 = 0.5f)
        {
            MusicianId = musicianId;
            Density01 = Mathf.Clamp01(density01);
            RangeLow = Mathf.Clamp(rangeLow, 0, 127);
            RangeHigh = Mathf.Clamp(rangeHigh, 0, 127);
            PreferredScaleId = preferredScaleId ?? "";
            Ornamentation01 = Mathf.Clamp01(ornamentation01);
            VelocityBias01 = Mathf.Clamp01(velocityBias01);
        }
    }
}