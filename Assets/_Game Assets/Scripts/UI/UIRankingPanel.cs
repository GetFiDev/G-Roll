using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Ranking panel: açılırken LeaderboardService'ten veriyi çeker,
/// listeyi kurar ve self header'ı AYNI KAYNAKTAN besler.
///
/// Kurallar:
/// - Row arka plan çerçevesi sıraya/elite'e göre değişir.
/// - Self header'da arka plan çerçevesi DEĞİŞMEZ (lockFrame=true),
///   ama rank plate ELITE durumuna göre değişir.
/// - Self header, plate sprite referansları boşsa row prefab'tan inject edilir.
/// </summary>
public class UIRankingPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private UILeaderboardDisplay rowPrefab;
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private UILeaderboardDisplay selfDisplay; // üstteki kendini gösteren satır
    [SerializeField] private GameObject fetchingPanel;         // opsiyonel: yükleniyor overlay

    [Header("Options")]
    [SerializeField] private int maxRows = 100;

    private CancellationTokenSource _cts;

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        _ = LoadAsync(_cts.Token); // fire-and-forget
    }

    private void OnDisable()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            if (fetchingPanel) fetchingPanel.SetActive(true);

            // 1) Veriyi tek kapıdan çek
            var result = await LeaderboardService.FetchAsync(ct: ct); // Result: items (List<Item>), self (nullable / optional)

            // 2) Listeyi kur
            RebuildRows(result);

            // 3) Self header’ı AYNI veriden besle
            BindSelfDisplay(result);
        }
        catch (System.OperationCanceledException)
        {
            // panel kapanmış olabilir; yok say
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[UIRankingPanel] LoadAsync EX: {ex.Message}");
        }
        finally
        {
            if (fetchingPanel) fetchingPanel.SetActive(false);
        }
    }

    private void RebuildRows(LeaderboardService.Result result)
    {
        if (!rowsRoot || rowPrefab == null) return;

        // Temizle
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);

        var items = result.Items;
        if (items == null) return;

        long nowMillis = result.ServerNowMillis;

        int count = Mathf.Min(maxRows, items.Count);
        int rankOffset = result.RankOffset; // service verdi
        for (int i = 0; i < count; i++)
        {
            var it = items[i];
            var go = Instantiate(rowPrefab, rowsRoot);
            var ui = go.GetComponent<UILeaderboardDisplay>();

            int rankNumber = rankOffset + i + 1;
            bool isTop3 = rankNumber >= 1 && rankNumber <= 3;
            bool hasElite = it.HasElite(nowMillis);

            ui.SetLockFrame(false);
            ui.SetData(
                rankText: rankNumber.ToString(),
                username: SafeUserName(it.Username),
                score: it.Score,
                isTop3: isTop3,
                hasElite: hasElite
            );
        }
    }

    private void BindSelfDisplay(LeaderboardService.Result result)
    {
        if (selfDisplay == null) return;
        LeaderboardService.Item? me = null;
        if (result.Me.HasValue)
        {
            me = result.Me.Value;
        }

        // 2) Self header’ın frame’i SABİT (değişmeyecek)
        selfDisplay.SetLockFrame(true);

        // 3) Rank Plate sprite güvence:
        //    Inspector’da self header’ın plate sprite’ları boş olabilir.
        //    Row prefab’taki plate sprite’ları burada enjekte ederek garanti ediyoruz.
        if (rowPrefab != null)
        {
            selfDisplay.EnsureRankPlateSprites(
                rowPrefab.GetRankPlateDefault(),
                rowPrefab.GetRankPlateElite()
            );
        }

        string rankText = "";
        string username = "You";
        int score = 0;
        bool isTop3 = false;
        bool hasElite = false;

        if (me.HasValue)
        {
            var m = me.Value;
            username = SafeUserName(m.Username);
            score = m.Score;
            hasElite = m.HasElite(result.ServerNowMillis);
            Debug.Log($"[UIRankingPanel] Self baseline hasElite from Me: {hasElite}");

            // Eğer kendi satırı listede görünüyorsa, sırayı hesapla
            int indexInPage = -1;
            if (result.Items != null)
            {
                for (int i = 0; i < result.Items.Count; i++)
                {
                    if (result.Items[i].Uid == m.Uid)
                    {
                        indexInPage = i;
                        break;
                    }
                }
            }
            if (indexInPage >= 0)
            {
                // Self satırı listedeyse, elite durumunu listeden de çek (bazı backendlerde Me eksik/gecikmeli olabilir)
                var pageSelf = result.Items[indexInPage];
                bool hasEliteFromPage = pageSelf.HasElite(result.ServerNowMillis);
                if (hasElite != hasEliteFromPage)
                {
                    Debug.LogWarning($"[UIRankingPanel] Me.HasElite({result.ServerNowMillis})={hasElite} but Items[{indexInPage}].HasElite={hasEliteFromPage}. Preferring page value.");
                }
                hasElite = hasEliteFromPage;

                int rankNumber = result.RankOffset + indexInPage + 1;
                rankText = rankNumber.ToString();
                isTop3 = rankNumber >= 1 && rankNumber <= 3;
            }
            else
            {
                // Sayfada yoksa, rank bilinmiyor; boş bırak.
                rankText = "";
                isTop3 = false;
            }
        }

        // Plate, hasElite’e göre kesin değişir; frame sabit kalır.
        selfDisplay.SetData(rankText, username, score, isTop3, hasElite);
    }

    private string SafeUserName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Player";
        return s.Length > 20 ? s.Substring(0, 20) : s;
    }
}