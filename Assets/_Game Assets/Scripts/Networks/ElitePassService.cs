using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

public class ElitePassService : MonoBehaviour
{
    private FirebaseFunctions _funcs;

    void Awake()
    {
        _funcs = FirebaseFunctions.DefaultInstance;
    }

    /// <summary>
    /// Satın alma: sunucu 30 gün ekler ve döndürür.
    /// purchaseId (Guid) verirsen idempotent olur.
    /// </summary>
    public async Task<(bool active, DateTime? expiresAtUtc)> PurchaseAsync(string purchaseId = null)
    {
        var callable = _funcs.GetHttpsCallable("purchaseElitePass");
        var data = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(purchaseId)) data["purchaseId"] = purchaseId;

        var result = await callable.CallAsync(data);
        var dict = result.Data as IDictionary;

        bool active = dict != null && dict["active"] is bool b && b;

        DateTime? expires = null;
        if (dict != null && dict["expiresAt"] != null)
        {
            // ISO 8601 -> UTC DateTime
            if (DateTime.TryParse(
                dict["expiresAt"].ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var dt))
            {
                expires = dt.ToUniversalTime();
            }
        }
        return (active, expires);
    }

    /// <summary>
    /// Kontrol: her “elite isteyen” aksiyon öncesi çağır.
    /// </summary>
    public async Task<(bool active, DateTime? expiresAtUtc)> CheckAsync()
    {
        var callable = _funcs.GetHttpsCallable("checkElitePass");
        var result = await callable.CallAsync(new Dictionary<string, object>());
        var dict = result.Data as IDictionary;

        bool active = dict != null && dict["active"] is bool b && b;

        DateTime? expires = null;
        if (dict != null && dict["expiresAt"] != null)
        {
            if (DateTime.TryParse(
                dict["expiresAt"].ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var dt))
            {
                expires = dt.ToUniversalTime();
            }
        }
        return (active, expires);
    }
}
