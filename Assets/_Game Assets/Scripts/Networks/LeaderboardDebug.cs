using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using NetworkingData; // LBEntry için

public class LeaderboardDebugger : MonoBehaviour
{
    [Header("Refs")]
    public LeaderboardManager leaderboard;          // Inspector’dan sürükle

    [Header("Options")]
    public KeyCode triggerKey = KeyCode.X;          // X'e basınca yazdır
    public bool refreshBeforePrint = true;          // Yazmadan önce fetch & cache?

    private bool _busy;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey) && !_busy)
        {
            _ = PrintAsync();
        }
    }

    private async Task PrintAsync()
    {
        if (leaderboard == null)
        {
            Debug.LogWarning("[LB Debug] LeaderboardManager ref missing");
            return;
        }

        _busy = true;
        try
        {
            if (refreshBeforePrint)
                await leaderboard.RefreshCacheAsync();   // fetch + cache (UserDatabaseManager üzerinden)

            var list = leaderboard.TopCached;
            if (list == null || list.Count == 0)
            {
                Debug.Log("[LB Debug] leaderboard empty");
                return;
            }

            var sb = new StringBuilder();

            // (opsiyonel) kendi sıralaman
            if (!string.IsNullOrEmpty(leaderboard.MyRankText))
                sb.AppendLine($"my-rank: {leaderboard.MyRankText}, my-score: {leaderboard.MyScore}");

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var name = string.IsNullOrWhiteSpace(e.username) ? "Guest" : e.username;
                sb.Append("rank: ").Append(i + 1)
                  .Append(", username: ").Append(name)
                  .Append(", score: ").Append(e.score)
                  .Append('\n');
            }

            Debug.Log(sb.ToString().TrimEnd());
        }
        finally
        {
            _busy = false;
        }
    }
}
