using MidiGenPlay;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/_Drum Pattern List")]
public class DrumPatternsList : ScriptableObject
{
    public List<DrumPatternData> drumPatternDatas;

    /// <summary>
    /// Retrieves a Drum Pattern based on a given Time Signature.
    /// If multiple patterns exist, one is randomly chosen.
    /// </summary>
    public DrumPatternData GetRandomPatternByTimeSignature(
        MusicTheory.TimeSignature timeSignature)
    {
        // Filter drum patterns that match the time signature
        var matchingPatterns = drumPatternDatas
            .Where(pattern => pattern.timeSignature == timeSignature)
            .ToList();

        // If no patterns match, return null
        if (matchingPatterns.Count == 0)
        {
            Debug.LogWarning($"No drum pattern found for time signature: {timeSignature}");
            return null;
        }

        // Select a random pattern from the filtered list
        return matchingPatterns[Random.Range(0, matchingPatterns.Count)];
    }
}
