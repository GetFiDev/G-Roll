using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Firebase;
using Firebase.Auth;
using Firebase.Functions;

public class CreateItemTool : MonoBehaviour
{
    [Title("Item Metadata")]
    [BoxGroup("General Info")]
    public string itemName = "itemname (demo)";
    [BoxGroup("General Info"), TextArea(2, 4)]
    public string itemDescription = "item description demo";
    [BoxGroup("General Info")]
    public string itemIconUrl = "https://cdn-icons-png.freepik.com/256/4957/4957671.png";

    [Title("Purchase Properties")]
    [BoxGroup("Prices")]
    public double itemPremiumPrice = 0;
    [BoxGroup("Prices")]
    public double itemGetPrice = 0.05;
    [BoxGroup("Prices")]
    public int itemReferralThreshold = 1;

    [BoxGroup("Flags")]
    public bool itemIsConsumable = false;
    [BoxGroup("Flags")]
    public bool itemIsRewardedAd = false;

    [Title("Item Stats")]
    [BoxGroup("Stats")]
    public double itemstat_coinMultiplierPercent = 0;
    [BoxGroup("Stats")]
    public double itemstat_comboPower = 0;
    [BoxGroup("Stats")]
    public double itemstat_gameplaySpeedMultiplierPercent = 0;
    [BoxGroup("Stats")]
    public double itemstat_magnetPowerPercent = 0;
    [BoxGroup("Stats")]
    public double itemstat_playerAcceleration = 0;
    [BoxGroup("Stats")]
    public double itemstat_playerSizePercent = 0;
    [BoxGroup("Stats")]
    public double itemstat_playerSpeed = 0;

    [Title("Server Call")]
    [Button(ButtonSizes.Large), GUIColor(0.2f, 0.8f, 1f)]
    public async void CreateItemOnServer()
    {
        await EnsureFirebaseReady();

        // IMPORTANT: Anonymous type YOK — Dictionary kullanıyoruz
        var data = new Dictionary<string, object>
        {
            { "itemName", itemName },
            { "itemDescription", itemDescription },
            { "itemIconUrl", itemIconUrl },
            { "itemPremiumPrice", itemPremiumPrice },
            { "itemGetPrice", itemGetPrice },
            { "itemReferralThreshold", itemReferralThreshold },
            { "itemIsConsumable", itemIsConsumable },
            { "itemIsRewardedAd", itemIsRewardedAd },
            { "itemstat_coinMultiplierPercent", itemstat_coinMultiplierPercent },
            { "itemstat_comboPower", itemstat_comboPower },
            { "itemstat_gameplaySpeedMultiplierPercent", itemstat_gameplaySpeedMultiplierPercent },
            { "itemstat_magnetPowerPercent", itemstat_magnetPowerPercent },
            { "itemstat_playerAcceleration", itemstat_playerAcceleration },
            { "itemstat_playerSizePercent", itemstat_playerSizePercent },
            { "itemstat_playerSpeed", itemstat_playerSpeed },
        };

        try
        {
            // Bölgeyi açık seçmek istersen:
            var functions = FirebaseFunctions.GetInstance("us-central1");
            var callable = functions.GetHttpsCallable("createItem");
            var result = await callable.CallAsync(data);

            if (result?.Data is IDictionary dict)
            {
                var ok = dict.Contains("ok") && dict["ok"] is bool b && b;
                if (ok)
                {
                    var id = dict.Contains("itemId") ? dict["itemId"] : "<none>";
                    var path = dict.Contains("path") ? dict["path"] : "<none>";
                    Debug.Log($"✅ Item created successfully!\nID: {id}\nPath: {path}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Server returned not-ok response: {Mini(dict)}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Unexpected response format from createItem");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ createItem failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task EnsureFirebaseReady()
    {
        // Dependencies
        await FirebaseApp.CheckAndFixDependenciesAsync();

        // Auth (anon)
        var auth = FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null)
        {
            Debug.Log("🔐 Signing in anonymously...");
            await auth.SignInAnonymouslyAsync();
            Debug.Log($"✅ Signed in as {auth.CurrentUser?.UserId}");
        }
    }

    // Küçük yardımcı: sözlüğü tek satırda yazdır
    private string Mini(IDictionary d)
    {
        var parts = new List<string>();
        foreach (DictionaryEntry e in d)
        {
            parts.Add($"{e.Key}:{e.Value}");
        }
        return "{" + string.Join(", ", parts) + "}";
    }
}