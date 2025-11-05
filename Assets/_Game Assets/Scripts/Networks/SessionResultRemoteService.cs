using Firebase.Functions;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class SessionResultRemoteService
{
    public struct SubmitResponse
    {
        public bool alreadyProcessed;
        public double currency;
        public double maxScore;
    }

    public static async Task<SubmitResponse> SubmitAsync(string sessionId, double earnedCurrency, double earnedScore, double maxComboInSession, int playtimeSec, int powerUpsCollectedInSession)
    {
        var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable("submitSessionResult");
        var data = new Dictionary<string, object> {
            { "sessionId", sessionId },
            { "earnedCurrency", earnedCurrency },
            { "earnedScore", earnedScore },
            { "maxComboInSession", maxComboInSession },
            { "playtimeSec", playtimeSec },
            { "powerUpsCollectedInSession", powerUpsCollectedInSession },
        };
        var resp = await callable.CallAsync(data);
        var dict = resp.Data as IDictionary<string, object>;
        return new SubmitResponse {
            alreadyProcessed = dict != null && dict.ContainsKey("alreadyProcessed") && (bool)dict["alreadyProcessed"],
            currency = dict != null && dict.ContainsKey("currency") ? System.Convert.ToDouble(dict["currency"]) : 0,
            maxScore = dict != null && dict.ContainsKey("maxScore") ? System.Convert.ToDouble(dict["maxScore"]) : 0,
        };
    }
}