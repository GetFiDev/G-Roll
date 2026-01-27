using System;
using GRoll.Core.Interfaces.Infrastructure;
using UnityEngine;

namespace GRoll.Infrastructure.Logging
{
    /// <summary>
    /// Unity Debug.Log kullanarak IGRollLogger implementasyonu.
    /// Production'da disable edilebilir.
    /// </summary>
    public class UnityLogger : IGRollLogger
    {
        private readonly string _prefix;
        private readonly bool _isEnabled;

        /// <summary>
        /// Parameterless constructor for VContainer DI.
        /// Uses default prefix "[GRoll]" and enabled state.
        /// </summary>
        public UnityLogger() : this("[GRoll]", true)
        {
        }

        public UnityLogger(string prefix, bool isEnabled = true)
        {
            _prefix = prefix ?? "[GRoll]";
            _isEnabled = isEnabled;
        }

        public void Log(string message)
        {
            if (!_isEnabled) return;
            Debug.Log($"{_prefix} {message}");
        }

        public void LogWarning(string message)
        {
            if (!_isEnabled) return;
            Debug.LogWarning($"{_prefix} {message}");
        }

        public void LogError(string message)
        {
            if (!_isEnabled) return;
            Debug.LogError($"{_prefix} {message}");
        }

        public void LogError(string message, Exception exception)
        {
            if (!_isEnabled) return;
            Debug.LogError($"{_prefix} {message}\n{exception}");
        }

        public void LogFormat(string format, params object[] args)
        {
            if (!_isEnabled) return;
            Debug.LogFormat($"{_prefix} {format}", args);
        }
    }

    /// <summary>
    /// Hiçbir şey yapmayan logger - test veya production için.
    /// </summary>
    public class NullLogger : IGRollLogger
    {
        public static readonly NullLogger Instance = new();

        public void Log(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void LogError(string message, Exception exception) { }
        public void LogFormat(string format, params object[] args) { }
    }
}
