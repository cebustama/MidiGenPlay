using MidiGenPlay;

using System;
using System.Collections.Generic;
using System.Linq;

using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// Registry + selector
public static class RhythmStyleRegistry
{
    private static readonly Dictionary<TimeSignature, List<IRhythmStyle>> _byMeter = new();

    public static void Register(IRhythmStyle style)
    {
        if (!_byMeter.TryGetValue(style.Meter, out var list))
            _byMeter[style.Meter] = list = new List<IRhythmStyle>();
        if (!list.Any(s => s.Id == style.Id)) list.Add(style);
    }

    public static void RegisterDefaults()
    {
        Register(new RockBackbeat4_4Style());
        Register(new Waltz3_4Style());
        Register(new Shuffle6_8Style());
        Register(new Backbeat5_4Style());
    }

    public static IRhythmStyle Choose(
        TimeSignature ts,
        RhythmRecipe recipe,
        Func<int, int, int> rng) // pass () => UnityEngine.Random.Range(0, n) or other RNG
    {
        if (!_byMeter.TryGetValue(ts, out var list) || list.Count == 0) return null;

        // explicit hint wins
        var hint = recipe != null ? recipe.RhythmStyleId : null;
        if (!string.IsNullOrEmpty(hint))
        {
            var chosen = list.FirstOrDefault(s => s.Id == hint);
            if (chosen != null) return chosen;
        }

        // (MVP) base weights only; later blend with recipe/personality weights
        var weights = list.Select(s => Math.Max(0.001f, s.BaseWeight)).ToArray();
        float total = weights.Sum();
        float pick = (float)rng(0, 100000) / 100000f * total;

        for (int i = 0; i < list.Count; i++)
        {
            if (pick <= weights[i]) return list[i];
            pick -= weights[i];
        }
        return list[^1];
    }
}
