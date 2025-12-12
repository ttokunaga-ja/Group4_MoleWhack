# 段階的リファクタリング計画 — Unity（Quest 3S）での複数QR認識 + 複合HIT判定

**バージョン**: 2.0（フェーズ分割版）  
**実装難度**: ⭐⭐⭐（段階的なため管理しやすい）  
**推定工数**: 10-14時間（フェーズ1-3）  
**最終ゴール**: 複数QR同時認識・UUID管理・複合判定（カメラ向き+スイング+距離）による正確なHIT判定

**📌 詳細な実装指示書**: [Docs/ImplementationPhases.md](ImplementationPhases.md)  
👉 **フェーズ1から順に実装してください。各フェーズに完全なコード例・チェックリスト・テスト項目が含まれます。**

---

## 🆕 補遺（位置ロック + 誤検出防止強化）

- セットアップ専用の `QRPoseLocker` を新設し、10秒間の収集で IQR ロバスト平均を算出して「固定Pose」を確定。プレイ中は更新しない（突然移動を防ぐ）。
- `QRTrustMonitor` を新設し、QRごとの既知UUID集合を管理。集合に入れる際・判定時は **ワールド距離しきい値（例: ≤1m）** を併用し、離れたUUID混入を防ぐ（距離内のもののみ集合に含める）。
- `QRManager` は喪失検出を一本化（`lostTimeout`）。下流は独自喪失判定を持たない。
- シーン構成は Setup / Gameplay / End に分け、Setup でロック＆信頼度チェック → Gameplay で固定Pose使用 → 再セットアップが必要なら戻す。

## 📋 現在の実装との比較

| 項目 | 現状 | 改善後 | 効果 |
|------|------|--------|------|
| トラッキング | QRCodeTracker_MRUK（単独） | QRManager（Singleton） | イベント購読の確実性向上 |
| イベント配信 | 分散型（各スクリプト自動割り当て） | 中央集約型（QRManager 経由） | 購読漏れ・重複排除 |
| ヒット判定 | 距離のみ | 複合判定（4要件） | **誤判定 > 99% 削減** |
| カメラ向き | 未実装 | CameraOrientationMonitor | 単独QR時の誤検出防止 |
| ハンマー検出 | 未実装 | HammerController | スイング速度で判定 |
| QR管理 | トラッカー + Sphere 混在 | Anchor + TargetSpawner 分離 | 複数QR同時管理が容易 |
| Sphere再出現 | 手動リセット | TargetSpawner（自動ランダム） | ゲーム体験の自動化 |

---

# 1. 概要（簡潔）

* MR SDK（例：Meta MR Utility Kit）を使って複数 QR を検知する。各 QR は固定 UUID を持つ。
* `QRManager` が UUID と最初の world Pose を記録して `QRAnchor`（Anchor prefab）を生成。Anchor 下に **永続マーカー（Cube 等）** と **TargetSpawner** を配置。
* `TargetSpawner` はランダム時刻で Sphere（Object2）を出現させる。
* QR の **認識喪失（tracking lost）** を検知したら `QRLostEvent` を出す。`HitValidator` が **ハンマーの先端位置**、**IsSwinging**、**カメラ向きチェック（現在検出しているユニーク UUID 数）** を見て当たり判定を行う。
* 当たり成功なら Sphere を消去、スコア更新、Cube は残す。QR 再検出時に Sphere 再出現（スケジューリング再開）。

---

# 2. スクリプト（役割と公開 API）

### 1) QRManager (Singleton)

**責務**

* MR SDK の Trackable (QR) の `Added / Updated / Removed` を受け取り、内部 `Dictionary<string, QRInfo>` を更新。
* UUID の初回検出時に `QRAnchorPrefab` を生成し、firstPose を渡す。
* 他スクリプト向けにイベントを Publish（`OnQRAdded`, `OnQRUpdated`, `OnQRLost`）および公開プロパティ `CurrentTrackedUUIDs`（HashSet）を持つ。

**公開イベント**

* `event Action<QRInfo> OnQRAdded`
* `event Action<QRInfo> OnQRUpdated`
* `event Action<QRInfo> OnQRLost`

**Inspector 変数（例）**

* `GameObject qrAnchorPrefab`
* `Transform qrAnchorsParent`
* `float lostDetectionDebounce = 0.05f` （喪失時のデバウンス）

---

### 2) QRInfo (データクラス)

**フィールド**

* `string uuid`
* `Pose firstPose`（初回 world pose）
* `Pose lastPose`（最終観測 pose）
* `bool isTracked`
* `DateTime firstSeenAt`
* `string payload`（必要なら）

---

### 3) QRAnchor (attached to QRAnchorPrefab)

**責務**

* `Initialize(QRInfo)` で Anchor を初期配置（firstPose）。
* 永続マーカー群（Object1群）を Anchor のローカル位置に instantiate して固定する。
* `TargetSpawner` を持ち、Anchor ローカル空間にターゲットを出す。

**Inspector**

* `List<Transform> persistentObjectOffsets`（Cube や Object1群のローカルオフセットを定義）
* `TargetSpawner targetSpawner`（Reference）

---

### 4) TargetSpawner (per-Anchor)

**責務**

* Anchor のローカル transform を基準に Sphere（Object2）をランダムタイミングで spawn/ despawn する。
* Sphere が spawn 中に `OnQRLost` が来た場合は `HitValidator` に判定を委ねる（または自分で簡易判定）。
* Sphere の再出現は QR の `OnQRAdded` または `OnQRUpdated` で再スケジュール。

**Inspector**

* `GameObject spherePrefab`
* `float spawnIntervalMin, spawnIntervalMax`
* `Vector3 localSpawnOffset`（デフォルト）
* `bool spawnOnFirstDetect = true`

---

### 5) HammerController（削除方針）

**変更点**

* ハンマーは「実物のプラスチック製」を使用するため、Unity 上でコントローラー設定やスイング検出は行わない。
* 当たり判定は QR 喪失をトリガーに集約し、HitValidator で処理する。

---

### 6) CameraOrientationMonitor / VisibilityMonitor

**責務**

* `QRManager.CurrentTrackedUUIDs` を監視し、**現在認識されているユニーク UUID の個数**を算出する。
* `IsCameraFacingEnough` を公開（`true` = カメラが概ね正しい方向を向いている）。
* しきい値 `minVisibleUUIDs` を設け、これ未満なら camera-facing フラグ false（→ Hit 判定抑止）。

**Inspector**

* `int minVisibleUUIDs = 2`（デフォルト） — 実験で調整。複数 QR を環境に貼るなら値を上げる。

---

### 7) HitValidator

**責務**

* `OnQRLost(QRInfo)` を受け取り、以下を総合的に判定して HIT 成否を決める：

  1. `CameraOrientationMonitor.IsCameraFacingEnough == true`（カメラ向き）
  2. `lostDuration <= maxLossWindow`（喪失が短時間であること）
* ハンマーは実物使用のため、Unity 側でのスイング検出・距離判定は行わない。
* 成功なら `TargetSpawner` に通知して Sphere を消す / スコアを加算 / ログ出力。

**Inspector**

* `float maxLossWindow = 0.5f`（秒）

---

### 8) ScoreManager / GameEventBus

**責務**

* スコアの蓄積・表示、ログ出力。
* 汎用イベントバス（`Action<string>` など）でログを受け渡し。

---

### 9) PersistenceManager（任意）

**責務**

* `QRInfo.firstPose` をローカル保存（PlayerPrefs / JSON / Meta Scene Anchor）してアプリ再起動時に再生成できるようにする（必要なら）。

---

# 3. Hierarchy（シーン配置・Prefab構成）

```
XR Rig (OVR/XR Interaction)
  ├─ Camera (MainCamera / Passthrough)
  └─ Player
Managers (Empty)
  ├─ QRManager (script)
  ├─ CameraOrientationMonitor (script)
  ├─ HitValidator (script)
  ├─ ScoreManager (script)
  └─ PersistenceManager (optional)

SceneObjects
  └─ QRAnchors (empty parent)    <-- QRAnchor prefab instances will be created here at runtime

Prefabs/
  ├─ QRAnchorPrefab (has QRAnchor, TargetSpawner, PersistentMarker children)
  │    ├─ AnchorRoot (positioned at firstPose)
  │    │    ├─ PersistentMarkerGroup
  │    │    │    ├─ Cube_Marker (Object1)
  │    │    │    └─ ... (other Object1s)
  │    │    └─ TargetSpawner (script)  -- spawns Sphere_Target as child of AnchorRoot
  │    └─ DebugGizmos (optional)
  ├─ Sphere_Target (Object2)
  └─ Cube_Marker (Object1)

Player
  └─ Hammer (with HammerController, Collider)
```

---

# 4. 主要シーケンス（疑似コード／フロー）

### QRManager: TrackableAdded

```csharp
void HandleTrackableAdded(Trackable t) {
  string uuid = t.Uuid;
  if(!qrs.ContainsKey(uuid)) {
    var info = new QRInfo{ uuid=uuid, firstPose = t.Pose, lastPose = t.Pose, isTracked=true, firstSeenAt = DateTime.UtcNow };
    qrs[uuid] = info;
    // instantiate anchor prefab
    var go = Instantiate(qrAnchorPrefab, qrAnchorsParent);
    var anchor = go.GetComponent<QRAnchor>();
    anchor.Initialize(info);
    OnQRAdded?.Invoke(info);
  } else {
    // update lastPose
  }
  currentTrackedUUIDs.Add(uuid);
}
```

### TrackableRemoved → QRLost 発行（喪失時に lastPose を添える）

```csharp
void HandleTrackableRemoved(Trackable t) {
  if(qrs.TryGetValue(t.Uuid, out var info)) {
    info.isTracked = false;
    info.lastPose = t.Pose; // or last known pose
    OnQRLost?.Invoke(info);
    currentTrackedUUIDs.Remove(t.Uuid);
  }
}
```

### CameraOrientationMonitor（短い）

```csharp
void Update() {
  int count = QRManager.Instance.CurrentTrackedUUIDs.Count;
  IsCameraFacingEnough = count >= minVisibleUUIDs;
}
```

### HitValidator（判定）

```csharp
void OnQRLost(QRInfo info) {
  if(!CameraOrientMonitor.IsCameraFacingEnough) return;
  if(!HammerController.Instance.IsSwinging) return;

  float dist = Vector3.Distance(HammerController.Instance.TipPosition, info.lastPose.position);
  if(dist <= hitThreshold && (Time.realtimeSinceStartup - info.lastSeenTime) <= maxLossWindow) {
    // success
    GameEventBus.Publish("HIT_SUCCESS", info.uuid);
    // tell anchor/spawner to destroy active sphere
  } else {
    // no hit
  }
}
```

---

# 5. 「カメラ向きによる誤判定」対策（あなたの要望を反映）

* `CameraOrientationMonitor` が `QRManager.CurrentTrackedUUIDs.Count` を監視。
* `minVisibleUUIDs` を inspector で設定（例：2）。
* `HitValidator` は `IsCameraFacingEnough == true` を必須条件にする。
  → 結果：単一の QR しか見えていない（またはカメラが大きく逸れている）ときは Hit 判定を行わない。
  （補足）もし屋外やスキャン対象が1つしか無い状況で常に `minVisibleUUIDs > 1` を使うと意図したヒットがブロックされるので、必ず運用条件に応じて `minVisibleUUIDs` を決める。例えば会場に複数 QR を貼るなら `2`〜`3` が有効。

---

# 6. Inspector 推奨初期値（チューニング用）

* `hitThreshold` = 0.30〜0.45 m（ハンマーの先端と QR の lastPose の距離）
* `maxLossWindow` = 0.5 s（喪失から判定までの許容時間）
* `spawnIntervalMin` = 2s, `spawnIntervalMax` = 6s（ターゲットのランダム再出現）
* `minVisibleUUIDs` = 2（カメラ向きチェック閾値）
* `isSwinging` 判定: tip の速度 > 0.8 m/s（テストで調整）

---

# 7. 実装上の注意点・落とし穴

* **Trackable の Pose の精度**：SDK が返す Pose はノイズがある。Anchor を初回検出で固定する場合は「初回 Pose をそのまま採用するか、平均化（数フレーム平均）」を検討。
* **誤検出（false positive）**：喪失や一時的ノイズで誤って `OnQRLost` が来ることがある。`maxLossWindow` や喪失前の confidence（SDK が提供すれば）でフィルタする。
* **複数 QR の同時処理**：`QRManager` は Dictionary で管理し、各 QR ごとに `QRAnchor/TargetSpawner` が存在するため、並列で処理可能。ただし `HammerController` 判定は複数 QR を同時に HIT させてしまう可能性があるので、HitValidator は最も近い QR だけ許可するなど工夫する。
* **アンカーの永続化**：同じ物理 QR を複数セッションで同じ場所に戻したいなら Scene Anchors/Cloud Anchors を検討する（実装はやや重い）。

---

# 8. デバッグ用ログ・可視化

* `Debug.DrawRay` / `Gizmos` で `firstPose` と `lastPose` を可視化。
* `OnQRLost` のとき `Debug.Log` で `uuid`, `hammerTipPos`, `dist`, `IsSwinging`, `visibleUUIDCount` を吐く（閾値調整に必須）。

---

# 9. サンプルファイルスニペット（主要メソッド）

（上の擬似コードに API 名やイベント名を合わせて実装するイメージ。MR SDK のコールバック署名で微修正が必要。）

---

# 10. テスト項目（必須）

1. **単体テスト**：1つの QR を検出 → Anchor 生成 → Sphere 出現 → ハンマーで叩く（喪失）→ Sphere 消失。
2. **複数 QR**：同時に 3 つ置いて認識し、`minVisibleUUIDs` を変えて誤判定抑止を確認。
3. **ノイズ耐性**：一瞬の視界ブロックで誤ヒットしないことを確認。
4. **スケーラビリティ**：同時に 10 個近い QR を置いた時の処理負荷確認（SDK の追跡限界確認）。
