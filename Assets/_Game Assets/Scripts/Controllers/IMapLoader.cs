public interface IMapLoader
{
    bool IsReady { get; }
    event System.Action OnReady;
    // Artık parametre yok; MapManager kendi kaynağından yükler.
    void Load();
    // Tüm inşa edilmiş map GO’larını temizler, state’i sıfırlar.
    void Unload();
}