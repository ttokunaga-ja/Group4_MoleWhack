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
    [SerializeField, HideInInspector] private QRCodeTracker_MRUK qrCodeTracker;
    [SerializeField, HideInInspector] private QRObjectPositioner qrObjectPositioner;
    [SerializeField] private bool enableHitLogging = true;

    [Header("Hit Events")]
    public UnityEvent<string> OnHitSuccess;  // 当たり判定成功時のイベント（UUID 付き）

    [Header("Statistics")]
    [SerializeField] private int totalHits = 0;
    [SerializeField] private float lastHitTime = 0f;

    private bool subscribed = false;

    private void Start()
    {
        LogHit("[START] QRHitDetector initializing...");

        AutoAssignReferences();

        LogHit("[START] ✓ QRHitDetector ready");
        LogHit($"[START] Enable Hit Logging: {enableHitLogging}");
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        // インスペクタでの参照抜けを防ぐため自動割り当てを実行
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (qrCodeTracker != null && (!qrCodeTracker.enabled || !qrCodeTracker.gameObject.activeInHierarchy))
        {
            LogWarningHit("[AutoAssign] Existing QRCodeTracker is disabled or inactive. Reassigning...");
            qrCodeTracker = null;
        }

        if (qrCodeTracker == null)
        {
            qrCodeTracker = FindFirstObjectByType<QRCodeTracker_MRUK>();
            if (qrCodeTracker != null)
            {
                LogHit("[AutoAssign] ✓ QRCodeTracker_MRUK auto-assigned");
            }
            else
            {
                LogErrorHit("[AutoAssign] ✖ QRCodeTracker_MRUK not found (active & enabled)");
            }
        }

        if (qrObjectPositioner != null && (!qrObjectPositioner.enabled || !qrObjectPositioner.gameObject.activeInHierarchy))
        {
            LogWarningHit("[AutoAssign] Existing QRObjectPositioner is disabled or inactive. Reassigning...");
            qrObjectPositioner = null;
        }

        if (qrObjectPositioner == null)
        {
            qrObjectPositioner = FindFirstObjectByType<QRObjectPositioner>();
            if (qrObjectPositioner != null)
            {
                LogHit("[AutoAssign] ✓ QRObjectPositioner auto-assigned");
            }
            else
            {
                LogWarningHit("[AutoAssign] ⚠ QRObjectPositioner not found (active & enabled)");
            }
        }
    }

    private void EnsureSubscribed()
    {
        if (qrCodeTracker == null || subscribed) return;

        qrCodeTracker.OnQRLost += HandleQRLost;
        subscribed = true;
        LogHit("[Subscribe] ✓ QR Lost event listener registered");
    }

    private void Unsubscribe()
    {
        if (qrCodeTracker != null && subscribed)
        {
            qrCodeTracker.OnQRLost -= HandleQRLost;
            LogHit("[Unsubscribe] ✓ QR Lost event listener unregistered");
        }
        subscribed = false;
    }

    /// <summary>
    /// QR 認識喪失 = ハンマーで叩かれた = 当たり判定成功
    /// </summary>
    private void HandleQRLost(string uuid)
    {
        totalHits++;
        lastHitTime = Time.time;

        LogHit("========================================");
        LogHit($"[HIT_SUCCESS] ★★★ HIT DETECTED ★★★");
        LogHit("========================================");
        LogHit($"[HIT_SUCCESS] QR UUID: {uuid}");
        LogHit($"[HIT_SUCCESS] Total Hits: {totalHits}");
        LogHit($"[HIT_SUCCESS] Time: {lastHitTime:F2}s");

        // QRObjectPositioner が存在する場合、Sphere 削除を確認
        if (qrObjectPositioner != null)
        {
            LogHit($"[HIT_SUCCESS] ✓ QRObjectPositioner will handle Sphere deletion");
        }
        else
        {
            LogWarningHit($"[HIT_SUCCESS] ⚠ QRObjectPositioner not available");
        }

        LogHit("========================================");

        // イベント発火（スコア加算などの処理）
        OnHitSuccess?.Invoke(uuid);

        LogHit($"[HIT_SUCCESS] ✓ OnHitSuccess event invoked");
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
