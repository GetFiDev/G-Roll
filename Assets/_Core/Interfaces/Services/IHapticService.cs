namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Haptic (titreşim) feedback yönetimi için service interface.
    /// </summary>
    public interface IHapticService
    {
        /// <summary>
        /// Haptic feedback açık mı?
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Haptic feedback'i açar/kapatır.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Hafif titreşim (UI feedback için)
        /// </summary>
        void Light();

        /// <summary>
        /// Orta şiddette titreşim
        /// </summary>
        void Medium();

        /// <summary>
        /// Güçlü titreşim (önemli olaylar için)
        /// </summary>
        void Heavy();

        /// <summary>
        /// Başarı titreşimi
        /// </summary>
        void Success();

        /// <summary>
        /// Uyarı titreşimi
        /// </summary>
        void Warning();

        /// <summary>
        /// Hata titreşimi
        /// </summary>
        void Error();

        /// <summary>
        /// Seçim titreşimi (UI seçimleri için)
        /// </summary>
        void Selection();

        /// <summary>
        /// Özel süre ve yoğunlukta titreşim
        /// </summary>
        /// <param name="durationMs">Süre (milisaniye)</param>
        /// <param name="intensity">Yoğunluk (0-1)</param>
        void Custom(int durationMs, float intensity);
    }
}
