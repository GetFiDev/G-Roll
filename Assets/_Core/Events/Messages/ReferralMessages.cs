namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Referral cache guncellendikten sonra yayinlanan message
    /// </summary>
    public readonly struct ReferralCacheUpdatedMessage : IMessage
    {
        public int TotalCount { get; }
        public decimal PendingTotal { get; }

        public ReferralCacheUpdatedMessage(int totalCount, decimal pendingTotal)
        {
            TotalCount = totalCount;
            PendingTotal = pendingTotal;
        }
    }

    /// <summary>
    /// Referral earnings claim edildikten sonra yayinlanan message
    /// </summary>
    public readonly struct ReferralEarningsClaimedMessage : IMessage, IOptimisticMessage
    {
        public decimal Amount { get; }
        public bool IsOptimistic { get; }

        public ReferralEarningsClaimedMessage(decimal amount, bool isOptimistic = false)
        {
            Amount = amount;
            IsOptimistic = isOptimistic;
        }
    }

    /// <summary>
    /// Referral key olusturulduktan sonra yayinlanan message
    /// </summary>
    public readonly struct ReferralKeyGeneratedMessage : IMessage
    {
        public string ReferralKey { get; }

        public ReferralKeyGeneratedMessage(string referralKey)
        {
            ReferralKey = referralKey;
        }
    }
}
