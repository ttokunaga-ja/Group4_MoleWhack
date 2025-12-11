# MRUK 再初期化確認ガイド

## 概要

MRUK が正常に再初期化され、QR トラッキングが継続されているかを確認するための方法と、シーン内の追跡可能なオブジェクト数をモニタリングする手法をまとめました。

---

## 1. MRUK インスタンスの健全性チェック

### Method 1: Instance Reference Monitoring

```csharp
private MRUK mrukInstance;
private int mrukInstanceHashCode = -1;

private void MonitorMRUKInstance()
{
    if (mrukInstance == null)
    {
        Log("[MRUK_CHECK] MRUK.Instance is NULL - MRUK not initialized!");
        return;
    }
    
    int currentHashCode = mrukInstance.GetHashCode();
    
    // インスタンスが変わったかチェック（再初期化の検出）
    if (mrukInstanceHashCode != currentHashCode)
    {
        Log($"[MRUK_CHECK] ⚠ MRUK Instance changed!");
        Log($"[MRUK_CHECK]   Old hash: {mrukInstanceHashCode}");
        Log($"[MRUK_CHECK]   New hash: {currentHashCode}");
        mrukInstanceHashCode = currentHashCode;
    }
}
```

**利点**: MRUK が再作成されたことを検出できる

---

## 2. Trackable 数の継続監視

### Method 2: Trackable List Monitoring

```csharp
private List<MRUKTrackable> allTrackables = new List<MRUKTrackable>();
private int previousTrackableCount = -1;
private float lastLogTime = 0f;
private float logInterval = 1f; // 1秒ごとにログ

private void MonitorTrackables()
{
    if (mrukInstance == null) return;
    
    // 全 trackable を取得
    allTrackables.Clear();
    mrukInstance.GetTrackables(allTrackables);
    
    // 数が変わったとき or 一定時間ごと
    if (allTrackables.Count != previousTrackableCount || 
        Time.time - lastLogTime > logInterval)
    {
        if (allTrackables.Count != previousTrackableCount)
        {
            Log($"[TRACKABLE_MONITOR] Count changed: {previousTrackableCount} → {allTrackables.Count}");
            previousTrackableCount = allTrackables.Count;
        }
        else if (allTrackables.Count > 0)
        {
            Log($"[TRACKABLE_MONITOR] Stable count: {allTrackables.Count}");
        }
        
        lastLogTime = Time.time;
    }
}
```

**ポイント**: 
- Trackable が 0 になった → 再検出待機中
- Trackable が増加 → 新しい QR が検出された
- 同じ数を維持 → MRUK が正常に稼働

---

## 3. Trackable ライフサイクル監視

### Method 3: Individual Trackable Tracking

```csharp
private Dictionary<MRUKTrackable, float> trackableFirstSeenTime = 
    new Dictionary<MRUKTrackable, float>();

private void MonitorTrackableLifecycle()
{
    if (mrukInstance == null) return;
    
    allTrackables.Clear();
    mrukInstance.GetTrackables(allTrackables);
    
    // 新規 trackable
    foreach (var trackable in allTrackables)
    {
        if (!trackableFirstSeenTime.ContainsKey(trackable))
        {
            trackableFirstSeenTime[trackable] = Time.time;
            Log($"[LIFECYCLE] ✓ NEW trackable detected");
            Log($"[LIFECYCLE]   UUID: {trackable.gameObject.name}");
            Log($"[LIFECYCLE]   Position: {trackable.transform.position}");
        }
    }
    
    // 失われた trackable
    List<MRUKTrackable> toRemove = new List<MRUKTrackable>();
    foreach (var tracked in trackableFirstSeenTime.Keys)
    {
        if (!allTrackables.Contains(tracked))
        {
            toRemove.Add(tracked);
            float lifespan = Time.time - trackableFirstSeenTime[tracked];
            Log($"[LIFECYCLE] ✗ LOST trackable after {lifespan:F2}s");
            Log($"[LIFECYCLE]   UUID: {tracked.gameObject.name}");
        }
    }
    
    foreach (var removed in toRemove)
    {
        trackableFirstSeenTime.Remove(removed);
    }
}
```

**監視項目**:
- Trackable の生存時間
- 検出から喪失までの期間
- Trackable が何個存在していたか

---

## 4. MRUK 再初期化検出スクリプト

### 完全なモニタリング実装

```csharp
using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;

public class MRUKReinitializationMonitor : MonoBehaviour
{
    private MRUK mrukInstance;
    private int previousMRUKHashCode = -1;
    private int previousTrackableCount = -1;
    private float lastCheckTime = 0f;
    private float checkInterval = 1f;
    
    private Dictionary<MRUKTrackable, float> trackableTracking = 
        new Dictionary<MRUKTrackable, float>();
    
    private List<MRUKTrackable> allTrackables = new List<MRUKTrackable>();

    private void Update()
    {
        if (Time.time - lastCheckTime < checkInterval)
            return;
        
        lastCheckTime = Time.time;
        
        CheckMRUKInstance();
        CheckTrackables();
        MonitorTrackableHealth();
    }

    private void CheckMRUKInstance()
    {
        MRUK currentInstance = MRUK.Instance;
        
        if (currentInstance == null)
        {
            Debug.LogError("[MRUK_MONITOR] MRUK.Instance is NULL!");
            mrukInstance = null;
            previousMRUKHashCode = -1;
            return;
        }
        
        int currentHash = currentInstance.GetHashCode();
        
        if (previousMRUKHashCode == -1)
        {
            // 初回チェック
            Debug.Log($"[MRUK_MONITOR] ✓ MRUK initialized (Hash: {currentHash})");
            mrukInstance = currentInstance;
            previousMRUKHashCode = currentHash;
        }
        else if (currentHash != previousMRUKHashCode)
        {
            // 再初期化検出！
            Debug.LogWarning($"[MRUK_MONITOR] ⚠⚠⚠ MRUK RE-INITIALIZED!");
            Debug.LogWarning($"[MRUK_MONITOR]   Old Hash: {previousMRUKHashCode}");
            Debug.LogWarning($"[MRUK_MONITOR]   New Hash: {currentHash}");
            Debug.LogWarning($"[MRUK_MONITOR]   Time: {Time.time}");
            
            // 再初期化時の処理
            OnMRUKReinitialized();
            
            mrukInstance = currentInstance;
            previousMRUKHashCode = currentHash;
        }
    }

    private void CheckTrackables()
    {
        if (mrukInstance == null) return;
        
        allTrackables.Clear();
        mrukInstance.GetTrackables(allTrackables);
        
        if (allTrackables.Count != previousTrackableCount)
        {
            if (previousTrackableCount >= 0)
            {
                Debug.Log($"[MRUK_MONITOR] Trackable count: {previousTrackableCount} → {allTrackables.Count}");
            }
            previousTrackableCount = allTrackables.Count;
        }
    }

    private void MonitorTrackableHealth()
    {
        if (mrukInstance == null) return;
        
        allTrackables.Clear();
        mrukInstance.GetTrackables(allTrackables);
        
        // 新規追跡
        foreach (var trackable in allTrackables)
        {
            if (!trackableTracking.ContainsKey(trackable))
            {
                trackableTracking[trackable] = Time.time;
                Debug.Log($"[TRACKABLE_HEALTH] ✓ New: {trackable.gameObject.name}");
            }
        }
        
        // 喪失検出
        List<MRUKTrackable> toRemove = new List<MRUKTrackable>();
        foreach (var tracked in trackableTracking.Keys)
        {
            if (!allTrackables.Contains(tracked))
            {
                toRemove.Add(tracked);
                float duration = Time.time - trackableTracking[tracked];
                Debug.LogWarning($"[TRACKABLE_HEALTH] ✗ Lost after {duration:F2}s: {tracked.gameObject.name}");
            }
        }
        
        foreach (var removed in toRemove)
        {
            trackableTracking.Remove(removed);
        }
    }

    private void OnMRUKReinitialized()
    {
        // MRUK が再初期化されたときの対応
        Debug.Log("[MRUK_MONITOR] → MRUK reinitialization detected, clearing tracked objects");
        
        trackableTracking.Clear();
        previousTrackableCount = -1;
        allTrackables.Clear();
    }

    public int GetCurrentTrackableCount()
    {
        return allTrackables.Count;
    }

    public string GetMRUKStatus()
    {
        if (mrukInstance == null)
            return "NOT_INITIALIZED";
        
        return $"Trackables: {allTrackables.Count}, Hash: {mrukInstance.GetHashCode()}";
    }
}
```

---

## 5. ログ出力をファイルに保存

### 再初期化の履歴を追跡

```csharp
private List<string> reinitializationHistory = new List<string>();

private void OnMRUKReinitialized()
{
    string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    string entry = $"[{timestamp}] MRUK Reinitialized at Time.time={Time.time:F2}";
    
    reinitializationHistory.Add(entry);
    Debug.Log(entry);
    
    // ファイルに保存
    string path = Application.persistentDataPath + "/mruk_reinit_log.txt";
    System.IO.File.AppendAllText(path, entry + "\n");
}

public void PrintReinitializationHistory()
{
    Debug.Log("=== MRUK Reinitialization History ===");
    foreach (var entry in reinitializationHistory)
    {
        Debug.Log(entry);
    }
    Debug.Log("=====================================");
}
```

---

## 6. 実装手順

### Step 1: スクリプトをシーンに追加

```
Hierarchy:
├── QRCodeTracker (既存)
└── MRUKHealthMonitor (新規 - MRUKReinitializationMonitor.cs をアタッチ)
```

### Step 2: QRCodeTracker_MRUK.cs に統合

```csharp
// QRCodeTracker_MRUK.cs に追加
[SerializeField] private MRUKReinitializationMonitor healthMonitor;

private void Start()
{
    // 既存コード...
    
    healthMonitor = GetComponent<MRUKReinitializationMonitor>();
    if (healthMonitor == null)
    {
        healthMonitor = gameObject.AddComponent<MRUKReinitializationMonitor>();
    }
}
```

### Step 3: ビルドしてテスト

ビルド時のコンソール出力を確認：

```
[MRUK_MONITOR] ✓ MRUK initialized (Hash: 12345678)
[MRUK_MONITOR] Trackable count: 0 → 1
[TRACKABLE_HEALTH] ✓ New: Trackable(Qrcode) abc-def-ghi
[TRACKABLE_HEALTH] ✓ New: Trackable(Qrcode) xyz-123-456
```

---

## 7. 問題診断チェックリスト

| 現象 | 原因 | 対策 |
|------|------|------|
| `MRUK.Instance is NULL` | MRUK が初期化されていない | MRUtilityKit GameObject を確認 |
| Trackable count が 0 のまま | QR が見えていない or MRUK が無効 | カメラを QR に向ける |
| Trackable が 1〜2 回で消える | MRUK の更新間隔が短い | GetTrackables() の呼び出し頻度確認 |
| Hash が頻繁に変わる | MRUK インスタンスが何度も再作成 | Hierarchy の MRUtilityKit の重複確認 |
| Trackable 数が増えない | QR 検出後、新しい QR が見えない | 物理的に 2 番目の QR を配置確認 |

---

## 8. 期待されるログ出力（正常時）

```
[START] QRCodeTracker_MRUK initializing...
[START] ✓ MRUK.Instance found
[START] ✓ Initialization complete. Waiting for QR detection...

[UPDATE] Total trackables: 1, Tracked QR objects: 1
[UPDATE] New trackable detected! Total: 1

[QR_DETECTED] ★★★ QR CODE #1 ★★★
[QR_DETECTED]   UUID: Trackable(Qrcode) xxx-yyy-zzz
[QR_DETECTED]   Position: (2.74, -0.43, 3.21)
[QR_DETECTED]   Tracked Count: 1

[UPDATE] Total trackables: 2, Tracked QR objects: 2
[QR_DETECTED] ★★★ QR CODE #2 ★★★
```

---

## まとめ

MRUK の再初期化を確認するには：

1. **MRUK Instance ハッシュコード** を追跡
2. **Trackable リスト** のカウント変化を監視
3. **Trackable のライフサイクル** を記録
4. **ログファイルに保存** して傾向を分析
5. **Hash 変化時に Alert** を出す

これらを組み合わせることで、MRUK の安定性と QR トラッキングの継続性が確認できます。
