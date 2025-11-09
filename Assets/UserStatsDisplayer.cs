using TMPro;
using UnityEngine;
using System.Threading.Tasks;
using NetworkingData;
using System.Globalization;
using System;
using System.Linq;
using AssetKits.ParticleImage;

public class UserStatsDisplayer : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("UI: Stat Texts")]
    public TextMeshProUGUI currencyTMP;
    public TextMeshProUGUI rankTMP;
    public TextMeshProUGUI streakTMP;

    [Header("UI: Fetching Panels (per stat)")]
    public GameObject currencyFetchingPanel;
    public GameObject rankFetchingPanel;
    public GameObject streakFetchingPanel;

    [Header("FX")] public ParticleImage currencyGainFx;

    [Header("Persistence")]
    public bool persistLastCurrency = true;
    public string lastCurrencyPrefsKey = "UserStatsDisplayer.LastCurrency";

    [Header("Options")] public int leaderboardProbeLimit = 100; // callable snapshot limit

    private double LoadLastCurrency()
    {
        if (!persistLastCurrency) return 0d;
        if (PlayerPrefs.HasKey(lastCurrencyPrefsKey))
        {
            var s = PlayerPrefs.GetString(lastCurrencyPrefsKey, "0");
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        return 0d;
    }

    private void SaveLastCurrency(double value)
    {
        if (!persistLastCurrency) return;
        PlayerPrefs.SetString(lastCurrencyPrefsKey, value.ToString("F4", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private void MaybePlayCurrencyFx(double previous, double current)
    {
        // tolerance to avoid float noise
        const double EPS = 0.0001;
        if (currencyGainFx == null) return;
        if (current > previous + EPS)
        {
            try { currencyGainFx.Play(); } catch { }
        }
    }

    /// <summary>
    /// Tek giriş noktası: tüm statları yeniler. Başka yerden çağrı yok.
    /// </summary>
    public async void RefreshUserStats()
    {
        // Açılış: her panel için loading göster
        if (currencyTMP) currencyTMP.text = "…";
        if (rankTMP)     rankTMP.text     = "…";
        if (streakTMP)   streakTMP.text   = "…";

        if (currencyFetchingPanel) currencyFetchingPanel.SetActive(true);
        if (rankFetchingPanel)     rankFetchingPanel.SetActive(true);
        if (streakFetchingPanel)   streakFetchingPanel.SetActive(true);

        if (userDB == null)
        {
            // Hepsini kapat ve unavailable yaz
            SetUnavailable();
            SetFetchingPanelsActive(false);
            return;
        }

        // 1) Currency + Streak (UserData'dan)
        _ = RefreshCurrencyAndStreak();

        // 2) Rank (callable snapshot üzerinden index+1)
        _ = RefreshRankFromSnapshot();
    }

    private async Task RefreshCurrencyAndStreak()
    {
        try
        {
            var data = await userDB.LoadUserData();
            if (data == null)
            {
                if (currencyTMP) currencyTMP.text = "-";
                if (streakTMP)   streakTMP.text   = "-";
            }
            else
            {
                // compare & fx
                var prev = LoadLastCurrency();
                var curr = (double)data.currency;
                MaybePlayCurrencyFx(prev, curr);
                SaveLastCurrency(curr);

                if (currencyTMP) currencyTMP.text = data.currency.ToString("F2", CultureInfo.InvariantCulture);
                if (streakTMP)   streakTMP.text   = data.streak.ToString();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserStats] currency/streak load failed: {e.Message}");
            if (currencyTMP) currencyTMP.text = "-";
            if (streakTMP)   streakTMP.text   = "-";
        }
        finally
        {
            if (currencyFetchingPanel) currencyFetchingPanel.SetActive(false);
            if (streakFetchingPanel)   streakFetchingPanel.SetActive(false);
        }
    }

    private async Task RefreshRankFromSnapshot()
    {
        try
        {
            int limit = Mathf.Clamp(leaderboardProbeLimit, 1, 500);
            var page = await userDB.GetLeaderboardsSnapshotAsync(limit: limit, startAfterScore: null, includeSelf: true);

            string myUid = userDB.currentLoggedUserID;
            int rank = -1;

            if (!string.IsNullOrEmpty(myUid) && page != null && page.items != null)
            {
                int idx = page.items.FindIndex(e => e.uid == myUid);
                if (idx >= 0) rank = idx + 1; // local list sırası
            }

            if (rankTMP) rankTMP.text = (rank > 0) ? rank.ToString() : "-";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UserStats] rank fetch failed: {ex.Message}");
            if (rankTMP) rankTMP.text = "-";
        }
        finally
        {
            if (rankFetchingPanel) rankFetchingPanel.SetActive(false);
        }
    }

    private void ApplyStats(UserData data)
    {
        if (currencyTMP)
            currencyTMP.text = data.currency.ToString("F2", CultureInfo.InvariantCulture);

        if (rankTMP)
            rankTMP.text = "-"; // rank artık user doc'tan okunmuyor

        if (streakTMP)
            streakTMP.text = data.streak.ToString();
    }

    private void SetLoading()
    {
        if (currencyTMP) currencyTMP.text = "…";
        if (rankTMP)     rankTMP.text     = "…";
        if (streakTMP)   streakTMP.text   = "…";
    }

    private void SetUnavailable()
    {
        if (currencyTMP) currencyTMP.text = "-";
        if (rankTMP)     rankTMP.text     = "-";
        if (streakTMP)   streakTMP.text   = "-";
    }

    private void SetFetchingPanelsActive(bool active)
    {
        if (currencyFetchingPanel) currencyFetchingPanel.SetActive(active);
        if (rankFetchingPanel)     rankFetchingPanel.SetActive(active);
        if (streakFetchingPanel)   streakFetchingPanel.SetActive(active);
    }
}
