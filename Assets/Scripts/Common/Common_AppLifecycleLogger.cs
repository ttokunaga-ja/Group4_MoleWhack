using UnityEngine;

/// <summary>
/// Logs application lifecycle events to help diagnose session/passthrough drops.
/// Attach to any always-alive object (e.g., GameFlowController).
/// </summary>
public class Common_AppLifecycleLogger : MonoBehaviour
{
    private void OnApplicationFocus(bool focus)
    {
        Debug.Log($"[AppLifecycle] OnApplicationFocus: {focus}");
    }

    private void OnApplicationPause(bool pause)
    {
        Debug.Log($"[AppLifecycle] OnApplicationPause: {pause}");
    }

    private void OnApplicationQuit()
    {
        Debug.Log("[AppLifecycle] OnApplicationQuit");
    }
}
