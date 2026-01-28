namespace GRoll.Core.SceneManagement
{
    /// <summary>
    /// Oyundaki sahne tipleri
    /// </summary>
    public enum SceneType
    {
        /// <summary>Boot/Initialization sahnesi</summary>
        Boot = 0,

        /// <summary>Authentication sahnesi (Login/Profile)</summary>
        Auth = 1,

        /// <summary>Meta sahnesi (Ana menu, shop, inventory)</summary>
        Meta = 2,

        /// <summary>Gameplay sahnesi</summary>
        Gameplay = 3,

        /// <summary>Loading sahnesi (additive olarak kullanilir)</summary>
        Loading = 4
    }

    /// <summary>
    /// Loading ekrani tipleri
    /// </summary>
    public enum LoadingType
    {
        /// <summary>Sahne gecisi</summary>
        SceneTransition,

        /// <summary>Network operasyonu (auth, session, submit)</summary>
        NetworkOperation,

        /// <summary>Veri senkronizasyonu</summary>
        DataSync
    }
}
