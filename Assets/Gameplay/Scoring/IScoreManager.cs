using System;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Gameplay.Scoring
{
    /// <summary>
    /// Manages score and collectibles during gameplay.
    /// </summary>
    public interface IScoreManager
    {
        int CurrentScore { get; }
        int CoinsCollected { get; }
        int CurrentCombo { get; }
        float DistanceTraveled { get; }

        /// <summary>
        /// Triggered when score changes.
        /// </summary>
        event Action<int> OnScoreChanged;

        /// <summary>
        /// Triggered when combo changes.
        /// </summary>
        event Action<int> OnComboChanged;

        void Reset();
        void AddScore(int amount);
        void AddCombo(int amount = 1);
        void ResetCombo();
        void AddCollectible(string type, int value);
        void UpdateDistanceScore(float deltaTime);
        SessionData CollectSessionData();
    }
}
