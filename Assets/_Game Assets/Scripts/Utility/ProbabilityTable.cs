using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ProbabilityTable<T> where T : UnityEngine.Object
{
    public IReadOnlyList<ProbabilityEntry<T>> Entries => entries;

    [SerializeField] private List<ProbabilityEntry<T>> entries = new();

    private float _totalWeight;
    private bool _isInitialized = false;

    public T GetRandom(ICollection<T> except = null)
    {
        if (!_isInitialized)
            Initialize();

        if (_totalWeight <= 0f || entries.Count == 0)
            return null;

        List<int> validIndices = new();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].probability <= 0f)
                continue;

            if (except != null && except.Contains(entries[i].data))
                continue;

            validIndices.Add(i);
        }

        if (validIndices.Count == 0)
            return null;

        var total = 0f;
        var cumulative = new float[validIndices.Count];
        for (var i = 0; i < validIndices.Count; i++)
        {
            total += entries[validIndices[i]].probability;
            cumulative[i] = total;
        }

        var random = UnityEngine.Random.Range(0f, total);

        for (var i = 0; i < cumulative.Length; i++)
        {
            if (random <= cumulative[i])
                return entries[validIndices[i]].data;
        }

        return null;
    }

    private void Initialize()
    {
        var count = entries.Count;
        _totalWeight = 0f;

        for (var i = 0; i < count; i++)
        {
            _totalWeight += entries[i].probability;
        }

        _isInitialized = true;
    }

    public void AddRange(IEnumerable<T> entryRange, float probability)
    {
        foreach (var entry in entryRange)
        {
            entries.Add(new ProbabilityEntry<T> { data = entry, probability = probability });
        }

        _isInitialized = false;
    }
    
    public ProbabilityTable<T> Clone()
    {
        var clone = new ProbabilityTable<T>();

        foreach (var entry in entries)
        {
            var newEntry = new ProbabilityEntry<T>
            {
                data = entry.data,
                probability = entry.probability
            };
            clone.entries.Add(newEntry);
        }

        clone._isInitialized = false;
        return clone;
    }
}