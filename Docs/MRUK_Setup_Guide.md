# MRUK v78+ QR コード トラッキング セットアップガイド

**対象:** Meta Quest 3 / 3S + Unity  
**実装時間:** 1-2週間  
**難度:** 🟢 低い  
**最終ゴール:** QR コード自動検出 → World 座標取得 → Cube 色変更

---

## フェーズ 1: 環境準備 (1-2日)

### ステップ 1.1: Unity バージョン確認

```
推奨: Unity 2022.3.x LTS または 2023.2.x 以降
現在の設定確認: Edit > Project Settings > Player
```

**最小要件:**
- Unity 2021.3.10f1 以降
- IL2CPP Scripting Backend
- Android Build Support

### ステップ 1.2: Meta XR Core SDK のアップデート

```
Package Manager から:
1. Window > TextAsset and Packages > Package Manager を開く
2. "+" > Add package from git URL
3. 入力: https://github.com/oculus-samples/Unity-MRUtilityKit.git
   OR Package Manager の左側 "Oculus" を検索
```

**確認:**
```
Packages/manifest.json に以下が追加されたことを確認:
"com.meta.xr.mrutilitykit": "78.0.0" 以降
"com.meta.xr.sdk.core": "60.0.0" 以降
```

### ステップ 1.3: XR Plug-in Management の確認・設定

```
Edit > Project Settings > XR Plug-in Management
```

**必須設定:**
- ✅ OpenXR チェックボックスを ON
- ✅ Meta XR Feature Groups を選択
- ✅ 以下を展開して確認:
  - ✅ Quest 3 Support
  - ✅ Hand Tracking Support
  - ✅ Eye Tracking Support (オプション)
  - ✅ Face Tracking Support (オプション)
  - ✅ Passthrough Support (重要)

### ステップ 1.4: Android ビルド設定

```
Edit > Project Settings > Player > Android
```

**確認・設定項目:**
| 項目 | 値 | 理由 |
|------|-----|------|
| **Scripting Backend** | IL2CPP | MRUK 要件 |
| **API Level** | 31 以上 | OpenXR 要件 |
| **Min API Level** | 29 以上 | Meta Quest 要件 |
| **Architecture** | ARM64 | Quest 3/3S のみ対応 |
| **Color Space** | Linear (推奨) | VR 最適化 |

---

## フェーズ 2: シーン構成 (1-2日)

### ステップ 2.1: New Scene を作成

```
Assets > Create > Scene を右クリック > "QR_MR_Scene" を作成
```

### ステップ 2.2: XR Setup (BuildingBlock コンポーネント)

**方法 A: Automatic Setup (推奨)**
```
1. 新規 Scene を開く
2. Menu > Meta > XR > Project Setup Tool を実行
3. "Auto Fix All" をクリック
   → 自動的に以下がセットアップされます:
      - OVRCameraRig
      - OVRManager
      - Meta XR Build Configuration
```

**方法 B: Manual Setup**
```
1. Hierarchy 右クリック > XR > Camera with OVRCameraRig
   → OVRCameraRig が Scene に追加
   
2. Hierarchy に新規 GameObject 作成: "MRUtilityKit"
   
3. Inspector > Add Component > MRUtilityKit
   (Meta.XR.MRUtilityKit 名前空間)
```

### ステップ 2.3: MRUK Manager コンポーネント配置

**手動でコンポーネントを追加:**

```csharp
// Hierarchy の MRUtilityKit GameObject に以下を追加:

// 1. Add Component > MRUtilityKit
// 2. MRUKConfiguration を設定:
//    - Request Anchors On Start: ✅ ON
//    - Request Scenes On Start: ✅ ON (オプション)
```

**Inspector で確認:**
```
MRUtilityKit コンポーネント:
├─ Request Anchors On Start: ✅
├─ Request Scenes On Start: ✅
├─ Disable Logging: ❌ (デバッグ中は OFF)
└─ Prefab Manager: (デフォルトのまま)
```

### ステップ 2.4: QR Code Tracker 用 GameObject を追加

```
Hierarchy 右クリック > Create Empty > "QRCodeTracker" を作成

Inspector で以下を追加:
- Add Component > Script "QRCodeTracker.cs" (次のステップで作成)
```

---

## フェーズ 3: スクリプト実装 (3-5日)

### ステップ 3.1: QRCodeTracker.cs を作成

```
Assets/Scripts/ 右クリック > Create > C# Script > "QRCodeTracker.cs"
```

**コード:**

```csharp
using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MRUK v78+ を使用して QR コードを検出し、
/// world 座標を取得してオブジェクトを配置する例
/// </summary>
public class QRCodeTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject targetCubePrefab;
    [SerializeField] private Material qrDetectedMaterial;

    [Header("Settings")]
    [SerializeField] private float objectHeight = 0.3f;
    [SerializeField] private bool spawnDebugMarker = true;

    private Dictionary<MRUKTrackable, GameObject> trackedQRObjects
        = new Dictionary<MRUKTrackable, GameObject>();

    private MRUKManager mrukManager;

    private void Start()
    {
        Debug.Log("[QRCodeTracker] Initializing...");

        // MRUK Manager 取得
        mrukManager = MRUKManager.Instance;
        if (mrukManager == null)
        {
            Debug.LogError("[QRCodeTracker] MRUKManager not found in scene!");
            return;
        }

        // イベントリスナー登録
        mrukManager.RegisterEventCallbacks(
            onTrackableAdded: OnQRCodeDetected,
            onTrackableRemoved: OnQRCodeLost
        );

        Debug.Log("[QRCodeTracker] ✓ Event callbacks registered");
    }

    /// <summary>
    /// QR コード検出時に呼ばれるコールバック
    /// </summary>
    private void OnQRCodeDetected(MRUKTrackable trackable)
    {
        // Trackable の型を確認 (QR かどうか)
        if (trackable == null)
        {
            Debug.LogWarning("[QRCodeTracker] Null trackable received");
            return;
        }

        // 既に処理済みならスキップ
        if (trackedQRObjects.ContainsKey(trackable))
        {
            return;
        }

        // ★ World 座標を取得
        Vector3 qrWorldPosition = trackable.transform.position;
        Quaternion qrWorldRotation = trackable.transform.rotation;

        Debug.Log($"[QRCodeTracker] ★ QR CODE DETECTED ★");
        Debug.Log($"  Position: {qrWorldPosition}");
        Debug.Log($"  Rotation: {qrWorldRotation.eulerAngles}");

        // オブジェクトを配置
        SpawnTrackedObject(trackable, qrWorldPosition, qrWorldRotation);

        // Cube に色変更を通知
        NotifyTargetCube(trackable.gameObject.name);
    }

    /// <summary>
    /// QR コードが失われた時に呼ばれるコールバック
    /// </summary>
    private void OnQRCodeLost(MRUKTrackable trackable)
    {
        if (trackedQRObjects.TryGetValue(trackable, out GameObject obj))
        {
            Debug.Log($"[QRCodeTracker] QR Code lost, destroying tracked object");
            Destroy(obj);
            trackedQRObjects.Remove(trackable);
        }
    }

    /// <summary>
    /// QR コード位置にオブジェクトを配置
    /// </summary>
    private void SpawnTrackedObject(MRUKTrackable trackable, Vector3 position, Quaternion rotation)
    {
        // z 軸方向に浮かせる
        Vector3 spawnPosition = position + Vector3.up * objectHeight;

        // プレハブがない場合は単純なキューブを生成
        if (targetCubePrefab == null)
        {
            GameObject debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugCube.name = $"QR_Marker_{trackable.gameObject.name}";
            debugCube.transform.position = spawnPosition;
            debugCube.transform.rotation = rotation;
            debugCube.transform.localScale = Vector3.one * 0.1f;

            // Renderer の色を変更
            var renderer = debugCube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.cyan;
            }

            trackedQRObjects[trackable] = debugCube;
        }
        else
        {
            // プレハブから生成
            GameObject instance = Instantiate(
                targetCubePrefab,
                position: spawnPosition,
                rotation: rotation
            );
            instance.name = $"QR_Object_{trackable.gameObject.name}";
            trackedQRObjects[trackable] = instance;
        }

        Debug.Log($"[QRCodeTracker] Object spawned at {spawnPosition}");
    }

    /// <summary>
    /// Scene の Cube に通知して色変更
    /// </summary>
    private void NotifyTargetCube(string qrName)
    {
        // Hierarchy から "Cube" を探す
        GameObject cube = GameObject.Find("Cube");
        if (cube == null)
        {
            Debug.LogWarning("[QRCodeTracker] 'Cube' not found in hierarchy");
            return;
        }

        // CubeColorOnQr スクリプトを探す
        var colorChanger = cube.GetComponent<CubeColorOnQr>();
        if (colorChanger != null)
        {
            // QR 検出イベント通知
            colorChanger.OnQrRecognized(qrName);
            Debug.Log($"[QRCodeTracker] Notified Cube: {qrName}");
        }
        else
        {
            Debug.LogWarning("[QRCodeTracker] CubeColorOnQr component not found on Cube");
        }
    }

    private void OnDestroy()
    {
        // クリーンアップ
        if (mrukManager != null)
        {
            mrukManager.UnregisterEventCallbacks(
                onTrackableAdded: OnQRCodeDetected,
                onTrackableRemoved: OnQRCodeLost
            );
        }
    }
}
```

### ステップ 3.2: CubeColorOnQr.cs を改良

既存の `CubeColorOnQr.cs` を以下で置き換え:

```csharp
using UnityEngine;
using System.Collections;

/// <summary>
/// QR コード認識時に Cube の色を変更するスクリプト
/// MRUK v78+ から通知を受け取る
/// </summary>
public class CubeColorOnQr : MonoBehaviour
{
    [Header("Color Settings")]
    [SerializeField] private Color detectedColor = Color.cyan;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private float colorDuration = 3f;

    private Renderer cubeRenderer;
    private Coroutine colorResetCoroutine;

    private void Start()
    {
        cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            Debug.LogError("[CubeColorOnQr] Renderer component not found!");
            return;
        }

        // 初期色を設定
        ResetToDefault();
        Debug.Log("[CubeColorOnQr] ✓ Initialized");
    }

    /// <summary>
    /// QR コード認識時に呼ばれるメソッド
    /// QRCodeTracker から通知される
    /// </summary>
    public void OnQrRecognized(string qrUuid)
    {
        if (cubeRenderer == null)
        {
            Debug.LogWarning("[CubeColorOnQr] Renderer is null");
            return;
        }

        Debug.Log($"[CubeColorOnQr] ★ QR RECOGNIZED: {qrUuid} ★");

        // 既存のリセット処理をキャンセル
        if (colorResetCoroutine != null)
        {
            StopCoroutine(colorResetCoroutine);
        }

        // UUID から色を生成 (ユニークな色)
        Color qrColor = GenerateColorFromUUID(qrUuid);
        cubeRenderer.material.color = qrColor;

        Debug.Log($"[CubeColorOnQr] Color changed to: {qrColor}");

        // 一定時間後に元の色に戻す
        colorResetCoroutine = StartCoroutine(ResetColorAfterDelay(colorDuration));
    }

    /// <summary>
    /// UUID から一貫性のある色を生成
    /// </summary>
    private Color GenerateColorFromUUID(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
        {
            return detectedColor;
        }

        // UUID の Hash から RGB を生成
        int hash = uuid.GetHashCode();
        float r = ((hash >> 0) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = ((hash >> 16) & 0xFF) / 255f;

        return new Color(r, g, b, 1f);
    }

    /// <summary>
    /// 一定時間後に元の色に戻す
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

        cubeRenderer.material.color = defaultColor;
        Debug.Log($"[CubeColorOnQr] Color reset to default");
    }
}
```

### ステップ 3.3: スクリプト配置

```
Assets/Scripts/ に以下を配置:
✅ QRCodeTracker.cs (新規)
✅ CubeColorOnQr.cs (改良版)
```

---

## フェーズ 4: Scene 設定 (1-2日)

### ステップ 4.1: Scene に QRCodeTracker を割り当て

```
Hierarchy の "QRCodeTracker" GameObject を選択
Inspector > Add Component > QRCodeTracker を追加
```

**Inspector 設定:**
```
QRCodeTracker:
├─ Target Cube Prefab: (空のままでOK、自動で Cube を探す)
├─ QR Detected Material: (オプション、デフォルト OK)
├─ Object Height: 0.3
└─ Spawn Debug Marker: ✅
```

### ステップ 4.2: Cube オブジェクトの確認

```
Hierarchy に "Cube" があることを確認
Inspector > Add Component > CubeColorOnQr を追加
```

**Inspector 設定:**
```
CubeColorOnQr:
├─ Detected Color: Cyan
├─ Default Color: White
└─ Color Duration: 3
```

### ステップ 4.3: MRUtilityKit の確認

```
Hierarchy の "MRUtilityKit" GameObject を選択
Inspector:
├─ Request Anchors On Start: ✅ ON
├─ Request Scenes On Start: ✅ ON (シーン認識が必要な場合)
└─ Disable Logging: ❌ OFF (デバッグ中)
```

---

## フェーズ 5: ビルド・デプロイ (2-3日)

### ステップ 5.1: Android Manifest 設定

```
Assets/Plugins/Android/ に以下の AndroidManifest.xml を配置:
(または自動生成させる)
```

**最小限の設定:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    
    <!-- MRUK v78+ は以下で十分 -->
    <uses-permission android:name="com.oculus.permission.SPATIAL_DATA" />
    
    <!-- Passthrough Camera を使う場合 -->
    <!-- <uses-permission android:name="com.oculus.permission.HEADSET_CAMERA" /> -->
    
    <application />
</manifest>
```

### ステップ 5.2: Build Settings

```
File > Build Settings
```

**設定:**
```
1. Platform: Android
2. Add Open Scenes: QR_MR_Scene を追加
3. Player Settings:
   - Company Name: YourCompany
   - Product Name: Group4_MoleWhack
   - Android Minimum API: 29
   - Android Target API: 33+
   - Scripting Backend: IL2CPP
   - Architecture: ARM64
4. Resolution: 1280x1024 (Quest 推奨)
```

### ステップ 5.3: Clean Build

```
PowerShell:
```

```powershell
# 1. キャッシュをクリア
Remove-Item -Recurse -Force "C:\path\to\project\Library\ScriptAssemblies"
Remove-Item -Recurse -Force "C:\path\to\project\Temp"

# 2. Unity を再起動
# File > Build > Build Android

# 3. ビルド完了後、Quest に接続
adb devices

# 4. APK をアンインストール
adb uninstall com.DefaultCompany.Group4_MoleWhack

# 5. 新しい APK をインストール
adb install -r "path\to\build.apk"
```

---

## フェーズ 6: テスト・デバッグ (3-5日)

### ステップ 6.1: Quest での実行・ログ確認

```
1. Quest にビルド
2. アプリを起動
3. ログキャプチャ:
```

```powershell
adb logcat -s Unity | Tee-Object -FilePath "qr_test_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
```

**期待ログ:**
```
[QRCodeTracker] Initializing...
[QRCodeTracker] ✓ Event callbacks registered
[QRCodeTracker] ★ QR CODE DETECTED ★
[QRCodeTracker]   Position: (0.5, 0.2, -1.2)
[QRCodeTracker]   Rotation: (0, 45, 0)
[CubeColorOnQr] ★ QR RECOGNIZED: qr-uuid-123 ★
[CubeColorOnQr] Color changed to: (0.6, 0.4, 0.8, 1.0)
```

### ステップ 6.2: トラブルシューティング

**Q: QR コードが検出されない**
```
1. QR サイズ: 10cm 以上推奨
2. 印刷品質: 高コントラスト (黒・白)
3. 照明: 十分な明るさ
4. カメラ位置: QR を正面に向ける
5. MRUK Manager: Scene に正しく配置されているか確認
```

**Q: Color が変わらない**
```
1. Cube の Renderer が有効か確認
2. CubeColorOnQr が Cube にアタッチされているか
3. QRCodeTracker が正しくイベントを発火しているか (ログ確認)
```

**Q: ログに何も出ない**
```
1. MRUtilityKit の Log をオンにする
2. MRUKManager.Instance が null でないか確認
3. Manifest の権限設定を確認
```

---

## フェーズ 7: 最適化・運用 (1-2週間)

### ステップ 7.1: パフォーマンス最適化

```csharp
// QRCodeTracker.cs に追加
[SerializeField] private bool enableDynamicTracking = true;

private void Update()
{
    // 必要な時だけ QR 追跡を有効にする
    if (Input.GetKeyDown(KeyCode.Space))
    {
        mrukManager.EnableTracker(enableDynamicTracking);
        enableDynamicTracking = !enableDynamicTracking;
    }
}
```

### ステップ 7.2: ログ管理

```csharp
// リリース版ではログを無効化
#if !UNITY_EDITOR
    mrukManager.DisableLogging = true;
#endif
```

---

## 参考リソース

- 📖 [Meta Developers - MRUK Documentation](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-trackables)
- 📹 [YouTube - Dilmer Valecillos MRUK v78+ Tutorial](https://www.youtube.com/watch?v=OPgn_5V4qJ0)
- 🔧 [GitHub - QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit)
- 📝 [LearnXR Blog](https://blog.learnxr.io/xr-development/qr-code-and-keyboard-tracking-with-meta-mixed-reality-utility-kit)

---

**セットアップ完了時の確認事項:**
- [ ] Unity プロジェクト設定完了
- [ ] MRUK v78+ インストール完了
- [ ] Scene に OVRCameraRig 配置
- [ ] QRCodeTracker.cs と CubeColorOnQr.cs 作成
- [ ] Android Manifest 設定完了
- [ ] Quest 3S でビルド・実行テスト完了
- [ ] QR 検出 → 色変更動作確認

