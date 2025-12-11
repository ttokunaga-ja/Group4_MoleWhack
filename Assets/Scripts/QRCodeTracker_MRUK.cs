using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MRUK v78+ QR Code Tracker - UUID Validation Enhanced
/// 
/// QR コードを検出し、World 座標を取得して Cube の色を変更します。
/// また、QR 検出イベントを発行し、他のスクリプトで座標を利用できます。
/// 複数トラッカー対応：UUID ベースで重複検出を抑制します。
/// 
/// セットアップ:
/// 1. Hierarchy に MRUtilityKit GameObject を配置し、MRUtilityKit コンポーネントをアタッチ
/// 2. 新規 GameObject "QRCodeTracker" を作成
/// 3. このスクリプトをアタッチ
/// 4. Inspector で Cube Reference を割り当て
/// </summary>
public class QRCodeTracker_MRUK : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetCubeTransform;
    [SerializeField] private bool enableDetailedLogging = true;

    [Header("Detection Settings")]
    [SerializeField] private float detectionCooldown = 0.5f; // 同一QRの再検出を抑制する時間（秒）
    [SerializeField] private float lostTimeout = 1.0f; // QR喪失判定までの時間（秒）

    // ===== イベント定義 =====
    public delegate void OnQRDetectedHandler(string uuid, Vector3 position, Quaternion rotation);
    public delegate void OnQRLostHandler(string uuid);
    
    public event OnQRDetectedHandler OnQRDetected;
    public event OnQRLostHandler OnQRLost;

    // UUID ベースのトラッキング情報
    private class TrackableInfo
    {
        public MRUKTrackable trackable;
        public float lastSeenTime;
        public Vector3 lastPosition;
        public Quaternion lastRotation;
        public bool eventFired; // 初回検出イベント発火済みフラグ
    }

    private Dictionary<string, TrackableInfo> trackedQRObjects = new Dictionary<string, TrackableInfo>();
    
    private MRUK mrukInstance;
    private int detectionCount = 0;
    private CubeColorOnQr cubeColorChanger;

    private void Start()
    {
        Log("[START] QRCodeTracker_MRUK initializing...");
        Log($"[START] Time.time: {Time.time}");

        // MRUK Instance を取得
        mrukInstance = MRUK.Instance;
        if (mrukInstance == null)
        {
            LogError("[START] MRUK.Instance is NULL!");
            LogError("[START] → Ensure MRUtilityKit GameObject exists in Hierarchy");
            LogError("[START] → Check that MRUtilityKit component is attached");
            enabled = false;
            return;
        }

        Log("[START] ✓ MRUK.Instance found");
        Log($"[START] MRUK Instance address: {mrukInstance.GetHashCode()}");

        // Target Cube が指定されていなければ自動探索
        if (targetCubeTransform == null)
        {
            GameObject cubeObj = GameObject.Find("Cube");
            if (cubeObj != null)
            {
                targetCubeTransform = cubeObj.transform;
                Log("[START] ✓ Found Cube automatically");
            }
            else
            {
                LogWarning("[START] ⚠ Cube not found in scene!");
            }
        }

        // CubeColorOnQr コンポーネントを取得
        if (targetCubeTransform != null)
        {
            cubeColorChanger = targetCubeTransform.GetComponent<CubeColorOnQr>();
            if (cubeColorChanger == null)
            {
                LogWarning("[START] ⚠ CubeColorOnQr component not found on Cube!");
                LogWarning("[START] → Attach CubeColorOnQr_MRUK.cs to the Cube GameObject");
            }
            else
            {
                Log("[START] ✓ CubeColorOnQr component found");
            }
        }

        Log("[START] ✓ Initialization complete. Waiting for QR detection...");
        Log($"[START] enableDetailedLogging: {enableDetailedLogging}");
    }

    private void Update()
    {
        if (mrukInstance == null) return;

        float currentTime = Time.time;

        // MRUK から全 Trackable を取得
        List<MRUKTrackable> allTrackables = new List<MRUKTrackable>();
        mrukInstance.GetTrackables(allTrackables);
        
        // ログレベル：全 trackable の数をモニタリング
        if (allTrackables.Count > 0 && (detectionCount == 0 || detectionCount % 30 == 0))
        {
            Log($"[UPDATE] Total trackables: {allTrackables.Count}, Tracked UUIDs: {trackedQRObjects.Count}");
        }

        // 現在のフレームで見つかった UUID を記録
        HashSet<string> currentUUIDs = new HashSet<string>();

        // 新規検出・更新処理（UUID ベース）
        foreach (var trackable in allTrackables)
        {
            if (trackable == null) continue;

            string uuid = trackable.gameObject.name;
            currentUUIDs.Add(uuid);

            if (trackedQRObjects.ContainsKey(uuid))
            {
                // 既存 UUID: lastSeenTime を更新、位置更新
                TrackableInfo info = trackedQRObjects[uuid];
                info.lastSeenTime = currentTime;
                info.lastPosition = trackable.transform.position;
                info.lastRotation = trackable.transform.rotation;

                // クールダウン期間外なら位置更新イベントを発火（オプション）
                // 今回は初回のみイベントを出すため、eventFired フラグで制御
            }
            else
            {
                // 新規 UUID: 初回検出
                Log($"[UPDATE] New QR UUID detected: {uuid}");
                TrackableInfo newInfo = new TrackableInfo
                {
                    trackable = trackable,
                    lastSeenTime = currentTime,
                    lastPosition = trackable.transform.position,
                    lastRotation = trackable.transform.rotation,
                    eventFired = false
                };
                trackedQRObjects[uuid] = newInfo;

                // 初回検出イベント発火（クールダウンなし）
                OnQRCodeDetected(trackable);
                newInfo.eventFired = true;
            }
        }

        // タイムアウトによる喪失処理
        List<string> toRemove = new List<string>();
        foreach (var kvp in trackedQRObjects)
        {
            string uuid = kvp.Key;
            TrackableInfo info = kvp.Value;

            // 現フレームで見つからず、かつタイムアウト超過
            if (!currentUUIDs.Contains(uuid) && (currentTime - info.lastSeenTime > lostTimeout))
            {
                toRemove.Add(uuid);
            }
        }

        // 喪失イベント発火 & 削除
        foreach (var uuid in toRemove)
        {
            TrackableInfo info = trackedQRObjects[uuid];
            Log($"[UPDATE] QR UUID lost (timeout): {uuid}");
            OnQRCodeLost(info.trackable);
            trackedQRObjects.Remove(uuid);
        }
    }

    /// <summary>
    /// QR コード検出時の処理（UUID 重複抑制付き）
    /// </summary>
    private void OnQRCodeDetected(MRUKTrackable trackable)
    {
        if (trackable == null) return;

        string qrUuid = trackable.gameObject.name;
        Vector3 position = trackable.transform.position;
        Quaternion rotation = trackable.transform.rotation;

        // クールダウンチェック: 既存 UUID で eventFired=true なら、クールダウン期間内は再発火しない
        if (trackedQRObjects.ContainsKey(qrUuid))
        {
            TrackableInfo info = trackedQRObjects[qrUuid];
            float timeSinceLastEvent = Time.time - info.lastSeenTime;
            
            if (info.eventFired && timeSinceLastEvent < detectionCooldown)
            {
                // クールダウン期間内: イベント再発火をスキップ
                return;
            }
        }

        detectionCount++;

        Log($"\n{'='.ToString().PadRight(50, '=')}");
        Log($"[QR_DETECTED] ★★★ QR CODE #{detectionCount} ★★★");
        Log($"{'='.ToString().PadRight(50, '=')}");
        Log($"  UUID: {qrUuid}");
        Log($"  Position: {position}");
        Log($"  Rotation: {rotation.eulerAngles}");
        Log($"  Time: {Time.time:F2}");
        Log($"  Tracked UUIDs: {trackedQRObjects.Count}");
        Log($"  Trackable GameObject: {trackable.gameObject.name}");
        Log($"  Trackable Active: {trackable.gameObject.activeInHierarchy}");

        // Cube の色を変更
        if (cubeColorChanger != null)
        {
            cubeColorChanger.OnQrRecognized(qrUuid);
            Log($"  ✓ Cube color changed");
        }
        else
        {
            LogWarning($"  ⚠ CubeColorChanger is null - color change skipped");
        }

        Log($"{'='.ToString().PadRight(50, '=')}");

        // ===== イベント発行 =====
        int subscriberCount = OnQRDetected?.GetInvocationList().Length ?? 0;
        OnQRDetected?.Invoke(qrUuid, position, rotation);
        Log($"[QR_DETECTED] ✓ OnQRDetected event invoked (Subscribers: {subscriberCount})");
    }

    /// <summary>
    /// QR コード喪失時の処理（UUID バリデーション付き）
    /// </summary>
    private void OnQRCodeLost(MRUKTrackable trackable)
    {
        if (trackable == null) return;

        string qrUuid = trackable.gameObject.name;
        Log($"[QR_LOST] QR code lost: {qrUuid}");

        // Cube をリセット
        if (cubeColorChanger != null)
        {
            cubeColorChanger.ResetToDefault();
            Log($"  ✓ Cube color reset");
        }

        // ===== イベント発行 =====
        int subscriberCount = OnQRLost?.GetInvocationList().Length ?? 0;
        OnQRLost?.Invoke(qrUuid);
        Log($"[QR_LOST] ✓ OnQRLost event invoked (Subscribers: {subscriberCount})");
    }

    /// <summary>
    /// トラッキング中の QR UUID 数を取得
    /// </summary>
    public int GetTrackedQRCount()
    {
        return trackedQRObjects.Count;
    }

    /// <summary>
    /// ログ出力
    /// </summary>
    private void Log(string message)
    {
        if (enableDetailedLogging)
        {
            Debug.Log($"[QRCodeTracker_MRUK] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[QRCodeTracker_MRUK] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[QRCodeTracker_MRUK] {message}");
    }
}
