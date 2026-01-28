using GRoll.Core.Interfaces.Services;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// User profile değiştiğinde yayınlanan message.
    /// </summary>
    public readonly struct UserProfileChangedMessage : IMessage
    {
        public UserProfile Profile { get; }
        public bool IsComplete { get; }

        public UserProfileChangedMessage(UserProfile profile)
        {
            Profile = profile;
            IsComplete = profile?.IsComplete ?? false;
        }
    }
}
