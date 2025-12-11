# ✅ 4段階検証スクリプト - セットアップ & 実行ガイド

## 📦 デリバラブル

| ファイル | 用途 |
|---------|------|
| `DebugStage1_VisionFrames.cs` | ビジョンシステム確認 |
| `DebugStage2_MRUKSceneLoading.cs` | MRUK シーン読み込み確認 |
| `DebugStage3_OpenXRExtensions.cs` | OpenXR拡張確認 |
| `DebugStage4_AsyncTasks.cs` | 非同期タスク & Fiducial検出確認 |
| `VERIFICATION_GUIDE.md` | 詳細ガイド |

---

## 🚀 クイックスタート

### 1️⃣ **シーン準備**

```
SampleScene.unity に以下を追加:

┌─ GameObject "DEBUG_STAGE1"
│  └─ Component: DebugStage1_VisionFrames
│
├─ GameObject "DEBUG_STAGE2"
│  └─ Component: DebugStage2_MRUKSceneLoading
│
├─ GameObject "DEBUG_STAGE3"
│  └─ Component: DebugStage3_OpenXRExtensions
│
└─ GameObject "DEBUG_STAGE4"
   └─ Component: DebugStage4_AsyncTasks
```

### 2️⃣ **ビルド & デプロイ**

```bash
# Unity Build Settings:
# ✓ Target: Android
# ✓ Architecture: ARM64
# ✓ Graphics API: OpenGLES3
# ✓ Scripting Backend: IL2CPP

# デプロイ:
adb -s 340YC10G7P0CNR install -r App.apk
adb -s 340YC10G7P0CNR shell am start -n com.UnityTechnologies.com.unity.template.urpblank/com.unity3d.player.UnityPlayerGameActivity
```

### 3️⃣ **ログ確認**

```bash
# リアルタイム監視:
adb -s 340YC10G7P0CNR logcat -v time | Select-String "\[STAGE"

# または一括出力:
adb -s 340YC10G7P0CNR logcat > debug_output.txt
```

---

## 📍 各ステージ実行フロー

### **ステージ1: 15秒**
```
Start → OVRManager確認 → Camera確認 → Frame Rate分析 → Complete
         ↓                ↓              ↓
        [✓/✗]           [✓/✗]          16.67ms?
```
**期待結果**: すべて `[✓]`

---

### **ステージ2: 5秒**
```
Start → MRUK Instance確認 → GameObject状態確認 → Anchor検索 → Complete
         ↓                 ↓                     ↓
        NULL?             Active?               Count>0?
```
**期待結果**: Count > 0 (または後で増加)

---

### **ステージ3: 3秒**
```
Start → XR_FB_scene確認 → Fiducial拡張確認 → Permission確認 → Complete
         ↓              ↓                   ↓
        [✓/✗]         [✓/✗]              GRANTED?
```
**期待結果**: すべて `[✓]`

---

### **ステージ4: 35秒**
```
Start → 初期状態記録 (30秒待機) → 非同期ロード確認 → 手動LoadScene試行 → Complete
         ↓            ↓          ↓                   ↓
        Count?      Count増加?   成功?              最終Count確認
```
**期待結果**: Count が増加するか、最終的に > 0

---

## 🎯 問題診断マトリックス

| ステージ | 失敗症状 | 原因 | 修正方法 |
|---------|---------|------|---------|
| 1 | `[✗] OVRManager` | XR未設定 | Project Settings → XR Plug-in Management |
| 1 | `[✗] Camera` | VR Camera配置なし | OVRCameraRig プレハブ追加 |
| 2 | `Found 0 MRUKAnchor` | LoadSceneOnStartup無効 | MRUK_Manager → LoadSceneOnStartup = ✓ |
| 2 | `MRUK.Instance NULL` | MRUK_Manager未配置 | シーンに MRUK_Manager 追加 |
| 3 | `[✗] XR_FB_scene` | 拡張未有効 | XR Plug-in Management で有効化 |
| 4 | `Still 0 anchors` | システムレベルで未検出 | ステージ1-3をすべて通す |

---

## 💾 ログ保存コマンド

```powershell
# ステージ1のみ実行 (15秒キャプチャ):
adb logcat -c; 
Start-Sleep -Seconds 15; 
adb logcat > stage1_output.txt

# すべてのステージ実行 (2分キャプチャ):
adb logcat -c; 
Start-Sleep -Seconds 120; 
adb logcat > full_debug.txt

# 特定パターンのみ抽出:
adb logcat | Select-String -Pattern "STAGE|FiducialTracking|MRUK" | Out-File debug_filtered.txt
```

---

## 🔧 手動検証コマンド

```csharp
// Unity Console で以下を実行 (Play Mode中):

// 1. MRUK存在確認
Debug.Log(MRUK.Instance != null ? "[✓] MRUK OK" : "[✗] MRUK NULL");

// 2. Anchor個数
var anchors = MRUK.Instance?.GetComponentsInChildren<MRUKAnchor>();
Debug.Log($"Anchors: {(anchors?.Length ?? 0)}");

// 3. 手動シーン読み込み
MRUK.Instance?.LoadScene();
Debug.Log("LoadScene() called");

// 4. 再度確認 (3秒後)
yield return new WaitForSeconds(3f);
anchors = MRUK.Instance?.GetComponentsInChildren<MRUKAnchor>();
Debug.Log($"Anchors after LoadScene: {(anchors?.Length ?? 0)}");
```

---

## 📊 期待される出力パターン

### ✅ **成功パターン**

```
[STAGE1] [✓] OVRManager found
[STAGE1] [✓] Main Camera found
[STAGE2] Found 5 MRUKAnchor components
[STAGE3] [✓] XR_FB_scene ENABLED
[STAGE4] [✓] Anchors loaded asynchronously! Delta: +5
[STAGE4] [SUCCESS] Scene loading working!
```

### ❌ **失敗パターン (ステージ2で停滞)**

```
[STAGE2] [✗] MRUK.Instance is NULL
[STAGE2] Found 0 MRUKAnchor components
[STAGE4] After 30 seconds, still 0 MRUKAnchor objects
```

### ⚠️ **警告パターン (OpenXR未設定)**

```
[STAGE3] [✗] XR_FB_scene DISABLED
[STAGE3] [✗] XR_METAX1_spatial_entity_marker DISABLED
[STAGE3] [⚠] USE_SCENE permission NOT granted
```

---

## 🆘 よくあるトラブル

| 症状 | 原因 | 解決 |
|------|------|------|
| すべてのステージが `[✗]` | Unity未初期化 | Play Mode で十分待機後、Console確認 |
| ステージ1-3は `[✓]` だが ステージ4で0 | システムにQRなし | ビジョン範囲内にQR配置 |
| `loadcat` にSTAGEログなし | スクリプト未実行 | GameObject確認 & Play Mode再開 |
| Frame rate が 30 FPS以下 | GPU/CPU制限 | グラフィック設定低下 |

---

## 🎓 使用方法

1. **初回診断** → すべてのステージを順に実行
2. **失敗確認** → 最初に失敗するステージ = 原因箇所
3. **修正** → 該当ステージのガイド参照して修正
4. **再検証** → ステージ1から再実行
5. **反復** → 原因が特定されるまで繰り返し

---

**次ステップ**: 
- [ ] 4つのスクリプトをシーンに配置
- [ ] Unity でビルド & デプロイ
- [ ] logcat でステージ1実行 (15秒待機)
- [ ] 最初に失敗するステージ特定
- [ ] VERIFICATION_GUIDE.md 参照して修正

---

**作成**: 2025-12-09  
**対応**: MRUK v81, Unity 6 LTS, Meta Quest
