using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    public TextMeshProUGUI premiumCurrencyTMP;
    public TextMeshProUGUI streakTMP;

    [Header("UI: Premium Visuals")]
    [Tooltip("Sürükle: Currency plater Image")]
    public Image currencyPlaterImage;
    [Tooltip("Sürükle: Streak plater Image")]
    public Image streakPlaterImage;
    [Tooltip("Sürükle: Gem (Premium) plater Image")]
    public Image gemPlaterImage;
    [Tooltip("Sürükle: Profile Picture plater Image")]
    public Image profilePicturePlaterImage;
    
    [Space(10)]
    [Tooltip("Sürükle: Pro Badge Image (Açılıp kapanacak)")]
    public Image proBadgeImage;

    [Header("Sprites: Normal vs Premium")]
    public Sprite currencyPlaterNormal;
    public Sprite currencyPlaterPremium;
    
    public Sprite streakPlaterNormal;
    public Sprite streakPlaterPremium;
    
    public Sprite gemPlaterNormal;
    public Sprite gemPlaterPremium;
    
    public Sprite profilePicturePlaterNormal;
    public Sprite profilePicturePlaterPremium;

    [Header("FX")] public ParticleImage currencyGainFx;

    [Header("Persistence")]
    public bool persistLastCurrency = true;
    public string lastCurrencyPrefsKey = "UserStatsDisplayer.LastCurrency";
    
    // New keys for Premium Currency and Streak
    public string lastPremiumCurrencyPrefsKey = "UserStatsDisplayer.LastPremiumCurrency";
    public string lastStreakPrefsKey = "UserStatsDisplayer.LastStreak";

    [Header("Options")] public int leaderboardProbeLimit = 100; // callable snapshot limit

    // Coroutine references to manage animations cleanly
    private Coroutine _currencyPulseRoutine;
    private Coroutine _premiumCurrencyPulseRoutine;
    private Coroutine _streakPulseRoutine;
    
    // Animation Coroutines
    private Coroutine _currencyAnimRoutine;
    private Coroutine _premiumCurrencyAnimRoutine;
    private Coroutine _streakAnimRoutine;

    #region Persistence Helpers

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
        // Use "R" or "G17" for round-trip double precision to avoid "0.01" drift
        PlayerPrefs.SetString(lastCurrencyPrefsKey, value.ToString("G17", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private float LoadLastPremiumCurrency()
    {
        return PlayerPrefs.GetFloat(lastPremiumCurrencyPrefsKey, 0f); // Default to 0 if not found
    }

    private void SaveLastPremiumCurrency(float value)
    {
        PlayerPrefs.SetFloat(lastPremiumCurrencyPrefsKey, value);
        PlayerPrefs.Save();
    }

    private float LoadLastStreak()
    {
        return PlayerPrefs.GetFloat(lastStreakPrefsKey, 0f); // Default to 0 if not found
    }

    private void SaveLastStreak(float value)
    {
        PlayerPrefs.SetFloat(lastStreakPrefsKey, value);
        PlayerPrefs.Save();
    }
    
    #endregion

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
        // 1. Initialize text with LAST KNOWN values (Persistence) instead of 0
        double lastCurrency = LoadLastCurrency();
        float lastPremiumCurrency = LoadLastPremiumCurrency();
        float lastStreak = LoadLastStreak();
        
        SetText(currencyTMP, lastCurrency.ToString("F2", CultureInfo.InvariantCulture));
        SetText(premiumCurrencyTMP, lastPremiumCurrency.ToString("0", CultureInfo.InvariantCulture));
        SetText(streakTMP, lastStreak.ToString("0", CultureInfo.InvariantCulture));

        // 2. Start Pulse Animations to indicate "Fetching/Updating"
        StartPulse(currencyTMP, ref _currencyPulseRoutine);
        StartPulse(premiumCurrencyTMP, ref _premiumCurrencyPulseRoutine);
        StartPulse(streakTMP, ref _streakPulseRoutine);

        if (userDB == null)
        {
            StopAllPulses();
            return;
        }

        // 3. Fire Fetches (Fire-and-forget logic handles completion internally)
        _ = RefreshCurrencyAndStreak();
        // Rank fetch removed as it is replaced by Premium Currency which is in UserData
    }

    private void StopAllPulses()
    {
        StopPulse(currencyTMP, ref _currencyPulseRoutine);
        StopPulse(premiumCurrencyTMP, ref _premiumCurrencyPulseRoutine);
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

            if (data != null)
            {
                UpdatePremiumVisuals(data.hasElitePass);
            }


            // --- Handling Currency ---
            if (data != null)
            {
                double prev = LoadLastCurrency(); 
                double curr = (double)data.currency;
                
                // Only animate if changed. 
                // Delay: 0.75s if currency increased (to sync with potential FX), else 0.
                bool increased = curr > prev;
                float delay = increased ? 0.75f : 0f;

                // FX logic
                MaybePlayCurrencyFx(prev, curr);
                SaveLastCurrency(curr);

                StopPulse(currencyTMP, ref _currencyPulseRoutine);
                
                // Start animation from 'prev' to 'curr' - Use DOUBLE to avoid precision issues
                StartAnim(currencyTMP, prev, curr, 0.5f, delay, "F2", ref _currencyAnimRoutine);
            }
            else
            {
                StopPulse(currencyTMP, ref _currencyPulseRoutine);
                SetText(currencyTMP, "-");
            }

            // --- Handling Premium Currency ---
            if (data != null)
            {
                float prevPremium = LoadLastPremiumCurrency();
                float currPremium = (float)data.premiumCurrency;
                
                SaveLastPremiumCurrency(currPremium);
                
                StopPulse(premiumCurrencyTMP, ref _premiumCurrencyPulseRoutine);
                StartAnim(premiumCurrencyTMP, (double)prevPremium, (double)currPremium, 0.5f, 0f, "0", ref _premiumCurrencyAnimRoutine);
            }
            else
            {
                StopPulse(premiumCurrencyTMP, ref _premiumCurrencyPulseRoutine);
                SetText(premiumCurrencyTMP, "-");
            }

            // --- Handling Streak ---
            float targetStreak = 0;
            if (streakSnap.ok)
            {
                targetStreak = streakSnap.totalDays;
            }
            else if (data != null)
            {
                targetStreak = data.streak;
            }
            
            float prevStreak = LoadLastStreak();
            SaveLastStreak(targetStreak);
            
            StopPulse(streakTMP, ref _streakPulseRoutine);
            // No extra delay for streak
            StartAnim(streakTMP, (double)prevStreak, (double)targetStreak, 0.5f, 0f, "0", ref _streakAnimRoutine);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserStats] currency/streak load failed: {e.Message}");
            StopPulse(currencyTMP, ref _currencyPulseRoutine);
            StopPulse(premiumCurrencyTMP, ref _premiumCurrencyPulseRoutine);
            StopPulse(streakTMP, ref _streakPulseRoutine);
        }
    }

    // Rank fetch removed

    private void UpdatePremiumVisuals(bool isPremium)
    {
        // 1. Swap Plater Sprites
        if (currencyPlaterImage) 
            currencyPlaterImage.sprite = isPremium ? currencyPlaterPremium : currencyPlaterNormal;

        if (streakPlaterImage) 
            streakPlaterImage.sprite = isPremium ? streakPlaterPremium : streakPlaterNormal;

        if (gemPlaterImage) 
            gemPlaterImage.sprite = isPremium ? gemPlaterPremium : gemPlaterNormal;

        if (profilePicturePlaterImage) 
            profilePicturePlaterImage.sprite = isPremium ? profilePicturePlaterPremium : profilePicturePlaterNormal;

        // 2. Toggle Pro Badge
        if (proBadgeImage)
        {
            proBadgeImage.gameObject.SetActive(isPremium);
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
        if (this.isActiveAndEnabled)
        {
            routine = StartCoroutine(Co_PulseAlpha(tmp));
        }
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
        float speed = 5f; 
        while (true)
        {
            float val = Mathf.PingPong(Time.time * speed, 1f); 
            if (tmp) tmp.alpha = Mathf.Lerp(0.25f, 1f, val);
            yield return null;
        }
    }
    
    private void StartAnim(TextMeshProUGUI tmp, double startVal, double endVal, float duration, float delay, string format, ref Coroutine currRoutine)
    {
        if(tmp == null) return;
        if (currRoutine != null) StopCoroutine(currRoutine);
        if (this.isActiveAndEnabled)
        {
            currRoutine = StartCoroutine(AnimateCountUp(tmp, startVal, endVal, duration, delay, format));
        }
        else
        {
            // If inactive, just set the text immediately so it's correct when enabled
            tmp.text = endVal.ToString(format, CultureInfo.InvariantCulture);
        }
    }

    private IEnumerator AnimateCountUp(TextMeshProUGUI tmp, double startVal, double endVal, float duration, float delay, string format)
    {
        if (tmp == null) yield break;

        // Wait for delay if any
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            
            // Double interpolation: start + (end - start) * t
            double current = startVal + (endVal - startVal) * progress;
            tmp.text = current.ToString(format, CultureInfo.InvariantCulture);
            yield return null;
        }
        
        tmp.text = endVal.ToString(format, CultureInfo.InvariantCulture);
    }
}

