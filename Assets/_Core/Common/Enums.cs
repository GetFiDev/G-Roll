namespace GRoll.Core
{
    /// <summary>
    /// Para birimi tipleri
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>Soft currency (Coin)</summary>
        SoftCurrency = 0,

        /// <summary>Hard currency (Gem)</summary>
        HardCurrency = 1
    }

    /// <summary>
    /// Oyun fazları
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Boot/Loading fazı</summary>
        Boot = 0,

        /// <summary>Meta/Menu fazı</summary>
        Meta = 1,

        /// <summary>Gameplay fazı</summary>
        Gameplay = 2
    }

    /// <summary>
    /// Session state'leri
    /// </summary>
    public enum SessionState
    {
        /// <summary>Session yok</summary>
        None,

        /// <summary>Session başlatılıyor (server'dan token alınıyor)</summary>
        Requesting,

        /// <summary>Session aktif</summary>
        Active,

        /// <summary>Session sonuçları gönderiliyor</summary>
        Submitting,

        /// <summary>Session tamamlandı</summary>
        Completed,

        /// <summary>Session hata ile sonlandı</summary>
        Failed,

        /// <summary>Session iptal edildi</summary>
        Cancelled
    }

    /// <summary>
    /// Oyun modları
    /// </summary>
    public enum GameMode
    {
        Classic,
        TimeAttack,
        Endless
    }
}
