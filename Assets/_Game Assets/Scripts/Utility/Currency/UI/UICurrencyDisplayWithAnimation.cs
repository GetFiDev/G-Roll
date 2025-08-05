using AssetKits.ParticleImage;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _Project.Scripts.Utility.Currency.UI
{
    public class UICurrencyDisplayWithAnimation : UICurrencyDisplay
    {
        [SerializeField] private ParticleImage particleImage;

        private const int MaxCount = 20;
        private const int MinPrice = 5;

        protected override void OnEnable()
        {
            base.OnEnable();

            CurrencyEvents.OnRewarded.Subscribe(currencyType, SpawnCurrencyOnRewarded);
        }

        private Tween _punchTween;

        private void OnAnyParticleFinished(int amount)
        {
            CurrencyData.Value += amount;

            if (_punchTween != null && _punchTween.IsPlaying())
                return;

            _punchTween = currencyImage.transform.DOPunchScale(Vector3.one * .2f, .1f).SetUpdate(true);
        }

        [GUIColor(1f, .4f, 1f)]
        [Button("Spawn", ButtonSizes.Large, ButtonStyle.Box)]
        private void SpawnCurrencyOnRewarded((int, Vector3) payload)
        {
            var amount = payload.Item1;
            var position = payload.Item2;

            var newParticle = CreateParticle(position);
            var spawnInfo = SpawnPriceInfo.Calculate(amount);

            SetupParticleListeners(spawnInfo, newParticle);
            PlayParticle(spawnInfo, newParticle);
        }

        private static void PlayParticle(SpawnPriceInfo spawnInfo, ParticleImage newParticle)
        {
            newParticle.AddBurst(0, spawnInfo.spawnCount);
            newParticle.duration = 1f;

            newParticle.Play();
        }

        private void SetupParticleListeners(SpawnPriceInfo spawnInfo, ParticleImage newParticle)
        {
            RemoveAllListeners(newParticle);

            if (spawnInfo.remainderPrice > 0)
                newParticle.onLastParticleFinished.AddListener(() => OnAnyParticleFinished(spawnInfo.remainderPrice));

            newParticle.onAnyParticleFinished.AddListener(() => OnAnyParticleFinished(spawnInfo.defaultPrice));
            newParticle.onParticleStop.AddListener(() => Destroy(newParticle.gameObject));
        }

        //TODO: Move into ParticleImage Class
        private static void RemoveAllListeners(ParticleImage newParticle)
        {
            newParticle.onLastParticleFinished.RemoveAllListeners();
            newParticle.onAnyParticleFinished.RemoveAllListeners();
            newParticle.onParticleStop.RemoveAllListeners();
        }

        private ParticleImage CreateParticle(Vector3 position)
        {
            //TODO: pool particles!
            var newParticle = Instantiate(particleImage, transform);
            newParticle.transform.position = position;
            newParticle.gameObject.SetActive(true);

            return newParticle;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            CurrencyEvents.OnRewarded.Unsubscribe(currencyType, SpawnCurrencyOnRewarded);
        }

        private class SpawnPriceInfo
        {
            public int spawnCount;
            public int defaultPrice;
            public int remainderPrice;

            private SpawnPriceInfo()
            {
            }

            public static SpawnPriceInfo Calculate(int amount)
            {
                var result = new SpawnPriceInfo();

                if (amount <= MaxCount * MinPrice)
                {
                    result.spawnCount = Mathf.FloorToInt(amount / (float)MinPrice);
                    result.defaultPrice = MinPrice;

                    result.remainderPrice = amount % MinPrice;
                }
                else
                {
                    result.defaultPrice = Mathf.FloorToInt(amount / (float)MaxCount);
                    result.remainderPrice = amount % MaxCount;

                    result.spawnCount = MaxCount;
                }

                return result;
            }
        }
    }
}