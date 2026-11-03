using UnityEngine;

namespace StoreManager
{
    /// <summary>Builds the whole game at startup, so any scene (even an empty one) can be played.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<Store>() != null) return;
            var go = new GameObject("Storemanager");
            go.AddComponent<Store>();
        }
    }
}
