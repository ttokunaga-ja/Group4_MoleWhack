using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single-scene fallback用の簡易モグラ制御:
/// - PoseLocker で2体以上ロック済みなら、まず穴のみ表示で waitSeconds を待つ
/// - 待機後に敵スポーンを有効化し、上下バウンドさせる
/// </summary>
public class Gameplay_MoleWaveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRObjectPositioner positioner;
    [SerializeField] private GameSessionManager sessionManager;
    [SerializeField] private Gameplay_HitPipeline hitPipeline;

    [Header("Timing")]
    [SerializeField] private float preGameDelaySeconds = 10f; // 穴だけ表示する時間
    [SerializeField] private float bounceAmplitude = 0.1f;
    [SerializeField] private float bounceSpeed = 1.5f;

    private bool routineStarted = false;
    private struct BasePose
    {
        public Vector3 position;
        public Vector3 up;
    }

    private Dictionary<Transform, BasePose> basePoses = new Dictionary<Transform, BasePose>();
    private Dictionary<string, Gameplay_EnemyLifecycle> lifecycleByUuid = new Dictionary<string, Gameplay_EnemyLifecycle>();

    private void Start()
    {
        poseLocker = poseLocker ?? FindObjectOfType<QRPoseLocker>();
        positioner = positioner ?? FindObjectOfType<QRObjectPositioner>();
        sessionManager = sessionManager ?? GameSessionManager.Instance ?? FindObjectOfType<GameSessionManager>();
        hitPipeline = hitPipeline ?? FindObjectOfType<Gameplay_HitPipeline>();

        if (hitPipeline != null)
        {
            hitPipeline.OnHitSuccess.AddListener(HandleHit);
        }
    }

    private void Update()
    {
        if (!routineStarted && poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Locked && poseLocker.LockedPoseCount >= 2)
        {
            routineStarted = true;
            StartCoroutine(PreGameRoutine());
        }

        if (basePoses.Count > 0)
        {
            float t = Time.time * bounceSpeed;
            foreach (var kvp in basePoses)
            {
                Transform tr = kvp.Key;
                if (tr == null) continue;
                float offset = Mathf.Sin(t) * bounceAmplitude;
                var pose = kvp.Value;
                tr.position = pose.position + pose.up * offset;
            }
        }
    }

    private IEnumerator PreGameRoutine()
    {
        if (positioner != null)
        {
            positioner.SetEnemySpawnEnabled(false);
            positioner.ClearAllEnemies(); // 穴のみ表示
        }

        yield return new WaitForSeconds(preGameDelaySeconds);

        if (positioner != null)
        {
            positioner.SetEnemySpawnEnabled(true);
            positioner.ForceSpawnMissingEnemies();
            CacheBasePositions();
            CacheLifecycles();
        }

        if (sessionManager != null && sessionManager.State == GameSessionManager.SessionState.Idle)
        {
            sessionManager.BeginSession();
        }
    }

    private void CacheLifecycles()
    {
        lifecycleByUuid.Clear();
        if (positioner == null) return;
        foreach (var kvp in positioner.GetUuidEnemyMap())
        {
            string uuid = kvp.Key;
            var enemyGo = kvp.Value;
            if (enemyGo == null) continue;
            var lc = enemyGo.GetComponent<Gameplay_EnemyLifecycle>();
            if (lc == null)
            {
                lc = enemyGo.AddComponent<Gameplay_EnemyLifecycle>();
            }
            lifecycleByUuid[uuid] = lc;
        }
    }

    private void CacheBasePositions()
    {
        basePoses.Clear();
        if (positioner == null) return;
        List<Transform> enemies = positioner.GetActiveEnemies();
        foreach (var tr in enemies)
        {
            if (tr != null && !basePoses.ContainsKey(tr))
            {
                basePoses[tr] = new BasePose
                {
                    position = tr.position,
                    up = tr.up.normalized
                };
            }
        }
    }

    private void HandleHit(string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return;
        if (lifecycleByUuid.TryGetValue(uuid, out var lc) && lc != null)
        {
            lc.HandleHit();
        }
    }
}
