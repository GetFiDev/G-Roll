using UnityEngine;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Published when player makes their first move in a session.
    /// </summary>
    public readonly struct PlayerFirstMoveMessage : IMessage { }

    /// <summary>
    /// Published when player changes direction.
    /// </summary>
    public readonly struct PlayerDirectionChangedMessage : IMessage
    {
        public Vector3 Direction { get; }

        public PlayerDirectionChangedMessage(Vector3 direction)
        {
            Direction = direction;
        }
    }

    /// <summary>
    /// Published when player jumps.
    /// </summary>
    public readonly struct PlayerJumpMessage : IMessage { }

    /// <summary>
    /// Published when player lands after a jump.
    /// </summary>
    public readonly struct PlayerLandMessage : IMessage { }

    /// <summary>
    /// Published when player hits a wall (basic, no details).
    /// </summary>
    public readonly struct PlayerWallHitMessage : IMessage { }

    /// <summary>
    /// Published when player hits a wall with position details.
    /// Used by PlayerMovement for wall hit feedback effects.
    /// </summary>
    public readonly struct PlayerWallHitDetailedMessage : IMessage
    {
        public Vector3 HitPoint { get; }
        public Vector3 HitNormal { get; }

        public PlayerWallHitDetailedMessage(Vector3 hitPoint, Vector3 hitNormal)
        {
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }
    }

    /// <summary>
    /// Published when player collects an item.
    /// </summary>
    public readonly struct PlayerCollectMessage : IMessage
    {
        public string ItemType { get; }
        public int Amount { get; }
        public Vector3 Position { get; }

        public PlayerCollectMessage(string itemType, int amount, Vector3 position)
        {
            ItemType = itemType;
            Amount = amount;
            Position = position;
        }
    }

    /// <summary>
    /// Published when player dies/fails.
    /// </summary>
    public readonly struct PlayerDeathMessage : IMessage
    {
        public string Reason { get; }
        public Vector3 Position { get; }

        public PlayerDeathMessage(string reason, Vector3 position)
        {
            Reason = reason;
            Position = position;
        }
    }
}
