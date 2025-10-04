using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Channels/Void Event")]
public class VoidEventChannelSO : ScriptableObject
{
    public event Action OnEvent;
    public void Raise() => OnEvent?.Invoke();
}