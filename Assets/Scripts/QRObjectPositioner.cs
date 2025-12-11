using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// QR コード座標にオブジェクトを配置・更新するスクリプト
/// 
/// 複数の QR コードが検出された場合、それぞれの座標に対応するオブジェクトを配置します。
/// 検出中は自動で位置を追跡し、喪失時に自動削除します。
/// 
/// セットアップ:
/// 1. 新規 GameObject "QRObjectPositioner" を作成
/// 2. このスクリプトをアタッチ
/// 3. Inspector で QRCodeTracker_MRUK を割り当て
/// 4. "ObjectPrefab" に表示したいオブジェクト（キューブやスフィア）を割り当て
/// </summary>
public class QRObjectPositioner : MonoBehaviour
{
    [Header("References")]
    [SerializeField, HideInInspector] private QRCodeTracker_MRUK qrCodeTracker;
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private bool enablePositioningLogging = true;

    [Header("Positioning Settings")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool rotateWithQR = true;
    [SerializeField] private float cubeScale = 0.2f;  // Cube（マーカー）のスケール
    [SerializeField] private float sphereScale = 0.15f;  // Sphere（当たり判定用）のスケール
    [SerializeField] private float sphereHeightOffset = 0.35f;  // Sphere を Cube の上に配置するオフセット（0.25 → 0.35）

    // QR UUID → 親オブジェクト（Cube + Sphere）の マッピング
    private Dictionary<string, GameObject> qrMarkerObjects = new Dictionary<string, GameObject>();
    // QR UUID → Sphere オブジェクトの マッピング
    private Dictionary<string, GameObject> qrSphereObjects = new Dictionary<string, GameObject>();

    private bool subscribed = false;

    private void Start()
    {
        LogPos("[START] QRObjectPositioner initializing...");

        // QRCodeTracker_MRUK が指定されていなければ自動探索
        AutoAssignTrackerIfNeeded();

        // オブジェクト Prefab が指定されていなければ デフォルト生成
        if (objectPrefab == null)
        {
            LogWarningPos("[START] ⚠ objectPrefab not assigned. Creating default cube...");
            objectPrefab = CreateDefaultObjectPrefab();
        }

        LogPos("[START] ✓ QRObjectPositioner ready");
        LogPos($"[START] Position Offset: {positionOffset}");
        LogPos($"[START] Rotate With QR: {rotateWithQR}");
        LogPos($"[START] Cube Scale: {cubeScale}");
        LogPos($"[START] Sphere Scale: {sphereScale}");
        LogPos($"[START] Sphere Height Offset: {sphereHeightOffset}");
    }

    private void OnEnable()
    {
        // QR 検出・喪失イベントを購読
        AutoAssignTrackerIfNeeded();
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        // イベント登録解除
        Unsubscribe();
    }

    private void OnValidate()
    {
        // インスペクタ上の参照抜けを防ぐため、自動でシーン内のトラッカーを拾う
        AutoAssignTrackerIfNeeded();
    }

    private void AutoAssignTrackerIfNeeded()
    {
        // 既存参照が無効（Disable や非アクティブ）なら捨てて再検索する
        if (qrCodeTracker != null && (!qrCodeTracker.enabled || !qrCodeTracker.gameObject.activeInHierarchy))
        {
            LogWarningPos("[AutoAssign] Existing QRCodeTracker is disabled or inactive. Reassigning...");
            qrCodeTracker = null;
        }

        if (qrCodeTracker == null)
        {
            qrCodeTracker = FindFirstObjectByType<QRCodeTracker_MRUK>();
            if (qrCodeTracker != null)
            {
                LogPos("[AutoAssign] ✓ QRCodeTracker_MRUK auto-assigned");
            }
            else
            {
                LogErrorPos("[AutoAssign] ✖ QRCodeTracker_MRUK not found (active & enabled) - positioning disabled");
            }
        }
    }

    private void EnsureSubscribed()
    {
        if (qrCodeTracker == null || subscribed) return;

        qrCodeTracker.OnQRDetected += OnQRDetected;
        qrCodeTracker.OnQRLost += OnQRLost;
        subscribed = true;
        LogPos("[Subscribe] ✓ Event listeners registered");
    }

    private void Unsubscribe()
    {
        if (qrCodeTracker != null && subscribed)
        {
            qrCodeTracker.OnQRDetected -= OnQRDetected;
            qrCodeTracker.OnQRLost -= OnQRLost;
            LogPos("[Unsubscribe] ✓ Event listeners unregistered");
        }
        subscribed = false;
    }

    /// <summary>
    /// QR 検出時：Cube（マーカー）と Sphere（当たり判定用）を配置または位置を更新
    /// </summary>
    private void OnQRDetected(string uuid, Vector3 position, Quaternion rotation)
    {
        LogPos($"[QR_DETECTED] UUID: {uuid}");
        LogPos($"[QR_DETECTED] Position: {position}");

        if (!qrMarkerObjects.ContainsKey(uuid))
        {
            // 新規 QR → Cube（マーカー）と Sphere（当たり判定）を生成
            Vector3 finalPosition = position + positionOffset;
            Quaternion finalRotation = rotateWithQR ? rotation : Quaternion.identity;

            // 親オブジェクト生成
            GameObject parentObject = new GameObject($"QR_Marker_{uuid.Substring(0, 8)}");
            parentObject.transform.position = finalPosition;
            parentObject.transform.rotation = finalRotation;
            parentObject.transform.SetParent(transform);

            // Cube（マーカー）生成 - QR 位置表示用（削除されない）
            GameObject cubeMarker = CreateCubeMarker();
            cubeMarker.transform.SetParent(parentObject.transform);
            cubeMarker.transform.localPosition = Vector3.zero;
            cubeMarker.transform.localRotation = Quaternion.identity;
            cubeMarker.transform.localScale = Vector3.one * cubeScale;
            cubeMarker.name = "CubeMarker";

            // Sphere（当たり判定用）生成 - Cube の上に配置
            GameObject sphere = CreateSphereWithCollider();
            sphere.transform.SetParent(parentObject.transform);
            sphere.transform.localPosition = new Vector3(0, sphereHeightOffset, 0);
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one * sphereScale;
            sphere.name = "CollisionSphere";

            qrMarkerObjects[uuid] = parentObject;
            qrSphereObjects[uuid] = sphere;

            LogPos($"[QR_POSITIONED] ✓ Marker + Sphere created for QR: {uuid}");
            LogPos($"[QR_POSITIONED]   Parent Name: {parentObject.name}");
            LogPos($"[QR_POSITIONED]   Position: {finalPosition}");
            LogPos($"[QR_POSITIONED]   Total QR Markers: {qrMarkerObjects.Count}");
        }
        else
        {
            // 既存 QR → 位置更新
            Vector3 finalPosition = position + positionOffset;
            Quaternion finalRotation = rotateWithQR ? rotation : Quaternion.identity;

            GameObject existingObject = qrMarkerObjects[uuid];
            existingObject.transform.position = finalPosition;
            if (rotateWithQR)
            {
                existingObject.transform.rotation = finalRotation;
            }

            // Sphere が削除されている場合は再生成（ループ機能）
            if (!qrSphereObjects.ContainsKey(uuid) || qrSphereObjects[uuid] == null)
            {
                GameObject sphere = CreateSphereWithCollider();
                sphere.transform.SetParent(existingObject.transform);
                sphere.transform.localPosition = new Vector3(0, sphereHeightOffset, 0);
                sphere.transform.localRotation = Quaternion.identity;
                sphere.transform.localScale = Vector3.one * sphereScale;
                sphere.name = "CollisionSphere";

                qrSphereObjects[uuid] = sphere;

                LogPos($"[QR_RESPAWN] ✓ Sphere respawned for QR: {uuid}");
                LogPos($"[QR_RESPAWN]   New Sphere Count: {qrSphereObjects.Count}");
            }

            LogPos($"[QR_POSITIONED] ✓ Marker updated for QR: {uuid}");
            LogPos($"[QR_POSITIONED]   New Position: {finalPosition}");
        }
    }

    /// <summary>
    /// QR 喪失時：Sphere（当たり判定用）のみを削除、Cube（マーカー）は残す
    /// </summary>
    private void OnQRLost(string uuid)
    {
        LogPos($"[QR_LOST] UUID: {uuid}");
        LogPos($"[QR_LOST] qrSphereObjects.ContainsKey: {qrSphereObjects.ContainsKey(uuid)}");

        // Sphere（当たり判定用）を削除
        if (qrSphereObjects.ContainsKey(uuid))
        {
            GameObject sphereToDestroy = qrSphereObjects[uuid];
            
            if (sphereToDestroy == null)
            {
                LogWarningPos($"[QR_LOST] ⚠ Sphere reference is NULL for UUID: {uuid}");
            }
            else
            {
                string sphereName = sphereToDestroy.name;
                LogPos($"[QR_LOST] Destroying Sphere: {sphereName} (Active: {sphereToDestroy.activeInHierarchy})");
                
                Destroy(sphereToDestroy);
                LogPos($"[QR_REMOVED] ✓ Sphere destroyed: {sphereName}");
            }
            
            qrSphereObjects.Remove(uuid);
            LogPos($"[QR_REMOVED]   Remaining Spheres: {qrSphereObjects.Count}");
            LogPos($"[QR_REMOVED]   ⚠ Cube marker remains for position reference");
        }
        else
        {
            LogWarningPos($"[QR_LOST] ⚠ No Sphere found for UUID: {uuid}");
        }

        // 注意: Cube マーカーは削除しない（QR 位置の視覚的参照のため）
    }

    /// <summary>
    /// デフォルトオブジェクト Prefab を生成（キューブ）
    /// </summary>
    private GameObject CreateDefaultObjectPrefab()
    {
        GameObject prefab = new GameObject("DefaultQRObjectPrefab");
        
        // キューブメッシュを追加
        MeshFilter meshFilter = prefab.AddComponent<MeshFilter>();
        meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        
        // マテリアルを追加
        MeshRenderer meshRenderer = prefab.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.cyan;
        meshRenderer.material = material;
        
        // Collider を追加
        prefab.AddComponent<BoxCollider>();
        
        prefab.SetActive(false);  // Prefab なので非アクティブ化
        
        LogPos("[DEFAULT] ✓ Default cube prefab created");
        return prefab;
    }

    /// <summary>
    /// Cube マーカーを生成（QR 位置表示用、削除されない）
    /// </summary>
    private GameObject CreateCubeMarker()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        // すべての Collider を削除
        foreach (var collider in cube.GetComponents<Collider>())
        {
            Destroy(collider);
        }
        
        // すべての MeshRenderer のマテリアルを設定
        MeshRenderer[] renderers = cube.GetComponentsInChildren<MeshRenderer>();
        Color cubeColor = new Color(0.3f, 0.8f, 1.0f, 0.7f);  // 半透明の青
        
        foreach (var renderer in renderers)
        {
            // 各 renderer のすべてのマテリアルを変更
            Material[] materials = new Material[renderer.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = new Material(renderer.materials[i].shader);
                mat.color = cubeColor;
                materials[i] = mat;
            }
            renderer.materials = materials;
        }
        
        LogPos("[CREATE] ✓ Cube marker created (Color: Cyan)");
        return cube;
    }

    /// <summary>
    /// Sphere（視覚的ターゲット）を生成
    /// 物理衝突は使用せず、QR 認識喪失による当たり判定のみ
    /// </summary>
    private GameObject CreateSphereWithCollider()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        
        // すべての Collider を削除
        foreach (var collider in sphere.GetComponents<Collider>())
        {
            Destroy(collider);
        }
        
        // すべての MeshRenderer のマテリアルを設定
        MeshRenderer[] renderers = sphere.GetComponentsInChildren<MeshRenderer>();
        Color sphereColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);  // 黄色（ターゲット表示）
        
        foreach (var renderer in renderers)
        {
            // 各 renderer のすべてのマテリアルを変更
            Material[] materials = new Material[renderer.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = new Material(renderer.materials[i].shader);
                mat.color = sphereColor;
                materials[i] = mat;
            }
            renderer.materials = materials;
        }
        
        LogPos("[CREATE] ✓ Visual target sphere created (Color: Yellow, no physics)");
        return sphere;
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
}
