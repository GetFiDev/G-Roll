using UnityEngine;
using TMPro;
using System;
using System.Threading.Tasks;

public class UIProfilePanel : MonoBehaviour
{
    [Header("Profile Fields")]
    [SerializeField] private TextMeshProUGUI emailText;    // Kullanıcının e‑posta adresi
    [SerializeField] private TextMeshProUGUI usernameText; // Kullanıcı adı
    [SerializeField] private TextMeshProUGUI currencyText; // 99.99 GET formatında bakiye

    private void OnEnable()
    {
        _ = RefreshProfileAsync();
    }

    /// <summary>
    /// UserDatabaseManager üzerinden kullanıcı verisini çekip UI'a uygular.
    /// </summary>
    private async Task RefreshProfileAsync()
    {
        try
        {
            var mgr = UserDatabaseManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[UIProfilePanel] UserDatabaseManager.Instance bulunamadı");
                ApplyToUI(null); // boşları çiz
                return;
            }

            // Öncelik: async yükleme
            object data = null;
            try
            {
                // Projendeki imza farklıysa derleme zamanı hatası olmasın diye object olarak tutuyoruz.
                // En yaygın senaryo: LoadUserData() -> UserData
                var maybeTask = mgr.LoadUserData();
                data = await WrapUnknownTask(maybeTask);
            }
            catch (Exception)
            {
                // Fallback: cached/current property varsa onu dene (reflection ile)
                data = TryGetProperty(mgr, "CurrentUserData") ?? TryGetProperty(mgr, "UserData") ?? null;
            }

            ApplyToUI(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIProfilePanel] RefreshProfileAsync error: {e.Message}");
            ApplyToUI(null);
        }
    }

    /// <summary>
    /// Elimizdeki data objesinden email/username/currency alanlarını okuyup UI'a basar.
    /// </summary>
    private void ApplyToUI(object data)
    {
        string email = ReadStringProp(data, "mail", "email");
        if (emailText) emailText.text = string.IsNullOrWhiteSpace(email) ? "-" : email;

        string uname = ReadStringProp(data, "username", "name", "userName");
        if (usernameText) usernameText.text = string.IsNullOrWhiteSpace(uname) ? "-" : uname;

        double currency = ReadDoubleProp(data, 0.0, "currency", "balance", "coins");
        if (currencyText) currencyText.text = $"{FormatCurrency(currency)} GET";
    }

    // ---- Helpers ----

    // Some managers dönebileceği Task<T> için, generic türü bilmeden object’e çözen yardımcı.
    private static async Task<object> WrapUnknownTask(object unknownTask)
    {
        if (unknownTask == null) return null;

        // Eğer zaten Task<object> ise direkt await et
        if (unknownTask is Task<object> tobj) return await tobj;

        // Task<T> ise dynamic await ile Result çek
        if (unknownTask is System.Threading.Tasks.Task t)
        {
            await t; // tamamlanmasını bekle
            var type = t.GetType();
            var prop = type.GetProperty("Result");
            return prop != null ? prop.GetValue(t) : null;
        }

        return unknownTask; // T olmayan bir şey döndüyse zaten object
    }

    private static object TryGetProperty(object owner, string propName)
    {
        if (owner == null) return null;
        var p = owner.GetType().GetProperty(propName);
        return p != null ? p.GetValue(owner) : null;
    }

    private static string ReadStringProp(object obj, params string[] names)
    {
        if (obj == null) return string.Empty;
        var type = obj.GetType();
        foreach (var n in names)
        {
            var p = type.GetProperty(n);
            if (p != null)
            {
                var v = p.GetValue(obj);
                if (v is string s) return s;
            }
        }
        return string.Empty;
    }

    private static double ReadDoubleProp(object obj, double def, params string[] names)
    {
        if (obj == null) return def;
        var type = obj.GetType();
        foreach (var n in names)
        {
            var p = type.GetProperty(n);
            if (p != null)
            {
                var v = p.GetValue(obj);
                if (v is double d) return d;
                if (v is float f) return (double)f;
                if (v is int i) return i;
                if (v is long l) return l;
                if (v is decimal m) return (double)m;
                // string ise parse etmeyi dene
                if (v is string s && double.TryParse(s, out var parsed)) return parsed;
            }
        }
        return def;
    }

    private static string FormatCurrency(double amount)
    {
        double rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded - Math.Round(rounded)) < 1e-9)
            return ((long)rounded).ToString(); // tam sayı ise .00 yazma
        return rounded.ToString("0.##"); // en fazla 2 ondalık
    }
}
