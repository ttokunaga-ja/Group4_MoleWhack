using UnityEngine;
using System.Collections;

/// <summary>
/// Cube Color On QR - MRUK v78+ 対応版
/// 
/// 役割:
/// - この Cube が紐づく QR UUID を保持
/// - 検出中: UUID からハッシュした色 or 指定色
/// - 喪失時: lostColor に切り替え
/// - 簡易スケール演出 (任意)
/// 
/// 必須:
/// - 同一位置の QR を表す Cube にこのスクリプトをアタッチ
/// - QRObjectPositioner から Initialize(uuid) を呼ぶ
/// - QRManager がシングルトンで存在し、OnQRAdded / OnQRLost を発火する
/// </summary>
public class CubeColorOnQr : MonoBehaviour
{
    [Header("References")]
    [Header("Color Settings")]
    [SerializeField] private Color detectedColor = Color.cyan;
    [SerializeField] private Color lostColor = Color.red;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private float colorDuration = 3f;
    [SerializeField] private bool useHashedColor = true;

    [Header("Visual Feedback")]
    [SerializeField] private bool enableScaleAnimation = true;
    [SerializeField] private float animationScale = 1.2f;
    [SerializeField] private float animationDuration = 0.5f;

    [Header("Logging")]
    [SerializeField] private bool enableLogging = true;

    private Renderer cubeRenderer;
    private Coroutine colorResetCoroutine;
    private Coroutine scaleAnimationCoroutine;
    private Vector3 originalScale;
    private int detectionCount = 0;
    private string boundUuid = null;

    private void OnEnable()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRAdded += OnQrAdded;
            QRManager.Instance.OnQRLost += OnQRLost;
        }
    }

    private void OnDisable()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRAdded -= OnQrAdded;
            QRManager.Instance.OnQRLost -= OnQRLost;
        }
    }

    private void Start()
    {
        Log("[START] CubeColorOnQr initializing...");
        
        cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            LogError("[START] Renderer component not found!");
            return;
        }

        originalScale = transform.localScale;
        ResetToDefault();
    }

    /// <summary>
    /// QRObjectPositioner から UUID を設定する
    /// </summary>
    public void Initialize(string uuid)
    {
        boundUuid = uuid;
        Log($"[INIT] Bound to UUID: {uuid}");
    }

    private void OnQrAdded(QRInfo info)
    {
        if (info == null || string.IsNullOrEmpty(boundUuid)) return;
        if (info.uuid != boundUuid) return;
        OnQrRecognized(info.uuid);
    }

    private void OnQRLost(QRInfo info)
    {
        if (info == null || string.IsNullOrEmpty(boundUuid)) return;
        if (info.uuid != boundUuid) return;

        // 喪失時は lostColor を即時適用（リセット予約は解除）
        if (colorResetCoroutine != null)
        {
            StopCoroutine(colorResetCoroutine);
        }
        if (scaleAnimationCoroutine != null)
        {
            StopCoroutine(scaleAnimationCoroutine);
            transform.localScale = originalScale;
        }

        ApplyColor(lostColor);
        Log($"[QR_LOST] Color changed to lostColor for UUID: {info.uuid}");
    }

    /// <summary>
    /// QR コード認識時のコールバック
    /// QRManager から呼ばれます
    /// </summary>
    public void OnQrRecognized(string qrUuid)
    {
        if (cubeRenderer == null)
        {
            LogWarning("[QR_RECOGNIZED] Renderer is null");
            return;
        }

        detectionCount++;

        Log($"\n========================================");
        Log($"[QR_RECOGNIZED] ★★★ QR CODE #{detectionCount} ★★★");
        Log($"========================================");
        Log($"  QR UUID: {qrUuid}");
        Log($"  Current time: {Time.time:F2}");

        // 既存のリセット処理をキャンセル
        if (colorResetCoroutine != null)
        {
            StopCoroutine(colorResetCoroutine);
        }

        // 既存のスケール アニメーションをキャンセル
        if (scaleAnimationCoroutine != null)
        {
            StopCoroutine(scaleAnimationCoroutine);
            transform.localScale = originalScale;
        }

        // 色を計算
        Color newColor;
        if (useHashedColor && !string.IsNullOrEmpty(qrUuid))
        {
            newColor = GenerateColorFromString(qrUuid);
        }
        else
        {
            newColor = detectedColor;
        }

        // 色を変更
        ApplyColor(newColor);
        Log($"  Color changed to: RGB({newColor.r:F3}, {newColor.g:F3}, {newColor.b:F3})");

        // スケール アニメーション
        if (enableScaleAnimation)
        {
            scaleAnimationCoroutine = StartCoroutine(AnimateScale());
        }

        // リセット処理をスケジュール
        colorResetCoroutine = StartCoroutine(ResetColorAfterDelay(colorDuration));

        Log($"========================================\n");
    }

    /// <summary>
    /// UUID（文字列）からユニークな色を生成
    /// 同じ UUID からは常に同じ色が生成されます
    /// </summary>
    private Color GenerateColorFromString(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return detectedColor;
        }

        // 文字列を数値に変換
        int hash = str.GetHashCode();

        // RGB を抽出
        float r = ((hash >> 0) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = ((hash >> 16) & 0xFF) / 255f;

        // 彩度を上げるため正規化
        float brightness = (r + g + b) / 3f;
        if (brightness < 0.3f)
        {
            r += 0.5f;
            g += 0.5f;
            b += 0.5f;
        }

        return new Color(
            Mathf.Clamp01(r),
            Mathf.Clamp01(g),
            Mathf.Clamp01(b),
            1f
        );
    }

    /// <summary>
    /// スケール アニメーション
    /// </summary>
    private IEnumerator AnimateScale()
    {
        float elapsedTime = 0f;

        // スケール アップ
        while (elapsedTime < animationDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / (animationDuration / 2f);
            transform.localScale = Vector3.Lerp(originalScale, originalScale * animationScale, t);
            yield return null;
        }

        elapsedTime = 0f;

        // スケール ダウン
        while (elapsedTime < animationDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / (animationDuration / 2f);
            transform.localScale = Vector3.Lerp(originalScale * animationScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// 一定時間後に色をリセット
    /// </summary>
    private IEnumerator ResetColorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetToDefault();
    }

    /// <summary>
    /// 色をデフォルトに戻す
    /// </summary>
    public void ResetToDefault()
    {
        if (cubeRenderer == null) return;

        ApplyColor(defaultColor);
        transform.localScale = originalScale;

        if (enableLogging && detectionCount > 0)
        {
            Log($"[RESET] Color reset to default: {defaultColor}");
        }
    }

    private void ApplyColor(Color color)
    {
        cubeRenderer.material.color = color;
    }

    /// <summary>
    /// 検出数を取得
    /// </summary>
    public int GetDetectionCount()
    {
        return detectionCount;
    }

    /// <summary>
    /// ログ出力ヘルパー
    /// </summary>
    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[CubeColorOnQr] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[CubeColorOnQr] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[CubeColorOnQr] {message}");
    }
}
