using UnityEngine;

namespace GRoll.Gameplay.Player.Input
{
    /// <summary>
    /// Input data passed to player state machine.
    /// </summary>
    public struct PlayerInputData
    {
        public PlayerInputType Type;
        public Vector3 Direction;
        public object Data;

        public static PlayerInputData None => new PlayerInputData { Type = PlayerInputType.None };

        public static PlayerInputData Swipe(Vector3 direction)
        {
            return new PlayerInputData
            {
                Type = PlayerInputType.Swipe,
                Direction = direction.normalized
            };
        }

        public static PlayerInputData Tap()
        {
            return new PlayerInputData { Type = PlayerInputType.Tap };
        }

        public static PlayerInputData DoubleTap()
        {
            return new PlayerInputData { Type = PlayerInputType.DoubleTap };
        }

        public static PlayerInputData TeleportTrigger(object portalData = null)
        {
            return new PlayerInputData
            {
                Type = PlayerInputType.TeleportTrigger,
                Data = portalData
            };
        }

        public static PlayerInputData Freeze()
        {
            return new PlayerInputData { Type = PlayerInputType.Freeze };
        }

        public static PlayerInputData Unfreeze()
        {
            return new PlayerInputData { Type = PlayerInputType.Unfreeze };
        }
    }

    /// <summary>
    /// Types of player input.
    /// </summary>
    public enum PlayerInputType
    {
        None,
        Swipe,
        Tap,
        DoubleTap,
        TeleportTrigger,
        Freeze,
        Unfreeze
    }
}
