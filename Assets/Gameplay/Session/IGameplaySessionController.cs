using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;

namespace GRoll.Gameplay.Session
{
    /// <summary>
    /// Controls gameplay session lifecycle.
    /// </summary>
    public interface IGameplaySessionController
    {
        bool IsSessionActive { get; }
        bool IsPaused { get; }
        string CurrentSessionId { get; }

        UniTask<OperationResult> BeginSessionAsync(GameMode mode);
        UniTask<OperationResult<SessionResult>> EndSessionAsync(SessionData data);
        void PauseSession();
        void ResumeSession();
        void CancelSession();
    }
}
