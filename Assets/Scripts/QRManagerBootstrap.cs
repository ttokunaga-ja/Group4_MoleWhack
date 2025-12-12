using UnityEngine;

/// <summary>
/// QRManager がシーンに存在しない場合に自動生成するブートストラップ。
/// シーンに手動で置くのがベストだが、配置漏れ対策として追加。
/// </summary>
public static class QRManagerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureQRManagerExists()
    {
        if (QRManager.Instance != null) return;

        var go = new GameObject("QRManager (Auto)");
        go.AddComponent<QRManager>();
        Object.DontDestroyOnLoad(go);
    }
}
