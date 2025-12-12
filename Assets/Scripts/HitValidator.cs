using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HIT判定バリデーター（複合判定）
/// QR 喪失時にカメラ向きと喪失時間をチェックする。
/// ハンマーは実物使用を前提とし、Unity側でのスイング検出や距離計測は行わない。
/// </summary>
public class HitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraOrientationMonitor cameraMonitor;

    [Header("Hit Settings")]
    [SerializeField] private float maxLossWindow = 0.5f;  // 秒（QR喪失からの判定ウィンドウ）
    [SerializeField] private bool enableDetailedLogging = true;

    [Header("Events")]
    public UnityEvent<string> OnHitSuccess;

    private void Start()
    {
        Log("[START] HitValidator initializing...");

        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost += ValidateHit;
            Log("[START] ✓ Registered to QRManager.OnQRLost");
        }
        else
        {
            LogError("[START] QRManager instance not found!");
            enabled = false;
            return;
        }

        if (cameraMonitor == null)
            cameraMonitor = CameraOrientationMonitor.Instance;

        Log("[START] ✓ HitValidator ready");
    }

    private void OnDestroy()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost -= ValidateHit;
        }
    }

    private void ValidateHit(QRInfo info)
    {
        // QRManager が最後に観測した時刻との差分を喪失時間とする
        float lostDuration = Time.time - info.lastSeenTime;

        // ===== 複合判定開始 =====
        Log($"\n========================================");
        Log($"[HIT_VALIDATION] Checking HIT for UUID: {info.uuid}");
        Log($"========================================");

        // チェック1: カメラ向き
        bool cameraOK = (cameraMonitor != null && cameraMonitor.IsCameraFacingEnough);
        Log($"[HIT_CHECK1] Camera Facing: {cameraOK} (Visible UUIDs: {cameraMonitor?.VisibleUUIDCount ?? 0})");
        if (!cameraOK)
        {
            Log($"[HIT_FAIL] Camera not facing enough - aborting");
            Log($"========================================\n");
            return;
        }

        // チェック2: 喪失ウィンドウ
        bool timeOK = (lostDuration <= maxLossWindow);
        Log($"[HIT_CHECK2] Loss Duration: {lostDuration:F2}s <= {maxLossWindow:F2}s? {timeOK}");
        if (!timeOK)
        {
            Log($"[HIT_FAIL] Loss window exceeded - aborting");
            Log($"========================================\n");
            return;
        }

        // ===== すべてのチェック成功 =====
        Log($"[HIT_SUCCESS] ★★★ ALL CHECKS PASSED ★★★");
        Log($"[HIT_SUCCESS] UUID: {info.uuid}");
        Log($"[HIT_SUCCESS] Position: {info.lastPose.position}");
        Log($"========================================\n");

        // イベント発火
        OnHitSuccess?.Invoke(info.uuid);
        Log($"[HIT_SUCCESS] ✓ OnHitSuccess event invoked");
    }

    private void Log(string message)
    {
        if (enableDetailedLogging)
            Debug.Log($"[HitValidator] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[HitValidator] {message}");
    }
}
