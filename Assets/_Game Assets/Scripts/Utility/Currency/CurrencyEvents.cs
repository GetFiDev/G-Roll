using UnityEngine;

public static class CurrencyEvents
{
    public static EventHub<CurrencyType, (int, Vector3)> OnRewarded = new();
}