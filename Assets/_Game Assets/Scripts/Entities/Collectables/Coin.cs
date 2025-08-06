public class Coin : Collectable
{
    public override void OnInteract()
    {
        base.OnInteract();
        
        //TODO: Increase Currency Properly
        DataManager.Currency++;
    }
}