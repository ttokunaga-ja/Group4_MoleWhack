# 🔍 QR検出問題 - 4段階検証ガイド

## 概要
MRUK v81でQR検出が0を返す問題を段階的に診断するための4つのテストスクリプト

---

## 📋 実行手順

### **準備**
1. 4つのデバッグスクリプトをシーンに配置:
   - `DebugStage1_VisionFrames.cs`
   - `DebugStage2_MRUKSceneLoading.cs`
   - `DebugStage3_OpenXRExtensions.cs`
   - `DebugStage4_AsyncTasks.cs`

2. 各スクリプトを**別々のGameObject**にアタッチ（同時実行回避）

3. Unity Build Settings で以下を確認:
   - Target: Android
   - Architecture: ARM64
   - Graphics: OpenGLES3

---

## 🎯 ステージ1: ビジョンフレーム供給確認

**目的**: カメラ/ビジョンシステムがアクティブか確認

**実行方法**:
```
1. DebugStage1_VisionFrames スクリプトをアタッチ
2. ビルド & 実行
3. 5-10秒待機して logcat 確認
```

**期待される出力**:
```
[STAGE1-VisionFrames] [✓] OVRManager.instance found
[STAGE1-VisionFrames] [✓] Main Camera found
[STAGE1-VisionFrames] [✓] OVRCameraRig found
[STAGE1-VisionFrames] Average frame time: 16.67ms (60.0 FPS)
```

**問題診断**:
| 出力 | 意味 | 対応 |
|------|------|------|
| `[✗] OVRManager.instance NOT FOUND` | XR設定なし | Project Settings → XR Plug-in Management確認 |
| `[✗] Main Camera NOT FOUND` | カメラなし | OVRCameraRig に MainCamera タグ付与 |
| `Average frame time > 50ms` | GPU/CPU不足 | グラフィック設定低下 |
| `[✗] PassThrough supported: No` | PassThrough無効 | OpenXR設定確認 |

**✓ ステージ1クリア**: すべて `[✓]` の場合 → ステージ2へ

---

## 🎯 ステージ2: MRUK シーン読み込み確認

**目的**: MRUK_Manager が正しくシーンをロードしているか確認

**実行方法**:
```
1. DebugStage2_MRUKSceneLoading をアタッチ
2. ビルド & 実行
3. 3-5秒待機して logcat 確認
```

**期待される出力**:
```
[STAGE2-MRUKLoading] [✓] MRUK.Instance exists
[STAGE2-MRUKLoading]   - Active: True
[STAGE2-MRUKLoading] [✓] Mesh root exists
[STAGE2-MRUKLoading]   → Mesh child count: N > 0
[STAGE2-MRUKLoading] [✓] Anchors root exists
[STAGE2-MRUKLoading] [✓] Anchors ARE loaded
[STAGE2-MRUKLoading] Found X MRUKAnchor components
```

**問題診断**:
| 出力 | 意味 | 対応 |
|------|------|------|
| `[✗] MRUK.Instance is NULL` | MRUK未初期化 | シーンにMRUK_Manager配置確認 |
| `- Active: False` | MRUK GameObject 無効 | Inspector で有効化 |
| `Found 0 MRUKAnchor components` | **シーン未ロード** | LoadSceneOnStartup有効化またはLoadScene()手動実行 |
| `Mesh child count: 0` | ルームメッシュ未取得 | ステージ1を確認 |

**💡 主要チェック**:
```csharp
// MRUK_Manager (シーン) で確認:
- MRUK Component: Enabled = ✓
- LoadSceneOnStartup = ✓ (有効)
- QRCodeTrackingEnabled = ✓ (有効)
```

**✓ ステージ2クリア**: MRUKAnchor > 0 の場合 → ステージ3へ

---

## 🎯 ステージ3: OpenXR拡張機能確認

**目的**: Fiducial/Scene処理用のOpenXR拡張が有効か確認

**実行方法**:
```
1. DebugStage3_OpenXRExtensions をアタッチ
2. ビルド & 実行
3. 2-3秒待機して logcat 確認
```

**期待される出力**:
```
[STAGE3-OpenXRExt] [OpenXR Runtime Status]
[STAGE3-OpenXRExt] [✓] XR_FB_scene extension ENABLED
[STAGE3-OpenXRExt] [✓] XR_METAX1_spatial_entity_marker extension ENABLED
[STAGE3-OpenXRExt] [Other Extensions Status]
[STAGE3-OpenXRExt]   [✓] XR_EXT_hand_tracking
[STAGE3-OpenXRExt] [✓] Permissions Status
[STAGE3-OpenXRExt] [✓] USE_SCENE permission GRANTED
```

**問題診断**:
| 出力 | 意味 | 対応 |
|------|------|------|
| `[✗] XR_FB_scene extension DISABLED` | 拡張未有効 | Project Settings → XR Plug-in Management → Meta Quest 有効化 |
| `[✗] XR_METAX1_spatial_entity_marker DISABLED` | Fiducial拡張なし | MetaXRFeature設定確認 |
| `[⚠] USE_SCENE permission NOT granted` | パーミッション拒否 | AndroidManifest.xml確認 |

**💡 修正方法** (Project Settings):
```
1. XR Plug-in Management > OpenXR (Android)
2. Meta Quest Support = ✓
3. MetaXRFeature enabled = ✓
4. Features → Spatial Entity Marker = ✓
```

**✓ ステージ3クリア**: すべて `[✓]` の場合 → ステージ4へ

---

## 🎯 ステージ4: 非同期タスク & Fiducial検出確認

**目的**: LoadScene非同期完了とシステム検出結果の変換確認

**実行方法**:
```
1. DebugStage4_AsyncTasks をアタッチ
2. ビルド & 実行
3. 35秒待機して logcat 確認
```

**期待される出力 (成功)**:
```
[STAGE4-AsyncTasks] [Initial State]
[STAGE4-AsyncTasks]   - Initial MRUKAnchor count: 0
[STAGE4-AsyncTasks] [Waiting for Scene Loading Async Tasks]
[STAGE4-AsyncTasks]   [5.0s] Current MRUKAnchor count: 0
[STAGE4-AsyncTasks]   [10.0s] Current MRUKAnchor count: 5
[STAGE4-AsyncTasks] [✓] Anchors loaded asynchronously! Delta: +5
[STAGE4-AsyncTasks] [✓] Found 5 anchors:
[STAGE4-AsyncTasks]     0. UUID: XXXX..., Type: QrCode
[STAGE4-AsyncTasks] [SUCCESS] Scene loading working!
```

**問題診断**:
| 出力 | 意味 | 対応 |
|------|------|------|
| `After 30 seconds, still 0 MRUKAnchor objects` | **Async完了失敗** | ステージ1,2,3確認後も0なら構造的問題 |
| `Manual LoadScene() did not create any anchors` | 手動ロード失敗 | LoadSceneOnStartup有効化 |
| `No anchors found after all attempts` | **完全に詰んでる状態** | 以下対応参照 |

**詳細ログ確認** (logcat):
```bash
# 以下パターンを検索:
adb logcat | grep -E "(FiducialTracking|SceneManager|MRUK|Queried.*fiducials)"

# 成功時:
"Queried N anchored fiducials" (N > 0)

# 失敗時:
"Queried 0 anchored fiducials"  ← これが見えたら原因特定開始
```

---

## 🔴 すべてのステージで失敗した場合

### **診断フローチャート**

```
├─ ステージ1失敗
│  ├─→ OVRManager/Camera 無効
│  └─→ 対応: XR Plugin Management 再設定
│
├─ ステージ2失敗
│  ├─→ MRUK GameObject 無効
│  ├─→ LoadSceneOnStartup 無効
│  └─→ 対応: MRUK_Manager Inspector確認 & 有効化
│
├─ ステージ3失敗
│  ├─→ OpenXR拡張未設定
│  ├─→ USE_SCENE権限なし
│  └─→ 対応: Project Settings再設定
│
└─ ステージ4失敗
   ├─→ 非同期タスク未完了
   ├─→ システム検出結果が0
   └─→ 対応: MRUK.LoadScene()手動実行テスト
```

### **最終確認項目**

```csharp
// プレイモード時に Console で実行:
Debug.Log(MRUK.Instance != null ? "OK" : "MRUK NULL");
Debug.Log(MRUK.Instance.GetComponentsInChildren<MRUKAnchor>().Length);

// 期待値: Length > 0
```

---

## 📊 データ収集テンプレート

```
テスト日時: ____年__月__日 __:__
デバイス: Meta Quest __
MRUK Version: __
Unity Version: __

【ステージ1】ビジョンフレーム
┌─ OVRManager: [✓] [✗]
├─ Main Camera: [✓] [✗]
├─ OVRCameraRig: [✓] [✗]
├─ Frame Time: ___ms (FPS: ___)
└─ PassThrough: [✓] [✗]

【ステージ2】MRUK シーン読み込み
┌─ MRUK Instance: [✓] [✗]
├─ MRUK Active: [✓] [✗]
├─ Mesh Loaded: [✓] [✗] (count: ___)
├─ Anchors Loaded: [✓] [✗] (count: ___)
└─ MRUKAnchor Components: ___

【ステージ3】OpenXR拡張
┌─ XR_FB_scene: [✓] [✗]
├─ XR_METAX1_spatial_entity_marker: [✓] [✗]
├─ XR_EXT_hand_tracking: [✓] [✗]
└─ USE_SCENE Permission: [✓] [✗]

【ステージ4】非同期タスク
┌─ Initial Anchors: ___
├─ After 30s Wait: ___
├─ Manual LoadScene(): [✓] [✗]
└─ Final Anchors: ___

【結論】
次のステップ: ___________
```

---

## 📞 サポート情報

各ステージの詳細ログは以下で確認:

```bash
# リアルタイムログ
adb logcat -v time | grep -E "(STAGE|MRUK|Fiducial|Scene)"

# 保存して後で確認
adb logcat > debug_output.log

# 特定パターンだけ
adb logcat | grep "\[STAGE"
```

---

**作成日**: 2025-12-09  
**対応MRUK**: v81.0.0  
**対応Unity**: 6 LTS
