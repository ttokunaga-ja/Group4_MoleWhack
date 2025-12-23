using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// セットアップ時に QR Pose を収集し、IQR で外れ値を除去してロックするコンポーネント。
/// 収集期間は collectionDuration（既定10秒）。ロック後は固定Poseを提供する。
/// </summary>
public class QRPoseLocker : MonoBehaviour
{
    public static QRPoseLocker Instance { get; private set; }

    public enum LockerState
    {
        Idle,
        Collecting,
        Locked,
        Failed,
        Retry
    }

    [Header("Settings")]
    [SerializeField] private float collectionDuration = 10f;
    [SerializeField] private int minimumSamples = 5;
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool autoStartOnEnable = true;

    public LockerState State { get; private set; } = LockerState.Idle;

    public event Action OnCollectingStarted;
    public event Action OnCollectionAborted;
    public event Action OnLockFailed;
    public event Action<string, Pose> OnPoseLocked;

    private class PoseSample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private readonly Dictionary<string, List<PoseSample>> poseHistories = new Dictionary<string, List<PoseSample>>();
    private readonly Dictionary<string, Pose> lockedPoses = new Dictionary<string, Pose>();
    private float collectionStartTime;
    private bool isRegisteredToQRManager;

    public float CollectionDuration => collectionDuration;
    public int LockedPoseCount => lockedPoses.Count;
    public bool AutoStartOnEnable { get => autoStartOnEnable; set => autoStartOnEnable = value; }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RegisterToQRManager();
        if (autoStartOnEnable)
        {
            BeginCollect();
        }
    }

    private void OnDisable()
    {
        UnregisterFromQRManager();
    }

    public void BeginCollect()
    {
        State = LockerState.Collecting;
        collectionStartTime = Time.time;
        poseHistories.Clear();
        lockedPoses.Clear();
        OnCollectingStarted?.Invoke();
        Log("[COLLECT] Started collecting poses");
    }

    public void Abort()
    {
        if (State != LockerState.Collecting) return;
        State = LockerState.Idle;
        poseHistories.Clear();
        OnCollectionAborted?.Invoke();
        Log("[COLLECT] Aborted");
    }

    public void Retry()
    {
        State = LockerState.Retry;
        BeginCollect();
    }

    public float GetElapsedSeconds()
    {
        return State == LockerState.Collecting ? Time.time - collectionStartTime : 0f;
    }

    private void Update()
    {
        if (!isRegisteredToQRManager)
        {
            RegisterToQRManager();
        }

        if (State != LockerState.Collecting) return;

        if (Time.time - collectionStartTime >= collectionDuration)
        {
            FinalizeLock();
        }
    }

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
        if (State != LockerState.Collecting) return;
        if (info == null) return;

        AddPoseSample(info.uuid, info.lastPose.position, info.lastPose.rotation);
    }

    private void AddPoseSample(string uuid, Vector3 position, Quaternion rotation)
    {
        if (!poseHistories.TryGetValue(uuid, out var list))
        {
            list = new List<PoseSample>();
            poseHistories[uuid] = list;
        }
        list.Add(new PoseSample { time = Time.time, position = position, rotation = rotation });
    }

    private void FinalizeLock()
    {
        bool anyLocked = false;

        foreach (var kvp in poseHistories)
        {
            string uuid = kvp.Key;
            List<PoseSample> samples = kvp.Value;

            if (samples.Count < minimumSamples)
            {
                Log($"[LOCK_FAIL] UUID {uuid}: samples {samples.Count} < min {minimumSamples}");
                continue;
            }

            Pose lockedPose = ComputeLockedPose(samples);
            lockedPoses[uuid] = lockedPose;
            anyLocked = true;
            OnPoseLocked?.Invoke(uuid, lockedPose);
            Log($"[LOCKED] UUID {uuid}: {lockedPose.position}");
        }

        if (anyLocked)
        {
            State = LockerState.Locked;
            Log("[LOCKED] Pose lock completed");
        }
        else
        {
            State = LockerState.Failed;
            OnLockFailed?.Invoke();
            Log("[LOCK_FAIL] No pose locked");
        }
    }

    private Pose ComputeLockedPose(List<PoseSample> samples)
    {
        float SmoothAxis(Func<Vector3, float> selector)
        {
            var ordered = samples.Select(s => selector(s.position)).OrderBy(v => v).ToList();
            int n = ordered.Count;
            float q1 = ordered[(int)(0.25f * (n - 1))];
            float q3 = ordered[(int)(0.75f * (n - 1))];
            float iqr = q3 - q1;
            float min = q1 - 1.5f * iqr;
            float max = q3 + 1.5f * iqr;
            var filtered = ordered.Where(v => v >= min && v <= max).ToList();
            if (filtered.Count == 0) return selector(samples.Last().position);
            return filtered.Average();
        }

        Vector3 smoothedPos = new Vector3(
            SmoothAxis(p => p.x),
            SmoothAxis(p => p.y),
            SmoothAxis(p => p.z)
        );

        // 回転は最新と中央値近傍を軽く平均
        Quaternion latest = samples.Last().rotation;
        Quaternion mid = samples[samples.Count / 2].rotation;
        Quaternion smoothedRot = Quaternion.Slerp(mid, latest, 0.25f);

        return new Pose(smoothedPos, smoothedRot);
    }

    public bool GetLockedPose(string uuid, out Pose pose)
    {
        return lockedPoses.TryGetValue(uuid, out pose);
    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[QRPoseLocker] {message}");
        }
    }
}
