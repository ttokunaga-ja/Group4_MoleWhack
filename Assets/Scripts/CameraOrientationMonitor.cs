using UnityEngine;

/// <summary>
/// カメラ向きモニター
/// 
/// 責務:
/// - QRManager.CurrentTrackedUUIDs の個数をチェック
/// - minVisibleUUIDs 以上見えている = カメラが正しい向き
/// - HitValidator で参照される
/// </summary>
public class CameraOrientationMonitor : MonoBehaviour
{
    public static CameraOrientationMonitor Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int minVisibleUUIDs = 2;  // 最低何個の QR が見えていれば OK か
    [SerializeField] private bool enableLogging = true;

    public bool IsCameraFacingEnough { get; private set; } = false;
    public int VisibleUUIDCount { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (QRManager.Instance == null) return;

        // 現在見えている UUID 個数を取得
        VisibleUUIDCount = QRManager.Instance.CurrentTrackedUUIDs.Count;

        // カメラ向き判定（閾値チェック）
        bool previousState = IsCameraFacingEnough;
        IsCameraFacingEnough = (VisibleUUIDCount >= minVisibleUUIDs);

        // 状態変化時にログ
        if (previousState != IsCameraFacingEnough)
        {
            Log($"[ORIENTATION] Camera facing state changed: {IsCameraFacingEnough} (Visible UUIDs: {VisibleUUIDCount})");
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
            Debug.Log($"[CameraOrientationMonitor] {message}");
    }
}
