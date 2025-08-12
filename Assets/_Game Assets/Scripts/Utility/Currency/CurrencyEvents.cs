using UnityEngine;

public static class CurrencyEvents
{
    public static readonly EventHub<CurrencyType, CurrencyCollectedData> OnCollected = new();
}

public struct CurrencyCollectedData
{
    public int Amount { get; }
    public Vector3 Position { get; }

    public CurrencyCollectedData(int amount, Vector3 position)
    {
        Amount = amount;
        Position = position;
    }
}