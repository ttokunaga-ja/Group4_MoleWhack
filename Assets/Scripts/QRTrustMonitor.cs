using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// QR の同時可視UUID集合を記録し、距離しきい値付きで信頼度を評価するモニター。
/// セットアップ中に「既知集合」を構築し、プレイ中は現在可視集合と比較して信頼度を公開する。
/// </summary>
public class QRTrustMonitor : MonoBehaviour
{
    public static QRTrustMonitor Instance { get; private set; }

    public enum MonitorMode
    {
        Idle,
        Setup,
        Gameplay
    }

    [Header("Settings")]
    [SerializeField] private float distanceThresholdMeters = 1.0f;
    [SerializeField] private float trustLowThreshold = 0.5f;
    [SerializeField] private bool enableLogging = true;

    public MonitorMode Mode { get; private set; } = MonitorMode.Idle;
    public float CurrentTrust { get; private set; } = 1f;
    public float TrustLowThreshold => trustLowThreshold;

    public event Action<float> OnTrustChanged;
    public event Action OnTrustLow;

    private readonly Dictionary<string, HashSet<string>> knownSets = new Dictionary<string, HashSet<string>>();
    private bool isRegisteredToQRManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        RegisterToQRManager();
    }

    private void OnDisable()
    {
        UnregisterFromQRManager();
    }

    private void Update()
    {
        if (!isRegisteredToQRManager)
        {
            RegisterToQRManager();
        }

        if (Mode == MonitorMode.Gameplay)
        {
            EvaluateTrust();
        }
    }

    public void BeginSetup()
    {
        Mode = MonitorMode.Setup;
        knownSets.Clear();
        CurrentTrust = 1f;
        Log("[SETUP] Begin setup collection");
    }

    public void BeginGameplay()
    {
        Mode = MonitorMode.Gameplay;
        EvaluateTrust();
        Log("[GAMEPLAY] Begin trust monitoring");
    }

    public void ResetMonitor()
    {
        Mode = MonitorMode.Idle;
        knownSets.Clear();
        CurrentTrust = 1f;
        Log("[RESET] Cleared known sets");
    }

    public IReadOnlyDictionary<string, HashSet<string>> GetKnownSets() => knownSets;

    private void RegisterToQRManager()
    {
        if (isRegisteredToQRManager || QRManager.Instance == null) return;

        QRManager.Instance.OnQRUpdated += HandleQRUpdated;
        isRegisteredToQRManager = true;
        Log("[START] ✓ Registered to QRManager.OnQRUpdated");
    }

    private void UnregisterFromQRManager()
    {
        if (isRegisteredToQRManager && QRManager.Instance != null)
        {
            QRManager.Instance.OnQRUpdated -= HandleQRUpdated;
            isRegisteredToQRManager = false;
        }
    }

    private void HandleQRUpdated(QRInfo info)
    {
        if (Mode != MonitorMode.Setup) return;
        if (info == null || QRManager.Instance == null) return;

        var visible = QRManager.Instance.CurrentTrackedUUIDs;
        if (visible == null || visible.Count == 0) return;

        AddKnownUUID(info.uuid, info.uuid); // 自己も既知集合に含めておく

        Pose targetPose = info.lastPose;
        foreach (var uuid in visible)
        {
            if (uuid == info.uuid) continue;
            QRInfo other = QRManager.Instance.GetQRInfo(uuid);
            if (other == null) continue;

            float dist = Vector3.Distance(targetPose.position, other.lastPose.position);
            if (dist <= distanceThresholdMeters)
            {
                AddKnownUUID(info.uuid, uuid);
            }
        }
    }

    private void AddKnownUUID(string keyUuid, string neighborUuid)
    {
        if (!knownSets.TryGetValue(keyUuid, out var set))
        {
            set = new HashSet<string>();
            knownSets[keyUuid] = set;
        }

        if (set.Add(neighborUuid))
        {
            Log($"[KNOWN] {keyUuid} <= {neighborUuid}");
        }
    }

    private void EvaluateTrust()
    {
        if (QRManager.Instance == null)
        {
            SetTrust(0f);
            return;
        }

        var visible = QRManager.Instance.CurrentTrackedUUIDs;
        int visibleCount = visible.Count;
        if (knownSets.Count == 0)
        {
            SetTrust(0f);
            return;
        }

        // 既知集合との一致率を計算（最小値を採用）
        float minKnownRatio = 1f;
        HashSet<string> unionKnown = new HashSet<string>();
        foreach (var kvp in knownSets)
        {
            unionKnown.UnionWith(kvp.Value);
            if (kvp.Value.Count == 0) continue;
            int hit = visible.Count == 0 ? 0 : visible.Intersect(kvp.Value).Count();
            float ratio = (float)hit / kvp.Value.Count;
            minKnownRatio = Mathf.Min(minKnownRatio, ratio);
        }

        // 未知UUIDが可視集合に占める割合
        int unknownCount = visibleCount == 0 ? 0 : visible.Except(unionKnown).Count();
        float unknownFraction = visibleCount > 0 ? (float)unknownCount / visibleCount : 0f;

        float trust = Mathf.Min(minKnownRatio, 1f - unknownFraction);
        trust = Mathf.Clamp01(trust);
        SetTrust(trust);
    }

    private void SetTrust(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Abs(clamped - CurrentTrust) > 0.01f)
        {
            CurrentTrust = clamped;
            OnTrustChanged?.Invoke(CurrentTrust);
            Log($"[TRUST] {CurrentTrust:F2}");
        }

        if (CurrentTrust < trustLowThreshold)
        {
            OnTrustLow?.Invoke();
            Log($"[TRUST] LOW (threshold {trustLowThreshold:F2})");
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[QRTrustMonitor] {message}");
        }
    }
}
