using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class UpgradeData<T> where T : struct, IConvertible, IComparable, IFormattable
{
    public virtual int CurrentLevel { get; protected set; }
    public virtual int MaxLevel => levelValues.Count;

    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    [SerializeField] private List<T> levelValues = new();

    public T GetCurrentValue()
    {
        if (levelValues.Count == 1)
            return levelValues.First();
        
        return levelValues[CurrentLevel];
    }
    
    public T GetLevelValue(int requestedLevel)
    {
        if (requestedLevel < 0 || requestedLevel >= MaxLevel)
        {
            throw new ArgumentOutOfRangeException($"Requested level {requestedLevel} is out of range.");
        }
        
        return levelValues[requestedLevel];
    }

    public void UpgradeLevel()
    {
        if (IsMaxLevel)
        {
            throw new NotSupportedException($"Already Upgraded to Max Level, Can't Upgrade !");
        }

        CurrentLevel++;
    }
    
    public void UpgradeToLevel(int level)
    {
        if (IsMaxLevel)
            throw new NotSupportedException($"Already Upgraded to Max Level, Can't Upgrade !");
        
        if (level >= MaxLevel || level < 0)
            throw new ArgumentOutOfRangeException($"Requested level {level} is out of range.");

        CurrentLevel = level;
    }
    
    public UpgradeData<T> CloneAndResetProgress()
    {
        CurrentLevel = 0;
        return MemberwiseClone() as UpgradeData<T>;
    }
}