using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class UIGamePlay : MonoBehaviour
{
    [Required] public Joystick joystick;

    private void OnValidate()
    {
        joystick = GetComponentInChildren<Joystick>();
    }
}
