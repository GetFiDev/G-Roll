using System;
using System.Collections.Generic;
using GRoll.Core;

namespace GRoll.Domain.Economy.Models
{
    /// <summary>
    /// Currency state'ini tutan internal class.
    /// CurrencyService tarafından yönetilir.
    /// </summary>
    public class CurrencyState
    {
        private readonly Dictionary<CurrencyType, int> _balances = new();
        private readonly object _lock = new();

        /// <summary>
        /// Belirtilen currency tipinin bakiyesini döndürür.
        /// </summary>
        public int GetBalance(CurrencyType type)
        {
            lock (_lock)
            {
                return _balances.TryGetValue(type, out var balance) ? balance : 0;
            }
        }

        /// <summary>
        /// Belirtilen currency tipinin bakiyesini ayarlar.
        /// </summary>
        public void SetBalance(CurrencyType type, int amount)
        {
            if (amount < 0)
                throw new ArgumentException("Balance cannot be negative", nameof(amount));

            lock (_lock)
            {
                _balances[type] = amount;
            }
        }

        /// <summary>
        /// Belirtilen miktarı ekler.
        /// </summary>
        public void Add(CurrencyType type, int amount)
        {
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(amount));

            lock (_lock)
            {
                _balances[type] = GetBalanceUnsafe(type) + amount;
            }
        }

        /// <summary>
        /// Belirtilen miktarı harcamaya çalışır.
        /// Başarılı ise true döner.
        /// </summary>
        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(amount));

            lock (_lock)
            {
                var current = GetBalanceUnsafe(type);
                if (current < amount)
                    return false;

                _balances[type] = current - amount;
                return true;
            }
        }

        /// <summary>
        /// Belirtilen miktarı karşılayabilir mi?
        /// </summary>
        public bool CanAfford(CurrencyType type, int amount)
        {
            return GetBalance(type) >= amount;
        }

        /// <summary>
        /// Tüm bakiyelerin kopyasını döndürür.
        /// </summary>
        public Dictionary<CurrencyType, int> GetBalancesCopy()
        {
            lock (_lock)
            {
                return new Dictionary<CurrencyType, int>(_balances);
            }
        }

        /// <summary>
        /// Bakiyeleri verilen dictionary ile değiştirir.
        /// Negatif bakiye değerleri kabul edilmez.
        /// </summary>
        /// <exception cref="ArgumentException">Negatif bakiye değeri varsa</exception>
        public void SetBalances(Dictionary<CurrencyType, int> balances)
        {
            if (balances == null)
                throw new ArgumentNullException(nameof(balances));

            // Validate all balances before applying
            foreach (var kvp in balances)
            {
                if (kvp.Value < 0)
                    throw new ArgumentException($"Balance cannot be negative for {kvp.Key}: {kvp.Value}", nameof(balances));
            }

            lock (_lock)
            {
                _balances.Clear();
                foreach (var kvp in balances)
                {
                    _balances[kvp.Key] = kvp.Value;
                }
            }
        }

        private int GetBalanceUnsafe(CurrencyType type)
        {
            return _balances.TryGetValue(type, out var balance) ? balance : 0;
        }
    }
}
