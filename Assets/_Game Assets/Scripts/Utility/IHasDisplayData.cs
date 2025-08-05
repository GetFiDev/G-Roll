using UnityEngine;

public interface IHasName
{
    string DisplayName { get; }
}

public interface IHasDescription
{
    string Description { get; }
}

public interface IHasIcon
{
    Sprite Icon { get; }
}

public interface IHasDisplayData : IHasName, IHasDescription, IHasIcon { }

public interface IHasPrice
{
    int Price { get; }
}