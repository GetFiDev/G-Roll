using System;
using AssetKits.ParticleImage;
using DG.Tweening;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using CoreCurrencyType = GRoll.Core.CurrencyType;

namespace _Project.Scripts.Utility.Currency.UI
{
    public class UICurrencyDisplayWithAnimation : UICurrencyDisplay
    {
        [SerializeField] private ParticleImage particleImage;

        private const int MaxCount = 20;
        private const int MinPrice = 1;

        private IMessageBus _messageBus;
        private IDisposable _subscription;

        [Inject]
        public void Construct(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_messageBus != null)
            {
                _subscription = _messageBus.Subscribe<CurrencyCollectedMessage>(OnCurrencyCollected);
            }
        }

        private void OnCurrencyCollected(CurrencyCollectedMessage message)
        {
            // Compare by underlying int value since CurrencyType exists in two namespaces
            if ((int)message.Type != (int)currencyType) return;
            SpawnCurrencyOnCollected(message.Amount, message.WorldPosition);
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
        private void SpawnCurrency(int amount)
        {
            SpawnCurrencyOnCollected(amount, new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
        }

        private void SpawnCurrencyOnCollected(int amount, Vector3 position)
        {
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

            _subscription?.Dispose();
            _subscription = null;
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