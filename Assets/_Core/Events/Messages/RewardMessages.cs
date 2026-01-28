using GRoll.Core;
using GRoll.Core.Events;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Achievement ödülü talep edildiğinde publish edilir.
    /// CurrencyService bu mesajı dinleyerek ödülü işler.
    /// Bu sayede AchievementService → CurrencyService circular dependency önlenir.
    /// </summary>
    public readonly struct RewardRequestedMessage : IMessage
    {
        /// <summary>Ödül kaynağı (örn: "achievement_first_win")</summary>
        public string Source { get; }

        /// <summary>Ödül tipi</summary>
        public CurrencyType CurrencyType { get; }

        /// <summary>Ödül miktarı</summary>
        public int Amount { get; }

        /// <summary>İşlemi başlatan operation ID (rollback için)</summary>
        public string OperationId { get; }

        public RewardRequestedMessage(string source, CurrencyType currencyType, int amount, string operationId)
        {
            Source = source;
            CurrencyType = currencyType;
            Amount = amount;
            OperationId = operationId;
        }
    }

    /// <summary>
    /// Ödül işlemi tamamlandığında publish edilir.
    /// AchievementService bu mesajı dinleyerek işlemin sonucunu öğrenir.
    /// </summary>
    public readonly struct RewardProcessedMessage : IMessage
    {
        /// <summary>İşlemi başlatan operation ID</summary>
        public string OperationId { get; }

        /// <summary>İşlem başarılı mı?</summary>
        public bool IsSuccess { get; }

        /// <summary>Hata mesajı (başarısızsa)</summary>
        public string ErrorMessage { get; }

        public RewardProcessedMessage(string operationId, bool isSuccess, string errorMessage = null)
        {
            OperationId = operationId;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}
