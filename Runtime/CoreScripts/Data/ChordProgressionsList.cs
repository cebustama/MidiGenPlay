using MidiGenPlay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/_Chord Progression List")]
public class ChordProgressionsList : ScriptableObject
{
    public List<ChordProgressionData> chordProgressionDatas;

    /// <summary>
    /// Retrieves a Chord Progression based on a given Time Signature.
    /// If multiple progressions exist, one is randomly chosen.
    /// If no match is found, a completely random progression is returned.
    /// </summary>
    public ChordProgressionData GetRandomProgressionByTimeSignature(
        MusicTheory.TimeSignature timeSignature)
    {
        var matchingProgressions = chordProgressionDatas
            .Where(progression => progression.timeSignature == timeSignature)
            .ToList();

        if (matchingProgressions.Count == 0)
        {
            Debug.LogWarning($"No chord progression found for time signature: {timeSignature}. Picking a random progression.");
            return GetCompletelyRandomProgression();
        }

        return matchingProgressions[Random.Range(0, matchingProgressions.Count)];
    }

    /// <summary>
    /// Retrieves a Chord Progression based on a given Tonality.
    /// If multiple progressions exist, one is randomly chosen.
    /// If no match is found, a completely random progression is returned.
    /// </summary>
    public ChordProgressionData GetRandomProgressionByTonality(MusicTheory.Tonality tonality)
    {
        var matchingProgressions = chordProgressionDatas
            .Where(progression => progression.tonalities.Contains(tonality))
            .ToList();

        if (matchingProgressions.Count == 0)
        {
            Debug.LogWarning($"No chord progression found for tonality: {tonality}. Picking a random progression.");
            return GetCompletelyRandomProgression();
        }

        return matchingProgressions[Random.Range(0, matchingProgressions.Count)];
    }

    /// <summary>
    /// Retrieves a Chord Progression that matches both a given Time Signature and Tonality.
    /// If multiple progressions exist, one is randomly chosen.
    /// If no match is found, it first falls back to matching only Time Signature or Tonality.
    /// If still no match, it picks a completely random progression.
    /// </summary>
    public ChordProgressionData GetRandomProgressionByTimeSignatureAndTonality(
        MusicTheory.TimeSignature timeSignature, MusicTheory.Tonality tonality)
    {
        var matchingProgressions = chordProgressionDatas
            .Where(progression => progression.timeSignature == timeSignature &&
                                  progression.tonalities.Contains(tonality))
            .ToList();

        if (matchingProgressions.Count == 0)
        {
            Debug.LogWarning($"No chord progression found for time signature {timeSignature} and tonality {tonality}. Trying fallbacks.");

            // Fallback: Try by Time Signature only
            var timeSignatureFallback = GetRandomProgressionByTimeSignature(timeSignature);
            if (timeSignatureFallback != null) return timeSignatureFallback;

            // Fallback: Try by Tonality only
            var tonalityFallback = GetRandomProgressionByTonality(tonality);
            if (tonalityFallback != null) return tonalityFallback;

            // Ultimate Fallback: Pick any random progression
            return GetCompletelyRandomProgression();
        }

        return matchingProgressions[Random.Range(0, matchingProgressions.Count)];
    }

    /// <summary>
    /// Retrieves a completely random Chord Progression from the list.
    /// Used as a fallback when no specific match is found.
    /// </summary>
    private ChordProgressionData GetCompletelyRandomProgression()
    {
        if (chordProgressionDatas.Count == 0)
        {
            Debug.LogError("No chord progressions available!");
            return null;
        }
        return chordProgressionDatas[Random.Range(0, chordProgressionDatas.Count)];
    }
}
