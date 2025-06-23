using MidiGenPlay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/_Melody Pattern List")]
public class MelodyPatternsList : ScriptableObject
{
    public List<MelodyPatternData> melodyPatternList;

    /// <summary>
    /// Retrieves a Melody Pattern based on a given Time Signature.
    /// If multiple patterns exist, one is randomly chosen.
    /// </summary>
    public MelodyPatternData GetRandomPatternByTimeSignature(
        MusicTheory.TimeSignature timeSignature)
    {
        // Filter melody patterns that match the time signature
        var matchingPatterns = melodyPatternList
            .Where(pattern => pattern.timeSignature == timeSignature)
            .ToList();

        // If no patterns match, return null
        if (matchingPatterns.Count == 0)
        {
            Debug.LogWarning($"No melody pattern found for time signature: {timeSignature}");
            return null;
        }

        // Select a random pattern from the filtered list
        return matchingPatterns[Random.Range(0, matchingPatterns.Count)];
    }
}
