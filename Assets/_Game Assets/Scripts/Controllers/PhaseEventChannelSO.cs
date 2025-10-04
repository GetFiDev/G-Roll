using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Channels/Phase Event")]
public class PhaseEventChannelSO : ScriptableObject
{
    public event Action<GamePhase> OnEvent;
    public void Raise(GamePhase phase) => OnEvent?.Invoke(phase);
}