using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// QRコード トラッキング マネージャー（Singleton）
/// 
/// 責務：
/// - MRUK から Trackable イベントを受け取る
/// - UUID ベースで QR 情報を管理（Dictionary）
/// - 他スクリプト向けにイベント発行
/// - CurrentTrackedUUIDs を公開（カメラ向きチェック用）
/// </summary>
public class QRManager : MonoBehaviour
{
    public static QRManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform targetCubeTransform;
    [SerializeField] private bool enableDetailedLogging = true;

    [Header("Detection Settings")]
    [SerializeField] private float detectionCooldown = 0.5f;
    [SerializeField] private float lostTimeout = 1.0f;

    // ===== イベント定義 =====
    public delegate void OnQRAddedHandler(QRInfo info);
    public delegate void OnQRUpdatedHandler(QRInfo info);
    public delegate void OnQRLostHandler(QRInfo info);

    public event OnQRAddedHandler OnQRAdded;
    public event OnQRUpdatedHandler OnQRUpdated;
    public event OnQRLostHandler OnQRLost;

    // ===== 内部状態 =====
    private readonly Dictionary<string, QRInfo> trackedQRs = new Dictionary<string, QRInfo>();
    private readonly HashSet<string> currentTrackedUUIDs = new HashSet<string>();
    private MRUK mrukInstance;
    private CubeColorOnQr cubeColorChanger;
    private int detectionCount = 0;

    public HashSet<string> CurrentTrackedUUIDs => currentTrackedUUIDs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            LogError("[Awake] Another QRManager already exists. Destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Log("[START] QRManager initializing...");
        
        mrukInstance = MRUK.Instance;
        if (mrukInstance == null)
        {
            LogError("[START] MRUK.Instance is NULL!");
            enabled = false;
            return;
        }

        Log("[START] ✓ MRUK.Instance found");

        // Cube の自動探索
        if (targetCubeTransform == null)
        {
            GameObject cubeObj = GameObject.Find("Cube");
            if (cubeObj != null)
            {
                targetCubeTransform = cubeObj.transform;
                Log("[START] ✓ Found Cube automatically");
            }
        }

        // CubeColorOnQr を取得
        if (targetCubeTransform != null)
        {
            cubeColorChanger = targetCubeTransform.GetComponent<CubeColorOnQr>();
            if (cubeColorChanger != null)
            {
                Log("[START] ✓ CubeColorOnQr component found");
            }
        }

        Log("[START] ✓ Initialization complete");
    }

    private void Update()
    {
        if (mrukInstance == null) return;

        float currentTime = Time.time;

        // MRUK から全 Trackable を取得
        List<MRUKTrackable> allTrackables = new List<MRUKTrackable>();
        mrukInstance.GetTrackables(allTrackables);

        // 現在のフレームで見つかった UUID
        HashSet<string> currentUUIDs = new HashSet<string>();

        // 新規・更新処理
        foreach (var trackable in allTrackables)
        {
            if (trackable == null) continue;

            // トラッキングを喪失している場合は「見えていない」として扱う
            if (!trackable.IsTracked)
            {
                continue;
            }

            string uuid = trackable.gameObject.name;
            currentUUIDs.Add(uuid);
            currentTrackedUUIDs.Add(uuid);

            if (trackedQRs.ContainsKey(uuid))
            {
                // 既存 UUID: 位置更新のみ
                QRInfo info = trackedQRs[uuid];
                info.UpdatePose(new Pose(trackable.transform.position, trackable.transform.rotation));
                OnQRUpdated?.Invoke(info);
            }
            else
            {
                // 新規 UUID: 初回検出イベント
                detectionCount++;
                QRInfo newInfo = new QRInfo(
                    uuid,
                    new Pose(trackable.transform.position, trackable.transform.rotation)
                );
                trackedQRs[uuid] = newInfo;

                Log($"[QR_ADDED] QR Code #{detectionCount}: {uuid}");

                // Cube の色変更
                if (cubeColorChanger != null)
                {
                    cubeColorChanger.OnQrRecognized(uuid);
                }

                OnQRAdded?.Invoke(newInfo);
            }
        }

        // タイムアウトによる喪失処理
        List<string> toRemove = new List<string>();
        foreach (var kvp in trackedQRs)
        {
            string uuid = kvp.Key;
            QRInfo info = kvp.Value;

            if (!currentUUIDs.Contains(uuid) && (currentTime - info.lastSeenTime > lostTimeout))
            {
                toRemove.Add(uuid);
            }
        }

        // 喪失イベント発行
        foreach (var uuid in toRemove)
        {
            QRInfo info = trackedQRs[uuid];
            Log($"[QR_LOST] QR Code lost (timeout): {uuid}");

            // Cube をリセット
            if (cubeColorChanger != null)
            {
                cubeColorChanger.ResetToDefault();
            }

            OnQRLost?.Invoke(info);
            trackedQRs.Remove(uuid);
            currentTrackedUUIDs.Remove(uuid);
        }
    }

    public QRInfo GetQRInfo(string uuid)
    {
        return trackedQRs.ContainsKey(uuid) ? trackedQRs[uuid] : null;
    }

    public int GetTrackedQRCount()
    {
        return trackedQRs.Count;
    }

    private void Log(string message)
    {
        if (enableDetailedLogging)
            Debug.Log($"[QRManager] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[QRManager] {message}");
    }
}
