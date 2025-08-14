public class InstantFillBooster : Collectable
{
    public override void OnInteract()
    {
        base.OnInteract();

        UIManager.Instance.gamePlay.boosterFill.InstantFill();
    }
}
