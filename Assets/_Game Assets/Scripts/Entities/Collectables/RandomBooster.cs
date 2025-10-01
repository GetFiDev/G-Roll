using UnityEngine;

public class RandomBooster : BoosterBase
{
    [Header("Random Booster")]
    public BoosterBase[] options;

    protected override void Apply(PlayerController player)
    {
        if (options == null || options.Length == 0) return;

        var pick = options[Random.Range(0, options.Length)];
        if (pick == null) return;

        // Seçilen booster'ı anında uygula (yeni bir çarpışma bekleme)
        // BoosterBase.Apply protected olduğu için kısa bir yol: IPlayerInteractable üzerinden tetikleyelim.
        // Sahte bir collider gerekmesin diye, booster'ı public API'ya açmak en temizi:
        // => BoosterBase'e public void Trigger(PlayerController) ekleyelim:
        // Ama basit kalsın diye OnPlayerEnter'ı çağırmak istemiyoruz (tag vs. kontrol). 
        // O yüzden BoosterBase'e küçük bir public wrapper ekleyelim.

        // QUICK HACK: BoosterBase'e public void ApplyNow(PlayerController p) ekleyebilirsin; burada çağırırız.
        // Eğer eklemek istemezsen:
        // pick.OnPlayerEnter(player, null); // null collider ile; fakat tag kontrolü varsa takılabilir.

        // Tercih edilen: BoosterBase'e ufak bir public wrapper:
        // pick.ApplyNow(player);

        // Geçici çözüm (wrapper ekleyemiyorsan):
        var col = player != null ? player.GetComponentInChildren<Collider>() : null;
        pick.OnPlayerEnter(player, col);
    }
}