using System;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Gameplay hız, booster ve combo yönetimi servisi.
    /// Eski GameplayLogicApplier'ın tüm fonksiyonlarının yerini alır.
    /// </summary>
    public interface IGameplaySpeedService
    {
        #region Speed Properties

        /// <summary>Başlangıç hızı</summary>
        float StartSpeed { get; }

        /// <summary>Maksimum hız</summary>
        float MaxSpeed { get; }

        /// <summary>Anlık hesaplanmış hız</summary>
        float CurrentSpeed { get; }

        /// <summary>Gameplay hız çarpanı (zone'lar, booster vs.)</summary>
        float SpeedMultiplierGameplay { get; }

        /// <summary>Player hız çarpanı (stat bonusları)</summary>
        float SpeedMultiplierPlayer { get; }

        /// <summary>Toplam hız çarpanı (Gameplay * Player)</summary>
        float TotalSpeedMultiplier { get; }

        /// <summary>Player hız çarpanı (legacy uyumluluk - SpeedMultiplierPlayer ile aynı)</summary>
        float PlayerSpeedMultiplier { get; }

        #endregion

        #region Booster Properties

        /// <summary>Booster aktif mi?</summary>
        bool IsBoosterActive { get; }

        /// <summary>Booster doluluk oranı (0-1)</summary>
        float BoosterFill { get; }

        /// <summary>Booster süresi (saniye)</summary>
        float BoosterDuration { get; }

        /// <summary>Booster hız çarpanı</summary>
        float BoosterSpeedMultiplier { get; }

        #endregion

        #region Combo Properties

        /// <summary>Mevcut combo gücü</summary>
        int CurrentComboPower { get; }

        /// <summary>Combo çarpanı (combo power'a dayalı)</summary>
        float ComboMultiplier { get; }

        /// <summary>Baz combo gücü (başlangıç değeri)</summary>
        int BaseComboPower { get; }

        #endregion

        #region Events

        /// <summary>Player hız çarpanı değiştiğinde</summary>
        event Action<float> OnPlayerSpeedMultiplierChanged;

        /// <summary>Gameplay hız çarpanı değiştiğinde</summary>
        event Action<float> OnGameplaySpeedMultiplierChanged;

        /// <summary>Booster aktifleştiğinde</summary>
        event Action OnBoosterActivated;

        /// <summary>Booster bittiğinde</summary>
        event Action OnBoosterDeactivated;

        /// <summary>Booster doluluk değiştiğinde</summary>
        event Action<float> OnBoosterFillChanged;

        /// <summary>İlk hareket yapıldığında (kamera için)</summary>
        event Action OnFirstPlayerMove;

        /// <summary>Combo gücü değiştiğinde</summary>
        event Action<int> OnComboPowerChanged;

        /// <summary>Combo sıfırlandığında</summary>
        event Action OnComboReset;

        #endregion

        #region Methods

        /// <summary>Gameplay hız çarpanını ayarla</summary>
        void SetGameplaySpeedMultiplier(float multiplier);

        /// <summary>Player hız çarpanını ayarla</summary>
        void SetPlayerSpeedMultiplier(float multiplier);

        /// <summary>Booster'ı aktifleştir</summary>
        void ActivateBooster();

        /// <summary>Booster doluluk ekle</summary>
        void AddBoosterFill(float amount);

        /// <summary>Booster'ı sıfırla</summary>
        void ResetBooster();

        /// <summary>Tüm değerleri sıfırla (yeni session için)</summary>
        void Reset();

        /// <summary>İlk hareket bildirimini tetikle</summary>
        void NotifyFirstPlayerMove();

        /// <summary>Update döngüsü (booster timer vs.)</summary>
        void Tick(float deltaTime);

        /// <summary>Baz combo gücünü ayarla</summary>
        void SetBaseComboPower(int power);

        /// <summary>Combo'ya ekleme yap</summary>
        void AddCombo(int amount);

        /// <summary>Combo'yu sıfırla</summary>
        void ResetCombo();

        #endregion
    }
}
