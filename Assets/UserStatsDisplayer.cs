using TMPro;
using UnityEngine;
using System.Threading.Tasks;

public class UserStatsDisplayer : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("UI")]
    public TextMeshProUGUI currencyStatTMP;
    public TextMeshProUGUI referralStatTMP;
    public TextMeshProUGUI streakStatTMP;
    public TextMeshProUGUI rankingStatTMP;

    private void OnEnable()
    {
        if (userDB != null)
            userDB.OnLoginSucceeded += HandleLoginSucceeded;
    }

    private void OnDisable()
    {
        if (userDB != null)
            userDB.OnLoginSucceeded -= HandleLoginSucceeded;
    }

    private void HandleLoginSucceeded()
    {
        // Login biter bitmez HUD’u tazele
        InitializeStatDisplays();
    }

    /// <summary>
    /// HUD’u elle tazelemek için çağır.
    /// </summary>
    public async void InitializeStatDisplays()
    {
        SetLoading();
        await RefreshAllStatsAsync();
    }

    private async Task RefreshAllStatsAsync()
    {
        if (userDB == null)
        {
            SetUnavailable();
            return;
        }

        var data = await userDB.LoadUserData(); // Firestore -> UserData (POCO)
        if (data == null)
        {
            SetUnavailable();
            return;
        }

        // — Currency (float)
        if (currencyStatTMP) currencyStatTMP.text = data.currency.ToString("0");

        if (referralStatTMP) referralStatTMP.text = data.referrals.ToString();
        if (streakStatTMP)   streakStatTMP.text   = data.streak.ToString();
        if (rankingStatTMP)  rankingStatTMP.text  = data.rank.ToString();
    }

    private void SetLoading()
    {
        if (currencyStatTMP) currencyStatTMP.text = "…";
        if (referralStatTMP) referralStatTMP.text = "…";
        if (streakStatTMP)   streakStatTMP.text   = "…";
        if (rankingStatTMP)  rankingStatTMP.text  = "…";
    }

    private void SetUnavailable()
    {
        if (currencyStatTMP) currencyStatTMP.text = "-";
        if (referralStatTMP) referralStatTMP.text = "-";
        if (streakStatTMP)   streakStatTMP.text   = "-";
        if (rankingStatTMP)  rankingStatTMP.text  = "-";
    }
}
