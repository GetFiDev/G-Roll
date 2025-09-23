using System;
using UnityEngine;

[Serializable]
public struct MapCell
{
    public bool walkable;
    public MapCellKind kind;
    public byte cost; // pathfinding maliyeti (0..255), istersen kullan

    public Color GetColor()
    {
        // Temel renkler: tür + walkable overlay
        Color baseCol = kind switch
        {
            MapCellKind.Empty  => new Color(0.18f, 0.20f, 0.25f),
            MapCellKind.Block  => new Color(0.45f, 0.10f, 0.10f),
            MapCellKind.Spawn  => new Color(0.10f, 0.40f, 0.15f),
            MapCellKind.Goal   => new Color(0.10f, 0.25f, 0.45f),
            MapCellKind.Water  => new Color(0.08f, 0.25f, 0.6f),
            MapCellKind.Slow   => new Color(0.35f, 0.32f, 0.10f),
            _ => Color.gray
        };
        // Walkable ise biraz daha parlaklaştır
        return walkable ? Color.Lerp(baseCol, Color.white, 0.15f) : baseCol;
    }
}

public enum MapCellKind : byte
{
    Empty = 0,
    Block = 1,
    Spawn = 2,
    Goal  = 3,
    Water = 4,
    Slow  = 5,
}