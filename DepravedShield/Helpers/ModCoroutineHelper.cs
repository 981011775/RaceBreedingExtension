using System.Collections;
using UnityEngine;

namespace DepravedShield.Helpers
{
    public static class ModCoroutineHelper
    {
        private static MonoBehaviour _handler;
        private static void EnsureHandler()
        {
            if (_handler == null)
            {
                var go = new GameObject("CoroutineHelper");
                Object.DontDestroyOnLoad(go);
                _handler = go.AddComponent<CoroutineHandler>();
            }
        }
        public static Coroutine StartCoroutine(IEnumerator routine)
        {
            EnsureHandler();
            return _handler.StartCoroutine(routine);
        }
        private class CoroutineHandler : MonoBehaviour { }
    }
}