namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Leaderboard guncellendikten sonra yayinlanan message
    /// </summary>
    public readonly struct LeaderboardUpdatedMessage : IMessage
    {
        public LeaderboardType Type { get; }
        public int EntryCount { get; }

        public LeaderboardUpdatedMessage(LeaderboardType type, int entryCount)
        {
            Type = type;
            EntryCount = entryCount;
        }
    }

    /// <summary>
    /// Leaderboard tipi degistiginde yayinlanan message
    /// </summary>
    public readonly struct LeaderboardTypeChangedMessage : IMessage
    {
        public LeaderboardType PreviousType { get; }
        public LeaderboardType NewType { get; }

        public LeaderboardTypeChangedMessage(LeaderboardType previousType, LeaderboardType newType)
        {
            PreviousType = previousType;
            NewType = newType;
        }
    }

    /// <summary>
    /// Yeni high score kazanildiginda yayinlanan message
    /// </summary>
    public readonly struct NewHighScoreMessage : IMessage
    {
        public int PreviousScore { get; }
        public int NewScore { get; }
        public int NewRank { get; }

        public NewHighScoreMessage(int previousScore, int newScore, int newRank)
        {
            PreviousScore = previousScore;
            NewScore = newScore;
            NewRank = newRank;
        }
    }

    /// <summary>
    /// Leaderboard tipi enum (AllTime, Season, Weekly, Daily)
    /// </summary>
    public enum LeaderboardType
    {
        AllTime = 0,
        Season = 1,
        Weekly = 2,
        Daily = 3
    }
}
