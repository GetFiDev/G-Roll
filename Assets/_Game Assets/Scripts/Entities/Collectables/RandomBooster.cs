using System.Collections.Generic;
using UnityEngine;

public class RandomBooster : Collectable
{
    [SerializeField] private List<Collectable> boosters;

    public override void OnInteract()
    {
        boosters.RandomItem().OnInteract();
    }
}