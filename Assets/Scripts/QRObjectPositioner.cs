using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// QR コード座標にオブジェクトを配置・更新するスクリプト
/// 
/// 複数の QR コードが検出された場合、それぞれの座標に対応するオブジェクトを配置します。
/// 検出中は自動で位置を追跡し、喪失時に自動削除します。
/// 
/// セットアップ:
/// 1. 新規 GameObject "QRObjectPositioner" を作成
/// 2. このスクリプトをアタッチ
/// 3. Prefabs/Cube.prefab と Prefabs/Sphere.prefab をそれぞれ割り当て
/// 4. QRManager (Singleton) が QR イベントを配信するため追加設定なし
/// </summary>
public class QRObjectPositioner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private GameObject respawnPrefab; // CubePrefab -> RespawnPrefab
    [SerializeField] private GameObject enemyPrefab;   // SpherePrefab -> EnemyPrefab
    [SerializeField] private GameObject killedPrefab;  // Defeated / Killed Prefab
    [SerializeField] private bool enablePositioningLogging = true;
    [SerializeField] private bool useLockedPoseOnly = false; // Default to FALSE to ensure Lost logic runs
    [SerializeField] private bool clearOnCollect = true;

    [Header("Positioning Settings")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool rotateWithQR = true;
    [SerializeField] private bool applyRotationOffset = true;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(-90f, 0f, 0f);
    [SerializeField] private bool forceVerticalUp = true; // 常に鉛直上向きに補正するか？
    [SerializeField] private float cubeScale = 0.2f;  // Cube（マーカー）のスケール
    [SerializeField] private float cubeHeightOffset = 0.0f; // Cube の高さオフセット
    [SerializeField] private float sphereScale = 0.15f;  // Sphere（当たり判定用）のスケール
    [SerializeField] private float sphereHeightOffset = 0.35f;  // Sphere を Cube の上に配置するオフセット（0.25 → 0.35）
    [SerializeField] private float killedScale = 0.15f; // やられたモグラのスケール（個別調整用）
    [SerializeField] private float scanDuration = 10.0f; // 最初のスキャン時間

    // QR UUID → 親オブジェクト（Cube + Sphere）の マッピング
    private Dictionary<string, GameObject> qrMarkerObjects = new Dictionary<string, GameObject>();
    // QR UUID → Sphere オブジェクトの マッピング
    private Dictionary<string, GameObject> qrSphereObjects = new Dictionary<string, GameObject>();
     // QR UUID → Killed オブジェクトの マッピング
    private Dictionary<string, GameObject> qrKilledObjects = new Dictionary<string, GameObject>();
    
    // スキャン中のQR情報キャッシュ（平均化用）
    private class QRData 
    { 
        public List<Vector3> positions = new List<Vector3>(); 
        public List<Quaternion> rotations = new List<Quaternion>();
    }
    private Dictionary<string, QRData> pendingQRs = new Dictionary<string, QRData>();
    private bool isScanning = true;
    // QR UUID → 直近のポーズ履歴（時刻付き）
    private class PoseSample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }
    private readonly Dictionary<string, List<PoseSample>> poseHistories = new Dictionary<string, List<PoseSample>>();
    private const float poseHistorySeconds = 5f; // 過去5秒を対象
    private const float iqrOutlierK = 1.5f;      // 外れ値判定の係数

    private bool IsLockedMode => poseLocker != null && useLockedPoseOnly;

    private void Start()
    {
        useLockedPoseOnly = false; // 強制的に無効化（ロスト処理を動かすため）
        LogPos("[START] QRObjectPositioner initializing...");

        LogPos($"[START] Respawn Prefab (Inspector): {(respawnPrefab != null ? respawnPrefab.name : "null")}");
        LogPos($"[START] Enemy Prefab (Inspector): {(enemyPrefab != null ? enemyPrefab.name : "null")}");
        LogPos($"[START] PoseLocker (Inspector): {(poseLocker != null ? poseLocker.name : "null")}");

        // Inspector 未設定なら Resources から読み込みを試みる（最終的に null なら停止）
        if (respawnPrefab == null)
        {
            respawnPrefab = Resources.Load<GameObject>("Prefabs/RespawnPrefab");
            if (respawnPrefab == null)
            {
                respawnPrefab = Resources.Load<GameObject>("Prefabs/Cube");
            }
            LogWarningPos(respawnPrefab != null
                ? "[START] ⚠ respawnPrefab not assigned. Loaded from Resources/Prefabs/RespawnPrefab (or fallback Cube)"
                : "[START] ⚠ respawnPrefab missing (Inspector & Resources).");
        }

        if (enemyPrefab == null)
        {
            enemyPrefab = Resources.Load<GameObject>("Prefabs/EnemyPrefab");
            if (enemyPrefab == null)
            {
                enemyPrefab = Resources.Load<GameObject>("Prefabs/Sphere");
            }
            LogWarningPos(enemyPrefab != null
                ? "[START] ⚠ enemyPrefab not assigned. Loaded from Resources/Prefabs/EnemyPrefab (or fallback Sphere)"
                : "[START] ⚠ enemyPrefab missing (Inspector & Resources).");
        }

        if (killedPrefab == null)
        {
            killedPrefab = Resources.Load<GameObject>("Prefabs/mole_defeated"); // Try specific name
            if (killedPrefab == null) killedPrefab = Resources.Load<GameObject>("Prefabs/RespawnPrefab"); // Fallback
            if (killedPrefab == null) killedPrefab = Resources.Load<GameObject>("Prefabs/Cube"); // Final fallback
            
            LogWarningPos(killedPrefab != null
                ? $"[START] ⚠ killedPrefab not assigned. Loaded: {killedPrefab.name}"
                : "[START] ⚠ killedPrefab missing (Inspector & Resources).");
        }

        if (respawnPrefab == null || enemyPrefab == null)
        {
            LogErrorPos("[START] Prefabs are missing. Please assign Respawn/Enemy prefabs in the Inspector or place them under Resources/Prefabs.");
            enabled = false;
            return;
        }

        if (poseLocker == null)
        {
            poseLocker = FindObjectOfType<QRPoseLocker>();
            if (poseLocker != null)
            {
                LogPos("[START] ✓ Found QRPoseLocker in scene");
            }
        }

        if (poseLocker != null && useLockedPoseOnly)
        {
            poseLocker.OnPoseLocked += HandlePoseLocked;
            poseLocker.OnCollectingStarted += HandleCollectingStarted;
            LogPos("[START] ✓ Using locked poses only (QRPoseLocker)");
        }
        else
        {
            // QRManager のイベントに登録（従来追従モード）
            if (QRManager.Instance != null)
            {
                QRManager.Instance.OnQRAdded += OnQRAdded;
                QRManager.Instance.OnQRUpdated += OnQRUpdated;
                QRManager.Instance.OnQRLost += OnQRLost;
                LogPos("[START] ✓ Registered to QRManager events (live follow mode)");
            }
            else
            {
                LogErrorPos("[START] QRManager instance not found!");
                enabled = false;
                return;
            }
        }

        LogPos("[START] ✓ QRObjectPositioner ready");
        LogPos($"[START] Position Offset: {positionOffset}");
        LogPos($"[START] Rotate With QR: {rotateWithQR}");
        LogPos($"[START] Apply Rotation Offset: {applyRotationOffset}");
        LogPos($"[START] Rotation Offset (Euler): {rotationOffsetEuler}");
        LogPos($"[START] Cube Scale: {cubeScale}");
        LogPos($"[START] Sphere Scale: {sphereScale}");
        LogPos($"[START] Sphere Height Offset: {sphereHeightOffset}");

        // スキャンタイマー開始
        StartCoroutine(ScanPhaseRoutine());
    }

    private System.Collections.IEnumerator ScanPhaseRoutine()
    {
        LogPos($"[SCAN] Starting {scanDuration}s scan phase. No objects will be shown.");
        isScanning = true;
        
        yield return new WaitForSeconds(scanDuration);
        
        LogPos("[SCAN] Scan phase finished. Calculating positions...");
        isScanning = false;

        // 1. 各QRの平均位置・回転を算出
        Dictionary<string, (Vector3 pos, Quaternion rot)> calculatedTargets = new Dictionary<string, (Vector3, Quaternion)>();
        float minY = float.MaxValue; // 最も低い高さを探す

        foreach (var kvp in pendingQRs)
        {
            string uuid = kvp.Key;
            QRData data = kvp.Value;
            if (data.positions.Count == 0) continue;

            // IQRフィルタを用いたロバスト平均位置算出
            Vector3 avgPos = GetRobustAverage(data.positions);

            // 平均回転
            Vector4 avgRotV = Vector4.zero;
            foreach (var r in data.rotations) avgRotV += new Vector4(r.x, r.y, r.z, r.w);
            avgRotV /= data.rotations.Count;
            Quaternion avgRot = new Quaternion(avgRotV.x, avgRotV.y, avgRotV.z, avgRotV.w).normalized;

            calculatedTargets[uuid] = (avgPos, avgRot);

            // 最も低い高さを更新
            if (avgPos.y < minY)
            {
                minY = avgPos.y;
            }
        }

        // 2. 最も低い高さに合わせて全オブジェクトを生成
        if (calculatedTargets.Count > 0)
        {
            LogPos($"[SCAN] Aligning all objects to lowest Y: {minY:F3}");
            foreach (var kvp in calculatedTargets)
            {
                string uuid = kvp.Key;
                var (pos, rot) = kvp.Value;

                // 高さを最低値に合わせる
                Vector3 finalPos = new Vector3(pos.x, minY, pos.z);

                OnQRDetected(uuid, finalPos, rot, force: true);
            }
        }
        
        pendingQRs.Clear();
    }

    private void OnDestroy()
    {
        if (poseLocker != null)
        {
            poseLocker.OnPoseLocked -= HandlePoseLocked;
            poseLocker.OnCollectingStarted -= HandleCollectingStarted;
        }

        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRAdded -= OnQRAdded;
            QRManager.Instance.OnQRUpdated -= OnQRUpdated;
            QRManager.Instance.OnQRLost -= OnQRLost;
            LogPos("[OnDestroy] ✓ Event listeners unregistered");
        }
    }

    // 既存の OnQRDetected を OnQRAdded にリネーム
    private void OnQRAdded(QRInfo info)
    {
        if (info == null)
        {
            LogErrorPos("[QR_ADDED] QRInfo is null");
            return;
        }

        if (respawnPrefab == null || enemyPrefab == null)
        {
            LogErrorPos("[QR_ADDED] Prefabs are missing. Skip instantiation. (Assign in Inspector or put under Resources/Prefabs)");
            return;
        }
        OnQRDetected(info.uuid, info.lastPose.position, info.lastPose.rotation);
    }

    /// <summary>
    /// QR 更新時：位置/回転を最新の pose に追従
    /// </summary>
    private void OnQRUpdated(QRInfo info)
    {
        if (IsLockedMode) return;
        if (info == null) return;
        
        // スキャン中は位置情報の更新（蓄積）のみ行う
        if (isScanning)
        {
            if (!pendingQRs.ContainsKey(info.uuid)) pendingQRs[info.uuid] = new QRData();
            pendingQRs[info.uuid].positions.Add(info.lastPose.position);
            pendingQRs[info.uuid].rotations.Add(info.lastPose.rotation);
            return; 
        }

        if (!qrMarkerObjects.ContainsKey(info.uuid)) return;

        if (respawnPrefab == null || enemyPrefab == null)
        {
            LogErrorPos("[QR_UPDATED] Prefabs are missing. Skip update.");
            return;
        }

        Vector3 finalPosition = info.lastPose.position + positionOffset;
        Quaternion finalRotation = Quaternion.identity;
        if (rotateWithQR)
        {
            finalRotation = info.lastPose.rotation;
            if (applyRotationOffset)
            {
                finalRotation = ApplyRotationOffset(finalRotation);
            }
        }

        // 履歴に追加
        AddPoseSample(info.uuid, info.lastPose.position, info.lastPose.rotation);
        
        // 【重要】スキャン完了後は位置を固定するため、位置更新処理を行わない
        // これにより「QRを動かしていないのにモグラが移動する（ドリフト）」や「動かしてしまった」場合の影響を防ぐ
        // ただし、もし追従させたい場合はここを有効に戻す
        bool shouldUpdatePosition = false; // 固定モード

        if (shouldUpdatePosition)
        {
            // IQR ベースのロバスト平均を算出
            (Vector3 smoothedPos, Quaternion smoothedRot) = GetSmoothedPose(info.uuid, finalPosition, finalRotation);

            GameObject parentObject = qrMarkerObjects[info.uuid];
            if (parentObject == null) return;

            parentObject.transform.position = smoothedPos;
            if (rotateWithQR)
            {
                parentObject.transform.rotation = smoothedRot;
            }

            // Sphere が存在する場合は高さに合わせて配置
            if (qrSphereObjects.TryGetValue(info.uuid, out GameObject sphere) && sphere != null)
            {
                float sphereWorldHeight = cubeHeightOffset + sphereHeightOffset;
                sphere.transform.position = smoothedPos + Vector3.up * sphereWorldHeight;
            }
        }

        // もし Killed オブジェクトがあれば、削除して Enemy を復活させる（再検出）
        // 位置は更新しなくても、状態（生死）の更新は行う
        if (qrKilledObjects.ContainsKey(info.uuid))
        {
            GameObject killed = qrKilledObjects[info.uuid];
            if (killed != null) Destroy(killed);
            qrKilledObjects.Remove(info.uuid);
            
            // Sphere がないはずなので再生成をトリガーするために、キー削除はしないが
            // 下のOnQRDetectedロジック等で再生されるように、ここでは sphere をチェック
            if (!qrSphereObjects.ContainsKey(info.uuid) || qrSphereObjects[info.uuid] == null)
            {
                // 固定モードの場合、OnQRDetected内では引数のposition/rotationではなく
                // 既存のMarker位置を使ってSphereを再生成するため、引数は現在値(finalPosition)で良い
                OnQRDetected(info.uuid, finalPosition, finalRotation, true);
            }
        }

        LogPos($"[QR_UPDATED] Pose updated for QR: {info.uuid}");
    }

    /// <summary>
    /// 履歴にサンプルを追加し、古いものを削除
    /// </summary>
    private void AddPoseSample(string uuid, Vector3 position, Quaternion rotation)
    {
        if (!poseHistories.TryGetValue(uuid, out var list))
        {
            list = new List<PoseSample>();
            poseHistories[uuid] = list;
        }
        list.Add(new PoseSample { time = Time.time, position = position, rotation = rotation });

        float cutoff = Time.time - poseHistorySeconds;
        list.RemoveAll(s => s.time < cutoff);
    }

    /// <summary>
    /// IQR を用いて外れ値を除外したロバスト平均を返す（サンプル不足時は生値）
    /// </summary>
    private (Vector3 position, Quaternion rotation) GetSmoothedPose(string uuid, Vector3 fallbackPos, Quaternion fallbackRot)
    {
        if (!poseHistories.TryGetValue(uuid, out var list) || list.Count < 3)
        {
            return (fallbackPos, fallbackRot);
        }

        // 各軸ごとに IQR で外れ値除外し平均
        float SmoothAxis(Func<Vector3, float> selector)
        {
            var values = list.Select(s => selector(s.position)).OrderBy(v => v).ToList();
            int n = values.Count;
            if (n < 3) return selector(fallbackPos);

            float Q1 = values[(int)(0.25f * (n - 1))];
            float Q3 = values[(int)(0.75f * (n - 1))];
            float IQR = Q3 - Q1;
            float min = Q1 - iqrOutlierK * IQR;
            float max = Q3 + iqrOutlierK * IQR;

            var filtered = values.Where(v => v >= min && v <= max).ToList();
            if (filtered.Count == 0) return selector(fallbackPos);
            return filtered.Average();
        }

        Vector3 smoothedPos = new Vector3(
            SmoothAxis(p => p.x),
            SmoothAxis(p => p.y),
            SmoothAxis(p => p.z)
        );

        // 回転はローパス的に最新と前回平均を Slerp
        // 過去の平均を取るのは難しいため、履歴末尾の回転と fallbackRot を軽く補間
        Quaternion latestRot = list[^1].rotation;
        Quaternion smoothedRot = Quaternion.Slerp(fallbackRot, latestRot, 0.2f);

        return (smoothedPos, smoothedRot);
    }

    /// <summary>
    /// QR 検出時：Cube（マーカー）と Sphere（当たり判定用）を配置または位置を更新
    /// </summary>
    private void OnQRDetected(string uuid, Vector3 position, Quaternion rotation, bool force = false)
    {
        if (IsLockedMode && !force) return;
        
        // スキャン中は位置情報の保存のみ（force=trueの呼び出し以外）
        if (isScanning && !force)
        {
            LogPos($"[SCAN] QR detected during scan: {uuid}");
            if (!pendingQRs.ContainsKey(uuid)) pendingQRs[uuid] = new QRData();
            // 重複チェックはせず、検出された分だけ全て加算して平均精度を高める
            pendingQRs[uuid].positions.Add(position);
            pendingQRs[uuid].rotations.Add(rotation);
            return;
        }

        LogPos($"[QR_DETECTED] UUID: {uuid}");
        LogPos($"[QR_DETECTED] Position: {position}");

        if (respawnPrefab == null || enemyPrefab == null)
        {
            LogErrorPos("[QR_DETECTED] Prefabs are missing. Skip instantiation. (Assign in Inspector or put under Resources/Prefabs)");
            return;
        }

        if (!qrMarkerObjects.ContainsKey(uuid))
        {
            // 新規 QR → Cube（マーカー）と Sphere（当たり判定）を生成
            Vector3 finalPosition = position + positionOffset;
            Quaternion finalRotation = Quaternion.identity;
            if (rotateWithQR)
            {
                finalRotation = rotation;
                if (applyRotationOffset)
                {
                    finalRotation = ApplyRotationOffset(finalRotation);
                }
            }

            // 親オブジェクト生成
            GameObject parentObject = new GameObject($"QR_Marker_{uuid.Substring(0, 8)}");
            parentObject.transform.position = finalPosition;
            parentObject.transform.rotation = finalRotation;
            parentObject.transform.SetParent(transform);



            // Cube（マーカー）生成 - QR 位置表示用（削除されない）
            GameObject cubeMarker = Instantiate(respawnPrefab, parentObject.transform);
            cubeMarker.transform.localPosition = new Vector3(0f, cubeHeightOffset, 0f);
            cubeMarker.transform.localRotation = Quaternion.identity;
            cubeMarker.transform.localScale = respawnPrefab.transform.localScale * cubeScale;
            cubeMarker.name = "RespawnMarker";

            // UUID を紐付けて色を管理
            CubeColorOnQr cubeColor = cubeMarker.GetComponent<CubeColorOnQr>();
            if (cubeColor != null)
            {
                cubeColor.Initialize(uuid);
                cubeColor.OnQrRecognized(uuid); // 初回検出時に色を反映
            }
            else
            {
                LogWarningPos("[QR_DETECTED] CubeColorOnQr not found on Cube prefab");
            }

            // Sphere（当たり判定用）生成 - Cube の上に配置
            GameObject sphere = Instantiate(enemyPrefab);
            float sphereWorldHeight = cubeHeightOffset + sphereHeightOffset;
            sphere.transform.position = finalPosition + Vector3.up * sphereWorldHeight;
            sphere.transform.SetParent(parentObject.transform, true); // world position stays
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = enemyPrefab.transform.localScale * sphereScale;
            sphere.name = "Enemy";

            qrMarkerObjects[uuid] = parentObject;
            qrSphereObjects[uuid] = sphere;

            LogPos($"[QR_POSITIONED] ✓ Marker + Sphere created for QR: {uuid}");
            LogPos($"[QR_POSITIONED]   Parent Name: {parentObject.name}");
            LogPos($"[QR_POSITIONED]   Position: {finalPosition}");
            LogPos($"[QR_POSITIONED]   Total QR Markers: {qrMarkerObjects.Count}");
        }
        else
        {
            // 既存 QR → 位置更新（固定モードなので更新しない）
            // LogPos($"[QR_POSITIONED] Marker update skipped (Locked Position): {uuid}");

            // ただし、Sphere が削除されている場合（再生成ループ）は復活させる必要がある
            GameObject existingObject = qrMarkerObjects[uuid];
            
            // Sphere が削除されている場合は再生成（ループ機能）
            if (!qrSphereObjects.ContainsKey(uuid) || qrSphereObjects[uuid] == null)
            {
                GameObject sphere = Instantiate(enemyPrefab);
                float sphereWorldHeight = cubeHeightOffset + sphereHeightOffset;
                // 位置は既存のマーカー位置を基準にする（QRの現在位置ではない）
                sphere.transform.position = existingObject.transform.position + Vector3.up * sphereWorldHeight;
                sphere.transform.SetParent(existingObject.transform, true); 
                sphere.transform.localRotation = Quaternion.identity;
                sphere.transform.localScale = Vector3.one * sphereScale; // プレハブ非依存
                sphere.name = "Enemy";

                qrSphereObjects[uuid] = sphere;

                LogPos($"[QR_RESPAWN] ✓ Sphere respawned for QR: {uuid}");
            }
        }
    }

    /// <summary>
    /// QR 喪失時：Sphere（当たり判定用）を削除し、KilledPrefab（やられモグラ）を配置
    /// </summary>
    private void OnQRLost(QRInfo info)
    {
        if (IsLockedMode) return;
        if (info == null)
        {
            LogErrorPos("[QR_LOST] QRInfo is null");
            return;
        }

        string uuid = info.uuid;
        LogPos($"[QR_LOST] UUID: {uuid}");

        // スキャン中はロストを無視（まだ生成していないので消すものがない）
        if (isScanning) return;

        // Sphere（当たり判定用）を削除
        if (qrSphereObjects.ContainsKey(uuid))
        {
            GameObject sphereToDestroy = qrSphereObjects[uuid];
            Vector3 lastPos = sphereToDestroy != null ? sphereToDestroy.transform.position : Vector3.zero;
            Quaternion lastRot = sphereToDestroy != null ? sphereToDestroy.transform.rotation : Quaternion.identity;
            Transform parent = sphereToDestroy != null ? sphereToDestroy.transform.parent : null;

            if (sphereToDestroy != null)
            {
                Destroy(sphereToDestroy);
                LogPos($"[QR_REMOVED] ✓ Sphere destroyed and replaced with Killed prefab");
            }
            
            qrSphereObjects.Remove(uuid);

            // Killed Prefab を生成
            if (killedPrefab != null && parent != null)
            {
                // Killedも同じ場所に生成（高さオフセット含む）
                // lastPos は既にオフセット込みの世界座標
                GameObject killed = Instantiate(killedPrefab, lastPos, lastRot, parent);
                killed.name = "KilledMole";
                // 明示的に Vector3.one ベースで計算（プレハブのスケール依存を断ち切る実験）
                // もしプレハブが巨大な場合は killedScale を 0.01 など小さくしてください
                killed.transform.localScale = Vector3.one * killedScale; 
                qrKilledObjects[uuid] = killed;
            }
        }
    }

    private void Update()
    {
        // ランタイムでのスケール調整リアルタイム反映（デバッグ用）
        if (qrKilledObjects.Count > 0)
        {
            foreach (var kvp in qrKilledObjects)
            {
                if (kvp.Value != null)
                {
                    // プレハブ依存ではなく、Inspector指定値のみを適用
                    kvp.Value.transform.localScale = Vector3.one * killedScale;
                }
            }
        }
    }

    /// <summary>
    /// 現在配置されているマーカー数を取得
    /// </summary>
    public int GetPositionedObjectCount()
    {
        return qrMarkerObjects.Count;
    }

    /// <summary>
    /// 現在配置されている Sphere 数を取得
    /// </summary>
    public int GetActiveSphereCount()
    {
        return qrSphereObjects.Count;
    }

    /// <summary>
    /// 全 Sphere を手動削除（デバッグ用）
    /// </summary>
    public void ClearAllSpheres()
    {
        foreach (var sphere in qrSphereObjects.Values)
        {
            if (sphere != null)
            {
                Destroy(sphere);
            }
        }
        qrSphereObjects.Clear();
        LogPos("[CLEAR] ✓ All spheres cleared");
    }

    /// <summary>
    /// 全オブジェクト（Marker + Sphere）を手動削除（デバッグ用）
    /// </summary>
    public void ClearAllObjects()
    {
        foreach (var obj in qrMarkerObjects.Values)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        qrMarkerObjects.Clear();
        qrSphereObjects.Clear();
        LogPos("[CLEAR] ✓ All QR objects cleared");
    }

    /// <summary>
    /// ログ出力（Positioning用）
    /// </summary>
    private void LogPos(string message)
    {
        if (enablePositioningLogging)
        {
            Debug.Log($"[QRObjectPositioner] {message}");
        }
    }

    private void LogWarningPos(string message)
    {
        Debug.LogWarning($"[QRObjectPositioner] {message}");
    }

    private void LogErrorPos(string message)
    {
        Debug.LogError($"[QRObjectPositioner] {message}");
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
        // Locked Pose を一度配置し、プレイ中は更新しない
        LogPos($"[LOCKED_PLACE] UUID: {uuid}");
        OnQRDetected(uuid, pose.position, pose.rotation, true);
    }

    private void HandleCollectingStarted()
    {
        if (clearOnCollect)
        {
            ClearAllObjects();
            poseHistories.Clear();
            LogPos("[COLLECT] Cleared objects for fresh locking");
        }
    }

    private Quaternion ApplyRotationOffset(Quaternion baseRotation)
    {
        // worldRot = qrRot * offset
        Quaternion rot = baseRotation * Quaternion.Euler(rotationOffsetEuler);

        if (forceVerticalUp)
        {
            // Y軸回転成分のみ抽出し、Upベクトルをワールドの真上に強制する
            // これにより、QRが微妙に傾いていてもモグラは垂直に立つ
            Vector3 forward = rot * Vector3.forward;
            // forwardのY成分を無視して水平にし、そこから回転を作る
            Vector3 projectedForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                rot = Quaternion.LookRotation(projectedForward, Vector3.up);
            }
        }
        
        return rot;
    }

    /// <summary>
    /// 座標リストから外れ値(IQR)を除去したロバストな平均を計算
    /// </summary>
    private Vector3 GetRobustAverage(List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0) return Vector3.zero;
        if (positions.Count < 3)
        {
            // サンプルが少なすぎる場合は単純平均
            Vector3 sum = Vector3.zero;
            foreach(var p in positions) sum += p;
            return sum / positions.Count;
        }

        float SmoothAxis(Func<Vector3, float> selector)
        {
            // 値を昇順ソート
            var values = positions.Select(p => selector(p)).OrderBy(v => v).ToList();
            int n = values.Count;
            
            // 四分位点計算
            float Q1 = values[(int)(0.25f * n)];
            float Q3 = values[(int)(0.75f * n)];
            float IQR = Q3 - Q1;
            
            // 外れ値の閾値
            float min = Q1 - 1.5f * IQR;
            float max = Q3 + 1.5f * IQR;

            // 範囲内のデータのみで平均
            var filtered = values.Where(v => v >= min && v <= max).ToList();
            
            if (filtered.Count == 0) return values.Average(); // 全て外れ値なら元の平均
            return filtered.Average();
        }

        return new Vector3(
            SmoothAxis(p => p.x),
            SmoothAxis(p => p.y),
            SmoothAxis(p => p.z)
        );
    }
}
