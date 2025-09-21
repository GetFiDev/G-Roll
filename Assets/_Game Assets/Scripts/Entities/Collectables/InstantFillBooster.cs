public class InstantFillBooster : Collectable
{
    public override void OnInteract(PlayerController player)
    {
        base.OnInteract(player);

        UIManager.Instance.gamePlay.boosterFill.InstantFill();
    }
}
