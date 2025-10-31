using TMPro;
using UnityEngine;
using System.Threading.Tasks;
using NetworkingData;
using System.Globalization;

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

    /// <summary>
    /// Tek giriş noktası: tüm statları yeniler. Başka yerden çağrı yok.
    /// </summary>
    public async void RefreshUserStats()
    {
        // Fetch panellerini aç
        SetFetchingPanelsActive(true);
        // Placeholder göster
        SetLoading();

        if (userDB == null)
        {
            SetUnavailable();
            SetFetchingPanelsActive(false);
            return;
        }

        try
        {
            // En güncel veriyi çekmeye çalış (LoadUserData mevcut kabul)
            var data = await userDB.LoadUserData();

            if (data == null)
            {
                SetUnavailable();
            }
            else
            {
                ApplyStats(data);
            }
        }
        finally
        {
            // İşlemler bittiğinde fetch panellerini kapat
            SetFetchingPanelsActive(false);
        }
    }

    private void ApplyStats(UserData data)
    {
        if (currencyTMP)
            currencyTMP.text = data.currency.ToString("0.00", CultureInfo.InvariantCulture);

        if (rankTMP)
            rankTMP.text = data.rank.ToString();

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
