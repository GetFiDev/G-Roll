using GRoll.Core.Events;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Inventory server ile sync edildiğinde publish edilir.
    /// </summary>
    public readonly struct InventorySyncedMessage : IMessage
    {
    }

    /// <summary>
    /// Achievements server ile sync edildiğinde publish edilir.
    /// </summary>
    public readonly struct AchievementsSyncedMessage : IMessage
    {
    }

    /// <summary>
    /// Tasks server ile sync edildiğinde publish edilir.
    /// </summary>
    public readonly struct TasksSyncedMessage : IMessage
    {
    }

    /// <summary>
    /// Energy server ile sync edildiğinde publish edilir.
    /// </summary>
    public readonly struct EnergySyncedMessage : IMessage
    {
    }

    /// <summary>
    /// Currency server ile sync edildiğinde publish edilir.
    /// </summary>
    public readonly struct CurrencySyncedMessage : IMessage
    {
    }
}
