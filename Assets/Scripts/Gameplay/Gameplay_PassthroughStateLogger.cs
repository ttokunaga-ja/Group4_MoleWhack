using UnityEngine;

/// <summary>
/// Logs OVRPassthroughLayer enabled state changes and focus events to help debug sudden passthrough loss.
/// Safe to attach even if OVRPassthroughLayer is missing (logs a warning once).
/// </summary>
public class Gameplay_PassthroughStateLogger : MonoBehaviour
{
    [SerializeField] private float pollInterval = 1f;
    private float nextPollTime;
    private bool lastEnabledState = false;
    private bool warnedMissing = false;
    private Component passthroughLayer; // keep it generic to avoid hard dependency

    private void Start()
    {
        TryResolvePassthroughLayer();
        LogState("Start");
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPollTime) return;
        nextPollTime = Time.unscaledTime + pollInterval;
        LogState("Update");
    }

    private void OnEnable()
    {
        LogState("OnEnable");
    }

    private void OnDisable()
    {
        LogState("OnDisable");
    }

    private void OnApplicationFocus(bool focus)
    {
        Debug.Log($"[PassthroughLogger] OnApplicationFocus: {focus}");
    }

    private void OnApplicationPause(bool pause)
    {
        Debug.Log($"[PassthroughLogger] OnApplicationPause: {pause}");
    }

    private void TryResolvePassthroughLayer()
    {
        if (passthroughLayer != null) return;
        passthroughLayer = GetComponent("OVRPassthroughLayer");
        if (passthroughLayer == null && !warnedMissing)
        {
            Debug.LogWarning("[PassthroughLogger] OVRPassthroughLayer not found on this object");
            warnedMissing = true;
        }
    }

    private void LogState(string context)
    {
        TryResolvePassthroughLayer();
        if (passthroughLayer == null) return;

        var enabledProp = passthroughLayer.GetType().GetProperty("enabled");
        bool current = enabledProp != null && (bool)enabledProp.GetValue(passthroughLayer);
        if (context == "Start" || context == "OnEnable" || context == "OnDisable" || current != lastEnabledState)
        {
            Debug.Log($"[PassthroughLogger] {context}: enabled={current} time={Time.time:F2}");
        }
        lastEnabledState = current;
    }
}
