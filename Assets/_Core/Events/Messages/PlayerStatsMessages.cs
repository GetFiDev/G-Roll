namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Player stats guncellendikten sonra yayinlanan message
    /// </summary>
    public readonly struct PlayerStatsUpdatedMessage : IMessage
    {
        public int TotalGamesPlayed { get; }
        public int HighScore { get; }
        public int TotalCoinsEarned { get; }

        public PlayerStatsUpdatedMessage(int totalGamesPlayed, int highScore, int totalCoinsEarned)
        {
            TotalGamesPlayed = totalGamesPlayed;
            HighScore = highScore;
            TotalCoinsEarned = totalCoinsEarned;
        }
    }

    /// <summary>
    /// Yeni high score kazanildiginda yayinlanan message
    /// </summary>
    public readonly struct NewHighScoreAchievedMessage : IMessage
    {
        public int PreviousHighScore { get; }
        public int NewHighScore { get; }

        public NewHighScoreAchievedMessage(int previousHighScore, int newHighScore)
        {
            PreviousHighScore = previousHighScore;
            NewHighScore = newHighScore;
        }
    }

    /// <summary>
    /// Oyun istatistigi kaydedildiginde yayinlanan message
    /// </summary>
    public readonly struct GameStatRecordedMessage : IMessage
    {
        public string StatKey { get; }
        public int PreviousValue { get; }
        public int NewValue { get; }

        public GameStatRecordedMessage(string statKey, int previousValue, int newValue)
        {
            StatKey = statKey;
            PreviousValue = previousValue;
            NewValue = newValue;
        }
    }
}
