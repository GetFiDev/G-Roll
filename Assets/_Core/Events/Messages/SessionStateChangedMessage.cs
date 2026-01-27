using GRoll.Core;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Session state değiştiğinde yayınlanan message.
    /// Gameplay session lifecycle için kullanılır.
    /// </summary>
    public readonly struct SessionStateChangedMessage : IMessage
    {
        /// <summary>
        /// Önceki session state
        /// </summary>
        public SessionState PreviousState { get; }

        /// <summary>
        /// Yeni session state
        /// </summary>
        public SessionState NewState { get; }

        /// <summary>
        /// Session ID (eğer aktifse)
        /// </summary>
        public string SessionId { get; }

        public SessionStateChangedMessage(
            SessionState previous,
            SessionState newState,
            string sessionId = null)
        {
            PreviousState = previous;
            NewState = newState;
            SessionId = sessionId;
        }
    }
}
