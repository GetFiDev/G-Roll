public class Coin : Collectable
{
    public override void OnInteract()
    {
        base.OnInteract();
        
        CurrencyEvents.OnCollected?.Invoke(CurrencyType.SoftCurrency, new CurrencyCollectedData(1, transform.position));
    }
}