# Unity × Meta Quest 3S で QR コードを認識し、安定した World 座標を得る実装ガイド

**公開日**: 2024年12月  
**対象読者**: Unity MR 開発者（初級〜中級）  
**動作確認環境**: Meta Quest 3S / Unity 6000.2.7f2 / Meta XR SDK v81 / MRUK v81 / URP 17.2.0

---

## 概要
Meta XR Utility Kit (MRUK) で QR コードを検出し、World 座標を安定して取得・利用するまでの実装をまとめました。  
ポイントは「中央集約の QRManager」「喪失タイムアウト」「IQR（四分位範囲）を使ったスムージング」で、カメラ移動時のスパイクや瞬断に強い座標更新を実現しています。

---

## システム構成（最新版）

- **QRManager（Singleton）**  
  - MRUK から `MRUKTrackable` を取得し、UUID をキーに `QRInfo` を管理  
  - イベント: `OnQRAdded / OnQRUpdated / OnQRLost`  
  - `lostTimeout` を設け、一時的な見失いでは喪失扱いにしない

- **QRInfo**  
  - `firstPose`（初回検出時の Pose）と `lastPose`（最新の Pose）を保持
  - `lastSeenTime` で最終観測時刻を記録

- **QRObjectPositioner**  
  - `OnQRUpdated` を購読し、Prefab ベースの Cube/Sphere を最新のワールド座標へ追従  
  - 過去 5 秒の履歴から IQR で外れ値を除去し、ロバスト平均でスムージング  
  - 回転は軽い Slerp でローパス

- **CubeColorOnQr**  
  - 検出/喪失イベントで色を変更し、視認性を向上

---

## コード構成（抜粋パターン）

### QRManager（中央集約）
```csharp
// Assets/Scripts/QRManager.cs
public class QRManager : MonoBehaviour
{
    public static QRManager Instance { get; private set; }
    public event Action<QRInfo> OnQRAdded;
    public event Action<QRInfo> OnQRUpdated;
    public event Action<QRInfo> OnQRLost;

    [SerializeField] private float lostTimeout = 1.0f;
    private readonly Dictionary<string, QRInfo> trackedQRs = new();
    private MRUK mruk;

    private void Start()
    {
        mruk = MRUK.Instance;
        if (mruk == null) { enabled = false; return; }
    }

    private void Update()
    {
        // 1) MRUK から Trackable を取得（IsTracked=false はスキップ）
        // 2) 既存なら lastPose/lastSeenTime を更新 → OnQRUpdated
        // 3) 新規なら QRInfo を生成 → OnQRAdded
        // 4) lostTimeout 超過で OnQRLost → 削除
    }
}
```

### QRInfo（初回と最新の Pose を保持）
```csharp
// Assets/Scripts/QRInfo.cs
public class QRInfo
{
    public string uuid;
    public Pose firstPose;
    public Pose lastPose;
    public float lastSeenTime;

    public QRInfo(string uuid, Pose pose)
    {
        this.uuid = uuid;
        firstPose = pose;
        lastPose = pose;
        lastSeenTime = Time.time;
    }

    public void UpdatePose(Pose newPose)
    {
        lastPose = newPose;
        lastSeenTime = Time.time;
    }
}
```

### QRObjectPositioner（IQR で安定化した配置）
```csharp
// Assets/Scripts/QRObjectPositioner.cs
public class QRObjectPositioner : MonoBehaviour
{
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private float cubeHeightOffset = 0f;
    [SerializeField] private float sphereHeightOffset = 0.35f;
    [SerializeField] private float cubeScale = 0.2f;
    [SerializeField] private float sphereScale = 0.15f;

    private const float poseHistorySeconds = 5f;
    private const float iqrOutlierK = 1.5f;
    private readonly Dictionary<string, List<PoseSample>> poseHistories = new();

    private void Start()
    {
        QRManager.Instance.OnQRAdded += OnQRAdded;
        QRManager.Instance.OnQRUpdated += OnQRUpdated;
        QRManager.Instance.OnQRLost += OnQRLost;
    }

    private void OnQRUpdated(QRInfo info)
    {
        // 履歴追加 → IQR で外れ値除去 → 平均位置
        // Cube/Sphere を最新のロバスト平均位置に追従（回転は軽く Slerp）
    }
}
```

### CubeColorOnQr（視覚フィードバック）
```csharp
// Assets/Scripts/CubeColorOnQr_MRUK.cs
public class CubeColorOnQr : MonoBehaviour
{
    [SerializeField] private Color detectedColor = Color.cyan;
    [SerializeField] private Color lostColor = Color.red;
    public void Initialize(string uuid) { /* QR と紐付け */ }
    public void OnQrRecognized(string uuid) { /* 色変更 */ }
    public void OnQRLost(QRInfo info) { /* lostColor */ }
}
```

---

## セットアップ手順

1. **Hierarchy**  
   - `MRUtilityKit`（MRUK コンポーネント付き）  
   - `QRManager`（シングルトン）  
   - `QRObjectPositioner`（Cube/Sphere Prefab を Inspector に割当）  
   - Cube Prefab に `CubeColorOnQr` を付与（検出/喪失で色変化）

2. **Meta XR Settings**  
   - Scene Understanding / QR Code Tracking を有効化

3. **パラメータの目安**  
   - `lostTimeout`: 1.0s（瞬断に耐える）  
   - `sphereHeightOffset`: Cube の上に Sphere を乗せる高さ  
   - Prefab を `Assets/Resources/Prefabs/` に置けば Inspector 未設定でも自動ロード可

---

## デバッグ

```powershell
adb logcat -s Unity
```

- `QRManager` の `[QR_ADDED] / [QR_UPDATED] / [QR_LOST]` を確認
- `QRObjectPositioner` の `[QR_POSITIONED] / [QR_UPDATED]` で座標追従を確認
- `CubeColorOnQr` の色変化ログで検出/喪失を確認

---

## トラブルシュート

- **Prefab 未設定で無効化される**: `QRObjectPositioner` に Cube/Sphere Prefab を割当（または Resources/Prefabs に配置）  
- **座標がジャンプする**: IQR スムージングを有効化し、`sphereHeightOffset/cubeHeightOffset` を調整  
- **イベントが受け取れない**: `QRManager` がシーンに 1 つだけ存在するか確認  

---

## 今後の改善

- スムージング係数・窓長のチューニング（用途に応じて 3〜5 秒など）  
- 空間アンカー（OVRSpatialAnchor 等）による長期安定化  
- QR ペイロード文字列の取得と、内容ベースの ID 付与（MRUK API 調査）  

