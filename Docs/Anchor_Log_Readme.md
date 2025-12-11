# Anchor ログ解析・現状まとめ

## 概要
- 対象プロジェクト: `Group4_MoleWhack`
- 目的: Meta Quest 上で「システムに保存されているアンカー数」と「Unity 側で取得されるアンカー数」に差がある原因解析と対処の手順整理。

## 現時点の状況（短く）
- デバイスのストレージ上には常に **42** アンカーが存在する（ログ: `listAllAnchors listed 42 anchors` が多数）。
- SLAM/LAD によって「localizable（局所化可能）」と判定されるアンカーはおおよそ **17** 件（ログ: `applyFilters: num Slam LAD results: 17`）。
- Unity 側の取得は実行タイミングで変動するが、改善は見られ、`Fetched 25/30/40 total anchors` のような出力が確認されている。ただし安定して40件取得できるわけではない。

## 問題点（技術的要約）
- SDK のフィルタリング／FetchOptions と SLAM/LAD のタイミングが主因で、アンカーが "not localizable" と扱われるとアプリ側で除外される。
- LAD の局所化状態は非同期で変化するため、フェッチ時点と LAD 更新タイミングが合致しないと取得件数が減る。
- 既に下記対策を実装済み:
  - `FetchOptions` を反射で広げる（互換性確保）
  - 取得したアンカーに対して `SetEnabledAsync(true)` を呼びローカライズを促す
  - `0.7s` 程度の短い遅延を置いて再フェッチするリトライ機構（例: 3 回まで）

## ログの見方（重要キーワードと意味）
- `SP:AP:AnchorLocalStorage: listAllAnchors listed 42 anchors`
  - デバイス上に登録されているアンカーの総数（この数は変わらず 42）。
- `SP:AP:AnchorLocalStorageQuery: applyFilters: num Slam LAD results: 17`
  - SLAM/LAD の結果として "localizable" と判断されたアンカー数。
  - この数がフェッチ結果を制限する主因。
- `I/Unity ... [QRDetector_NoFilter] [Query] Fetched N total anchors`
  - Unity スクリプトがフェッチして得たアンカーの件数（N）。
  - これが最終的にアプリで使えるアンカー数になる。
- `I/Unity ... [QRDetector_NoFilter] [Retry] Fetched X anchors; scheduling refetch attempt Y/3 in 0.7s`
  - リトライロジックが発火していることを示す。リトライ後に件数が増減する挙動を確認できる。
- `SP:AP:AnchorPersistence: Sending DiscoverSpaces found event ... numSpacesFound: 10`
  - スペース（グループ分け）の検出イベント。多数は内部処理ログで診断用。

### 例: よく見るパターン
- 1) `listAllAnchors listed 42 anchors` が出る → デバイスは完全な保存を保持している
- 2) 直後に `applyFilters: num Slam LAD results: 17` → LAD が 17 件を localizable と見なす
- 3) Unity の `Fetched` が 17 より小さい場合はさらにアプリ側フィルタ／コンポーネントで除外されているか、FetchOptions の絞り込みの影響

## 詳細解析の進め方（ログ抽出コマンド例）
※作業端末の既定シェルが PowerShell (`pwsh.exe`) の前提での例。

1) デバイスのログ全体をファイル保存（アプリ起動前に実行）:

```powershell
adb logcat -v time > C:\temp\quest_full_log.txt
```

2) 収集済みログからアンカー関連行のみ抽出（PowerShell）:

```powershell
Select-String -Path C:\temp\quest_full_log.txt -Pattern "AnchorLocalStorage|AnchorLocalStorageQuery|QRDetector_NoFilter|QRDetector_ActiveScan|QRDetector_SystemWide|SetEnabledAsync|Retry" > C:\temp\quest_anchor_lines.txt
```

3) UUID タイムラインを抽出（`spacesFoundUuids` 行）:

```powershell
Select-String -Path C:\temp\quest_full_log.txt -Pattern "spacesFoundUuids" | Out-File C:\temp\anchor_uuid_timeline.txt
```

これらで「どの時刻にどの UUID が見つかっているか」「LAD の数値がいつ 17 から増減するか」を可視化できます。

## 推奨される短期アクション（優先順）
1. `Unity` プロジェクトを現在のソースで再ビルドして Quest にデプロイ。
   - 目的: リトライ＋SetEnabledAsync の効果を安定して確認する。
2. 実行ログを上記コマンドで取得し、`applyFilters: num Slam LAD results:` の時間変化と Unity の `Fetched N` の時間変化を突き合わせる。
3. 安定化が必要な場合、段階的に試すパラメータ:
   - `_maxRefetchAttempts` を増やす（例: 3 → 5）
   - `_refetchDelay` を延長（例: 0.7s → 1.0–1.5s）
   - フェッチの順序を変える：`SetEnabledAsync(true)` を「フェッチ前に一度呼ぶ」→短待機→フェッチ、というパターンを試す

## 追加の診断アイデア（必要なら実施）
- 各アンカー UUID ごとに `localizable` 状態のログを追加で出力する（スクリプト側にデバッグログを追加）。
- どのアンカーが一貫して localizable にならないかを特定し、そのアンカーのメタデータ（作成時の環境、周囲のジオメトリ）を確認する。

## 変更されたファイル（現状のパッチ一覧）
- `AndroidManifest.xml` — Anchor 関連パーミッション追加（例: `USE_ANCHOR_API`, `ACCESS_SHARED_ANCHORS`）
- `QRDetector_ActiveScan.cs` — `TrySetAllEnumFlags`、`SetEnabledAsync(true)`、リトライ/遅延ロジック追加
- `QRDetector_SystemWide.cs` — 同上
- `QRDetector_NoFilter.cs` — 同上


