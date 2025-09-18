using TMPro;
using UnityEngine;
using System.Threading.Tasks;
using NetworkingData; // 👈 EKLENDİ

public class UserStatsDisplayer : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("UI")]
    public TextMeshProUGUI currencyStatTMP;
    public TextMeshProUGUI referralStatTMP;
    public TextMeshProUGUI streakStatTMP;
    public TextMeshProUGUI rankingStatTMP;
    public TextMeshProUGUI usernameTMP;

    private void OnEnable()
    {
        if (userDB != null)
        {
            userDB.OnLoginSucceeded += HandleLoginSucceeded;
            userDB.OnUserDataSaved  += HandleUserDataSaved;   // 👈 EKLENDİ
        }
    }

    private void OnDisable()
    {
        if (userDB != null)
        {
            userDB.OnLoginSucceeded -= HandleLoginSucceeded;
            userDB.OnUserDataSaved  -= HandleUserDataSaved;   // 👈 EKLENDİ
        }
    }

    private void HandleLoginSucceeded()
    {
        InitializeStatDisplays();
    }

    // 👇 YENİ: Save sonrası (ör. username set) tekrar tazele
    private void HandleUserDataSaved(UserData _)
    {
        InitializeStatDisplays();
    }

    public async void InitializeStatDisplays()
    {
        SetLoading();
        await RefreshAllStatsAsync();
    }

    private async Task RefreshAllStatsAsync()
    {
        if (userDB == null) { SetUnavailable(); return; }

        var data = await userDB.LoadUserData();
        if (data == null) { SetUnavailable(); return; }

        if (currencyStatTMP) currencyStatTMP.text = data.currency.ToString("0");
        if (referralStatTMP) referralStatTMP.text = data.referrals.ToString();
        if (streakStatTMP)   streakStatTMP.text   = data.streak.ToString();
        if (rankingStatTMP)  rankingStatTMP.text  = data.rank.ToString();
        if (usernameTMP)     usernameTMP.text     = string.IsNullOrWhiteSpace(data.username) ? "…" : data.username;
    }

    private void SetLoading()
    {
        if (currencyStatTMP) currencyStatTMP.text = "…";
        if (referralStatTMP) referralStatTMP.text = "…";
        if (streakStatTMP)   streakStatTMP.text   = "…";
        if (rankingStatTMP)  rankingStatTMP.text  = "…";
        if (usernameTMP)     usernameTMP.text     = "…";
    }

    private void SetUnavailable()
    {
        if (currencyStatTMP) currencyStatTMP.text = "-";
        if (referralStatTMP) referralStatTMP.text = "-";
        if (streakStatTMP)   streakStatTMP.text   = "-";
        if (rankingStatTMP)  rankingStatTMP.text  = "-";
        if (usernameTMP)     usernameTMP.text     = "-";
    }
}
