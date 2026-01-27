using GRoll.Core;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Game phase değiştiğinde yayınlanan message.
    /// Boot -> Meta -> Gameplay geçişlerinde kullanılır.
    /// </summary>
    public readonly struct GamePhaseChangedMessage : IMessage
    {
        /// <summary>
        /// Önceki phase
        /// </summary>
        public GamePhase PreviousPhase { get; }

        /// <summary>
        /// Yeni (aktif) phase
        /// </summary>
        public GamePhase NewPhase { get; }

        public GamePhaseChangedMessage(GamePhase previous, GamePhase newPhase)
        {
            PreviousPhase = previous;
            NewPhase = newPhase;
        }
    }
}
