using GRoll.Core.Interfaces.Services;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Auth state değiştiğinde yayınlanan message.
    /// </summary>
    public readonly struct AuthStateChangedMessage : IMessage
    {
        public bool IsAuthenticated { get; }
        public UserInfo User { get; }
        public AuthStateChangeReason Reason { get; }

        public AuthStateChangedMessage(AuthStateChangedEventArgs args)
        {
            IsAuthenticated = args.IsAuthenticated;
            User = args.User;
            Reason = args.Reason;
        }

        public AuthStateChangedMessage(bool isAuthenticated, UserInfo user, AuthStateChangeReason reason)
        {
            IsAuthenticated = isAuthenticated;
            User = user;
            Reason = reason;
        }
    }
}
