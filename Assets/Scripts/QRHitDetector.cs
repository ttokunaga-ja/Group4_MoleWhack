using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// QR コード認識喪失による「当たり判定」検出スクリプト
/// 
/// 動作原理:
/// 1. QR コード検出 → Sphere 配置（ターゲット表示）
/// 2. 実物のハンマーで QR コードを叩く
/// 3. QR コードが隠れる → 認識喪失
/// 4. OnQRLost イベント発火 → 当たり判定成功とみなす
/// 5. Sphere 削除 + スコア加算などの処理
/// 
/// セットアップ:
/// 1. QRObjectPositioner と同じ GameObject にアタッチ
/// 2. Inspector で OnHitSuccess イベントにスコア加算などの処理を登録
/// </summary>
public class QRHitDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool enableHitLogging = true;

    [Header("Hit Events")]
    public UnityEvent<string> OnHitSuccess;  // 当たり判定成功時のイベント（UUID 付き）

    [Header("Statistics")]
    [SerializeField] private int totalHits = 0;
    [SerializeField] private float lastHitTime = 0f;

    private void Start()
    {
        LogHit("[START] QRHitDetector initializing...");

        // QRManager のイベントに登録
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost += HandleQRLost;
            LogHit("[START] ✓ Registered to QRManager events");
        }
        else
        {
            LogErrorHit("[START] QRManager instance not found!");
            enabled = false;
            return;
        }

        LogHit("[START] ✓ QRHitDetector ready");
        LogHit($"[START] Enable Hit Logging: {enableHitLogging}");
    }

    private void OnDestroy()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost -= HandleQRLost;
            LogHit("[OnDestroy] ✓ Event listener unregistered");
        }
    }

    private void HandleQRLost(QRInfo info)
    {
        if (info == null)
        {
            LogErrorHit("[HIT_DETECTED] QRInfo is null");
            return;
        }

        totalHits++;
        lastHitTime = Time.time;

        LogHit("========================================");
        LogHit($"[HIT_DETECTED] ★★★ HIT DETECTED ★★★");
        LogHit("========================================");
        LogHit($"[HIT_DETECTED] QR UUID: {info.uuid}");
        LogHit($"[HIT_DETECTED] Total Hits: {totalHits}");
        LogHit($"[HIT_DETECTED] Time: {lastHitTime:F2}s");
        LogHit("========================================");

        OnHitSuccess?.Invoke(info.uuid);
        LogHit($"[HIT_DETECTED] ✓ OnHitSuccess event invoked");
    }

    /// <summary>
    /// 現在のヒット数を取得
    /// </summary>
    public int GetTotalHits()
    {
        return totalHits;
    }

    /// <summary>
    /// 最後のヒット時刻を取得
    /// </summary>
    public float GetLastHitTime()
    {
        return lastHitTime;
    }

    /// <summary>
    /// ヒット統計をリセット
    /// </summary>
    public void ResetStatistics()
    {
        totalHits = 0;
        lastHitTime = 0f;
        LogHit("[RESET] ✓ Hit statistics reset");
    }

    // ========================================
    // ログ出力メソッド
    // ========================================

    private void LogHit(string message)
    {
        if (enableHitLogging)
        {
            Debug.Log($"[QRHitDetector] {message}");
        }
    }

    private void LogWarningHit(string message)
    {
        Debug.LogWarning($"[QRHitDetector] {message}");
    }

    private void LogErrorHit(string message)
    {
        Debug.LogError($"[QRHitDetector] {message}");
    }
}
