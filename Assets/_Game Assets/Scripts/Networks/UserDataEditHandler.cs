using System;
using System.Threading.Tasks;
using UnityEngine;
using NetworkingData; // UserData burada

public class UserDataEditHandler : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Sahnedeki UserDatabaseManager referansını ver")]
    public UserDatabaseManager userDB;

    /// <summary>
    /// Tüm UserData'yı döner (userDB.LoadUserData kullanır).
    /// </summary>
    public async Task<UserData> GetUserDataAsync()
    {
        if (userDB == null) { Debug.LogError("[UserDataEditHandler] userDB ref yok"); return null; }
        return await userDB.LoadUserData();
    }

    /// <summary>
    /// TEK METOT: UserData'yı yükler, 'mutate' ile değiştirir ve MergeAll ile kaydeder.
    /// userDB.SaveUserData'ı kullanır (Auth/DB erişimi bu sınıfta yok).
    /// </summary>
    /// <param name="mutate">UserData üzerinde yapmak istediğin değişiklikler</param>
    /// <param name="createIfMissing">Doküman yoksa default bir UserData yaratıp kaydetsin mi?</param>
    public async Task<bool> EditAsync(Action<UserData> mutate, bool createIfMissing = true)
    {
        if (userDB == null) { Debug.LogError("[UserDataEditHandler] userDB ref yok"); return false; }
        if (mutate == null) { Debug.LogError("[UserDataEditHandler] mutate null");   return false; }

        try
        {
            // 1) Oku
            var data = await userDB.LoadUserData();

            // 2) Yoksa oluştur (opsiyonel)
            if (data == null)
            {
                if (!createIfMissing)
                {
                    Debug.LogWarning("[UserDataEditHandler] UserData bulunamadı (createIfMissing=false).");
                    return false;
                }
                data = new UserData(); // default değerler (UserData initializer'ların)
            }

            // 3) Değiştir
            mutate(data);

            // 4) Kaydet (MergeAll)
            userDB.SaveUserData(data, merge: true);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[UserDataEditHandler] EditAsync hata: " + e.Message);
            return false;
        }
    }

    // ---- İsteğe bağlı kısa yardımcılar (tamamen syntactic sugar) ----

    public Task<bool> SetUsernameAsync(string newName)
        => EditAsync(u => u.username = newName ?? "");

    public Task<bool> AddCurrencyAsync(float delta)
        => EditAsync(u => u.currency += delta);

    public Task<bool> SetCurrencyAsync(float value)
        => EditAsync(u => u.currency = value);

    public Task<bool> TouchLastLoginAsync()
        => EditAsync(u => u.lastLogin = Firebase.Firestore.Timestamp.GetCurrentTimestamp());

    public Task<bool> SetHasElitePassAsync(bool v)
        => EditAsync(u => u.hasElitePass = v);

    public Task<bool> IncrementStreakAsync(int delta = 1)
        => EditAsync(u => u.streak += delta);

    public Task<bool> IncrementReferralsAsync(int delta = 1)
        => EditAsync(u => u.referrals += delta);

    public Task<bool> SetRankAsync(int value)
        => EditAsync(u => u.rank = value);
}
