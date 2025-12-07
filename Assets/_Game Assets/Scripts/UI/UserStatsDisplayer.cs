using TMPro;
using UnityEngine;
using System.Threading.Tasks;
using NetworkingData;
using System.Globalization;
using System;
using System.Linq;
using System.Collections;
using AssetKits.ParticleImage;

public class UserStatsDisplayer : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("UI: Stat Texts")]
    public TextMeshProUGUI currencyTMP;
    public TextMeshProUGUI rankTMP;
    public TextMeshProUGUI streakTMP;

    [Header("FX")] public ParticleImage currencyGainFx;

    [Header("Persistence")]
    public bool persistLastCurrency = true;
    public string lastCurrencyPrefsKey = "UserStatsDisplayer.LastCurrency";

    [Header("Options")] public int leaderboardProbeLimit = 100; // callable snapshot limit

    // Coroutine references to manage animations cleanly
    private Coroutine _currencyPulseRoutine;
    private Coroutine _rankPulseRoutine;
    private Coroutine _streakPulseRoutine;
    private Coroutine _referralPulseRoutine;

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
        const double EPS = 0.0001;
        if (currencyGainFx == null) return;
        if (current > previous + EPS)
        {
            try { currencyGainFx.Play(); } catch { }
        }
    }

    /// <summary>
    /// Tek giriş noktası: tüm statları yeniler.
    /// </summary>
    public void RefreshUserStats()
    {
        // 1. Reset values to "0" (or previous known for currency if desired, but request implies 0 base for fetch)
        // Request says: "fetch olurken 0 görünecek... rakamlar fetchlendiğinde 0'dan hedef değere... yükselecekler"
        // So we reset to 0 visually and start pulse.
        
        SetText(currencyTMP, "0.00");
        SetText(rankTMP, "0");
        SetText(streakTMP, "0");

        // 2. Start Pulse Animations
        StartPulse(currencyTMP, ref _currencyPulseRoutine);
        StartPulse(rankTMP, ref _rankPulseRoutine);
        StartPulse(streakTMP, ref _streakPulseRoutine);

        if (userDB == null)
        {
            StopAllPulses();
            return;
        }

        // 3. Fire Fetches (Fire-and-forget logic handles completion internally)
        _ = RefreshCurrencyAndStreak();
        _ = RefreshRankFromSnapshot();
    }

    private void StopAllPulses()
    {
        StopPulse(currencyTMP, ref _currencyPulseRoutine);
        StopPulse(rankTMP, ref _rankPulseRoutine);
        StopPulse(streakTMP, ref _streakPulseRoutine);
    }

    private async Task RefreshCurrencyAndStreak()
    {
        try
        {
            var userDataTask = userDB.LoadUserData();
            var streakTask = StreakService.FetchAsync();

            await Task.WhenAll(userDataTask, streakTask);

            var data = userDataTask.Result;
            var streakSnap = streakTask.Result;

            // --- Handling Currency ---
            if (data != null)
            {
                var prev = LoadLastCurrency(); // We might use this for FX, but visual anim starts from 0 as requested
                var curr = (double)data.currency;
                
                // FX check logic remains based on PREVIOUS session value for consistency
                MaybePlayCurrencyFx(prev, curr);
                SaveLastCurrency(curr);

                StopPulse(currencyTMP, ref _currencyPulseRoutine);
                StartCoroutine(AnimateCountUp(currencyTMP, 0f, (float)curr, 0.5f, "F2"));
            }
            else
            {
                StopPulse(currencyTMP, ref _currencyPulseRoutine);
                SetText(currencyTMP, "-");
            }

            // --- Handling Streak ---
            double targetStreak = 0;
            if (streakSnap.ok)
            {
                targetStreak = streakSnap.totalDays;
            }
            else if (data != null)
            {
                targetStreak = data.streak;
            }
            
            StopPulse(streakTMP, ref _streakPulseRoutine);
            StartCoroutine(AnimateCountUp(streakTMP, 0f, (float)targetStreak, 0.5f, "0"));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserStats] currency/streak load failed: {e.Message}");
            StopPulse(currencyTMP, ref _currencyPulseRoutine);
            StopPulse(streakTMP, ref _streakPulseRoutine);
        }
    }

    private async Task RefreshRankFromSnapshot()
    {
        try
        {
            int limit = Mathf.Clamp(leaderboardProbeLimit, 1, 500);
            var page = await userDB.GetLeaderboardsSnapshotAsync(limit: limit, startAfterScore: null, includeSelf: true);

            string myUid = userDB.currentLoggedUserID;
            int rank = 0;

            if (!string.IsNullOrEmpty(myUid) && page != null && page.items != null)
            {
                int idx = page.items.FindIndex(e => e.uid == myUid);
                if (idx >= 0) rank = idx + 1; 
            }

            StopPulse(rankTMP, ref _rankPulseRoutine);
            StartCoroutine(AnimateCountUp(rankTMP, 0f, (float)rank, 0.5f, "0"));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UserStats] rank fetch failed: {ex.Message}");
            StopPulse(rankTMP, ref _rankPulseRoutine); 
        }
    }

    // --- Helpers ---

    private void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp) tmp.text = text;
    }

    // --- Animation Logic ---

    private void StartPulse(TextMeshProUGUI tmp, ref Coroutine routine)
    {
        if (tmp == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Co_PulseAlpha(tmp));
    }

    private void StopPulse(TextMeshProUGUI tmp, ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        // Ensure alpha is back to 1
        if (tmp != null) tmp.alpha = 1f;
    }

    private IEnumerator Co_PulseAlpha(TextMeshProUGUI tmp)
    {
        // "alfları 0.25, 1 arasında hızlıca gidip gelecek"
        float speed = 5f; // Adjust for "hızlıca" feel
        while (true)
        {
            float val = Mathf.PingPong(Time.time * speed, 1f); 
            // PingPong returns 0..1. Map 0..1 to 0.25..1.0
            // Lerp(0.25, 1.0, val)
            if (tmp) tmp.alpha = Mathf.Lerp(0.25f, 1f, val);
            yield return null;
        }
    }

    private IEnumerator AnimateCountUp(TextMeshProUGUI tmp, float startVal, float endVal, float duration, string format)
    {
        if (tmp == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            
            // Optional: EaseOut
            // progress = Mathf.Sin(progress * Mathf.PI * 0.5f);

            float current = Mathf.Lerp(startVal, endVal, progress);
            tmp.text = current.ToString(format, CultureInfo.InvariantCulture);
            yield return null;
        }
        
        tmp.text = endVal.ToString(format, CultureInfo.InvariantCulture);
    }
}

