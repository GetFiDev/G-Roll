using GRoll.Core;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Gameplay session başladığında yayınlanan message.
    /// </summary>
    public readonly struct GameplaySessionStartedMessage : IMessage
    {
        public string SessionId { get; }
        public GameMode Mode { get; }

        public GameplaySessionStartedMessage(string sessionId, GameMode mode)
        {
            SessionId = sessionId;
            Mode = mode;
        }
    }

    /// <summary>
    /// Gameplay session bittiğinde yayınlanan message.
    /// </summary>
    public readonly struct GameplaySessionEndedMessage : IMessage
    {
        public string SessionId { get; }
        public int Score { get; }
        public int Coins { get; }
        public bool Success { get; }

        public GameplaySessionEndedMessage(string sessionId, int score, int coins, bool success)
        {
            SessionId = sessionId;
            Score = score;
            Coins = coins;
            Success = success;
        }
    }

    /// <summary>
    /// Meta ekranına dönüldüğünde yayınlanan message.
    /// </summary>
    public readonly struct ReturnToMetaMessage : IMessage
    {
    }

    /// <summary>
    /// Oyuncu bitiş noktasına ulaştığında yayınlanan message.
    /// </summary>
    public readonly struct LevelCompleteMessage : IMessage
    {
        public UnityEngine.Vector3 FinishPosition { get; }

        public LevelCompleteMessage(UnityEngine.Vector3 finishPosition)
        {
            FinishPosition = finishPosition;
        }
    }
}
