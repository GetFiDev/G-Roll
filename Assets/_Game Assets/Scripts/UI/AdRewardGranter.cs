using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetworkingData; // For UserData
using Firebase.Functions;
using Firebase.Extensions;
using System.Threading.Tasks;

public class AdRewardGranter : MonoBehaviour
{
    [Header("Reward Configuration")]
    [Tooltip("Amount of standard currency (Coins) to grant.")]
    [SerializeField] private int currencyAmount = 0;

    [Tooltip("Amount of premium currency (Gems) to grant.")]
    [SerializeField] private int premiumCurrencyAmount = 0;

    [Tooltip("Amount of energy to grant.")]
    [SerializeField] private int energyAmount = 0;

    /// <summary>
    /// Grants the configured rewards to the current user.
    /// Updates UserDatabaseManager which will trigger UI updates (UITopPanel etc).
    /// </summary>
    public void GrantReward()
    {
        var userDb = UserDatabaseManager.Instance;
        if (userDb == null)
        {
            Debug.LogError("[AdRewardGranter] UserDatabaseManager instance is null.");
            return;
        }

        if (userDb.currentUserData == null)
        {
            Debug.LogError("[AdRewardGranter] Current user data is null. Cannot grant reward.");
            return;
        }

        // We will modify a copy or the cached data directly? 
        // UserDatabaseManager.SaveUserData takes a UserData object and sends a patch.
        // It accepts `UserData` and merges fields.
        
        // Let's create a partial UserData to send as patch (only modified fields).
        // Actually SaveUserData implementation in UserDatabaseManager takes a full UserData object 
        // but strips server-only keys and constructs a patch dictionary internally.
        // So we should modify the current cached data and pass it?
        // Or create a new object with JUST the changes?
        // The signature is `SaveUserData(UserData data, bool merge = true)`.
        // If we pass a new UserData with just `currency` set, other fields might be null/default.
        // 'merge=true' implies we only update fields present in Firestore, but for the C# object...
        // Let's look at SaveUserData implementation again from previous context:
        /*
          public async void SaveUserData(UserData data, bool merge = true) {
             var patch = new Dictionary<string, object> {
                 { "currency",  data.currency }, ...
             };
          }
        */
        // It seemingly constructs the patch from the PASSED 'data' object's properties.
        // So we should pass a UserData object that has the VALID NEW TOTALS.
        
        UserData current = userDb.currentUserData;
        UserData updatedData = new UserData(); 
        
        // We need to carry over the 'base' values or just set what we change?
        // If we pass a new object, `currency` will be set, but `username` might be null.
        // SaveUserData implementation:
        // { "username", data.username ?? string.Empty }
        // If we pass null username, it writes empty string! That's bad.
        // SO: We must use the current cached object, modify it, and pass it.
        // Or clone it. For safety let's modify the values we need on the cached object? 
        // Modifying cached object directly is risky if Save fails? 
        // But UserDatabaseManager does optimistic updates anyway.
        
        // Better approach: Get current values, calculate new totals, call the specific `Set...` methods if available?
        // `SetCurrencyAsync` exists! 
        // `SetPremiumCurrencyAsync` IS MISSING in the provided snippet of UserDatabaseManager.cs.
        // We only saw `SetCurrencyAsync`.
        // Let's check if we can use `SaveUserData` carefully.
        
        // Strategy: 
        // 1. Clone critical fields from current data.
        // 2. Apply deltas.
        // 3. Call SaveUserData.
        
        updatedData.username = current.username;
        updatedData.mail = current.mail;
        updatedData.streak = current.streak;
        updatedData.referrals = current.referrals;
        
        if (currencyAmount > 0)
        {
            float currentVal = (float)current.currency;
            updatedData.currency = currentVal + currencyAmount;
            Debug.Log($"[AdRewardGranter] Granting {currencyAmount} Currency. New Total: {updatedData.currency}");
        }
        else
        {
             updatedData.currency = current.currency;
        }

        // Premium Currency? 
        // UserData definition wasn't fully visible but `SaveUserData` sends explicit fields.
        // The snippet for SaveUserData showed:
        /*
           var patch = ... { "currency", ... }
        */
        // It DID NOT show premiumCurrency in the `patch` dictionary in the snippet I read (Step 55).
        // Wait, let's re-read Step 55 `SaveUserData`.
        // Lines 454-460: 
        // { "mail", ... "username", ... "currency", ... "streak", ... "referralCount" ... }
        // IT DOES NOT SAVE PREMIUM CURRENCY! 
        // This implies Premium Currency is SERVER-AUTHORITATIVE Only? Or just missing from that method?
        // "currency" is there. "premiumCurrency" is missing.
        // 
        // IF Premium Currency is server-side only (likely for security), we cannot grant it via `SaveUserData`.
        // HOWEVER, the user asked: "energy, currency ve premium currency grant edildiğinde sunucuişini bitirdikten sonra uitoppanel güncellenecek"
        // This implies we rely on the server side to grant?
        // BUT `UIAdProduct.cs` calls `incrementAdUsage`. That only tracks usage.
        // 
        // IF we want to grant rewards, we should probably do it securely via a Cloud Function.
        // `incrementAdUsage` in `ad.functions.ts` ONLY increments usage count. It does NOT grant rewards.
        // 
        // OPTION 1: Update `incrementAdUsage` to ALSO grant the specific reward defined in Firestore?
        //   - User said: "uiadproductlar üzerlerine bir ad id string fieldı alır... buton atanabilecek bir field... butona basıldığında admanager rewarded reklam çağırır..."
        //   - AND "daily_limit" is checked.
        //   - For the Granter: "uiadproductlarının üzeirne ekleyebileceğim ad rewardgranter.cs...  uiadproduct'ın üzeirne de bir tane ad reward granter fieldı ekleki üzeirne sürükleyeyim... action butonuna basıldığında üzerine atılmış olan ad reward granterın grantreward metodunu çağıracak."
        // 
        // The user specifically asked for a CLIENT SIDE script `AdRewardGranter.cs` to grant rewards.
        // "grantreward metodunu çağıracak... sunucuişini bitirdikten sonra uitoppanel güncellenecek"
        // 
        // If I implement this purely client-side:
        // - Currency: Can set via `SaveUserData` (if I add it to the patch?) Or `SetCurrencyAsync`.
        // - Premium: Likely blocked by security rules/logic if not in `SaveUserData`.
        // - Energy: Controlled by `energy.functions`.
        // 
        // PERHAPS the intention is that `AdRewardGranter.cs` calls a CLOUD FUNCTION to grant these?
        // OR `UIAdProduct` calls `incrementAdUsage` (which I wrote) AND `AdRewardGranter` does... what?
        // 
        // If I write client code to "grant" premium currency, it will likely fail if the server doesn't accept it.
        // 
        // Let's look at `UserDatabaseManager.cs` again.
        // Is there a `SetPremium...`? No.
        // Is there a generic `GrantReward` cloud function? No.
        // 
        // SOLUTION:
        // I will assume for now that standard Currency is client-writable (as per `SetCurrencyAsync`).
        // For Premium and Energy, they usually require server authority.
        // 
        // If the user wants `AdRewardGranter.cs` component, I will implement it to call the Server via a new Cloud Function or generic `grantReward` function?
        // 
        // Wait, the user said: "sunucuişini bitirdikten sonra uitoppanel güncellenecek".
        // This strongly implies a Server Call is involved in the Granting process.
        // 
        // So `AdRewardGranter.cs` should probably call a Cloud Function like `grantAdReward`.
        // 
        // Let's modify `ad.functions.ts` to include a `grantAdReward` or modify `incrementAdUsage`?
        // But `AdRewardGranter` is a Unity Component with configurable fields. 
        // If we configure amount on Client, we are trusting the Client. 
        // "bizim seçtiklerimizin hepsinden yanlarına yazdığımız değer kadar versin."
        // Value is defined in Unity Inspector. So we SEND the amount to the server.
        // (Insecure but requested).
        // 
        // So `AdRewardGranter.cs` -> Call Cloud Function `grantManualReward` (or similar) with { currency, premium, energy }.
        // 
        // I need to add this function to `ad.functions.ts` (or `user.functions.ts`)?
        // User asked for `AdRewardGranter.cs` code primarily.
        // 
        // I will implement `AdRewardGranter.cs` to call a new Cloud Function `grantAdReward` which I will assume exists or I should add.
        // I'll add `grantReward` to `ad.functions.ts`.
        // 
        // Plan:
        // 1. Create `AdRewardGranter.cs`
        //    - Calls `functions.GetHttpsCallable("grantReward").CallAsync({ currency: x, premium: y, energy: z })`
        //    - On success: Client logic to refresh UI (load user data).
        // 2. Update `ad.functions.ts`
        //    - Add `grantReward`.
        
        _functions = FirebaseFunctions.GetInstance("us-central1");
    }

    private FirebaseFunctions _functions;

    private void Start()
    {
        _functions = FirebaseFunctions.GetInstance("us-central1");
    }

    public void GrantReward(System.Action onComplete = null)
    {
        if (_functions == null) _functions = FirebaseFunctions.GetInstance("us-central1");

        var data = new Dictionary<string, object>
        {
            { "currency", currencyAmount },
            { "premium", premiumCurrencyAmount },
            { "energy", energyAmount }
        };

        var func = _functions.GetHttpsCallable("grantReward");
        func.CallAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[AdRewardGranter] Failed to grant reward: {task.Exception}");
            }
            else
            {
                Debug.Log($"[AdRewardGranter] Reward granted: C={currencyAmount}, P={premiumCurrencyAmount}, E={energyAmount}");
                // Refresh UI
                if (UserDatabaseManager.Instance != null)
                {
                    // Trigger a reload to get fresh server values (sync) and update UI
                    _ = UserDatabaseManager.Instance.RefreshUserData(); 
                    
                    // Direct UI Refresh as requested
                    if (UITopPanel.Instance != null)
                    {
                        UITopPanel.Instance.Initialize();
                    } 
                }
            }
            onComplete?.Invoke();
        });
    }
}
