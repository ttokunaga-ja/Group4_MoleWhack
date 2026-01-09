using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary helper to run Setup flow inside Gameplay scene only.
/// - Starts QRPoseLocker/QRTrustMonitor collection
/// - Waits for pose lock + trust OK, then starts gameplay session
/// - Intended for fallback when scene transitions fail; remove after normal flow is restored.
/// </summary>
public class Gameplay_SingleSceneSetupBootstrap : MonoBehaviour
{
    [Header("References (optional)")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRTrustMonitor trustMonitor;
    [SerializeField] private QRObjectPositioner positioner;
    [SerializeField] private GameSessionManager sessionManager;

    [Header("Settings")]
    [SerializeField] private string setupSceneName = "Setup";
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private bool onlyWhenSetupSceneNotLoaded = true;
    [SerializeField] private bool autoRetryOnFail = true;
    [SerializeField] private float retryDelaySeconds = 2f;
    [SerializeField] private bool keepSessionAlive = true; // disable auto-end for single-scene fallback

    [Header("Mole Gameplay")]
    [SerializeField] private GameObject normalMolePrefab;
    [SerializeField] private GameObject goldMolePrefab;
    [SerializeField] private Transform[] moleSpawnHoles;
    [SerializeField] private float moleGameTimeSeconds = 100f;
    [SerializeField] private float goldMoleSwitchTimeSeconds = 45f;
    [SerializeField, Range(0f, 1f)] private float goldMoleChance = 0.5f;
    [SerializeField] private float moleMinSpawnDelaySeconds = 1f;
    [SerializeField] private float moleMaxSpawnDelaySeconds = 3f;
    [SerializeField] private float moleLifetimeSeconds = 3f;

    private readonly List<Transform> availableMoleHoles = new List<Transform>();
    private readonly HashSet<Transform> occupiedMoleHoles = new HashSet<Transform>();
    private readonly HashSet<SingleSceneMoleRuntime> activeMoles = new HashSet<SingleSceneMoleRuntime>();
    private Coroutine moleSpawnRoutine;
    private Coroutine moleTimerRoutine;
    private float remainingMoleTime;
    private bool moleLoopActive;

    private bool gameplayStarted = false;

    private void Awake()
    {
        poseLocker = poseLocker ?? FindObjectOfType<QRPoseLocker>();
        trustMonitor = trustMonitor ?? FindObjectOfType<QRTrustMonitor>();
        positioner = positioner ?? FindObjectOfType<QRObjectPositioner>();
        sessionManager = sessionManager ?? GameSessionManager.Instance ?? FindObjectOfType<GameSessionManager>();

        if (sessionManager != null && keepSessionAlive)
        {
            var disableAutoEndField = typeof(GameSessionManager).GetField("disableAutoEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (disableAutoEndField != null)
            {
                disableAutoEndField.SetValue(sessionManager, true);
                Debug.Log("[SingleSceneSetupBootstrap] disableAutoEnd=true for single-scene mode");
            }
        }

        if (onlyWhenSetupSceneNotLoaded)
        {
            bool setupLoaded = SceneManager.GetSceneByName(setupSceneName).isLoaded;
            bool isGameplay = SceneManager.GetActiveScene().name == gameplaySceneName;
            if (!isGameplay || setupLoaded)
            {
                Debug.Log("[SingleSceneSetupBootstrap] Disabled (setup scene loaded or active scene not gameplay)");
                enabled = false;
                return;
            }
        }

        if (poseLocker != null)
        {
            poseLocker.AutoStartOnEnable = false;
        }
        if (positioner != null)
        {
            // ensure locked pose only to avoid drift during gameplay
            var positionerUseLocked = positioner.GetType().GetField("useLockedPoseOnly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionerUseLocked != null)
            {
                positionerUseLocked.SetValue(positioner, true);
            }
        }
    }

    private void OnEnable()
    {
        if (poseLocker != null)
        {
            poseLocker.OnPoseLocked += HandlePoseLocked;
            poseLocker.OnLockFailed += HandleLockFailed;
            poseLocker.BeginCollect();
            Debug.Log("[SingleSceneSetupBootstrap] PoseLocker BeginCollect");
        }
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged += HandleTrustChanged;
            trustMonitor.BeginSetup();
            Debug.Log("[SingleSceneSetupBootstrap] TrustMonitor BeginSetup");
        }
    }

    private void OnDisable()
    {
        if (poseLocker != null)
        {
            poseLocker.OnPoseLocked -= HandlePoseLocked;
            poseLocker.OnLockFailed -= HandleLockFailed;
        }
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged -= HandleTrustChanged;
        }

        StopMoleLoop();
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
        TryStartGameplay();
    }

    private void HandleLockFailed()
    {
        if (autoRetryOnFail && poseLocker != null)
        {
            Invoke(nameof(RestartCollect), retryDelaySeconds);
            Debug.Log("[SingleSceneSetupBootstrap] Lock failed -> retry scheduled");
        }
    }

    private void HandleTrustChanged(float value)
    {
        TryStartGameplay();
    }

    private void RestartCollect()
    {
        if (trustMonitor != null) trustMonitor.BeginSetup();
        poseLocker?.Retry();
        Debug.Log("[SingleSceneSetupBootstrap] Restart collect");
    }

    private void TryStartGameplay()
    {
        if (gameplayStarted) return;
        bool locked = poseLocker == null || poseLocker.State == QRPoseLocker.LockerState.Locked;
        bool trustOK = trustMonitor == null || trustMonitor.CurrentTrust >= trustMonitor.TrustLowThreshold;

        if (locked && trustOK)
        {
            trustMonitor?.BeginGameplay();
            sessionManager = sessionManager ?? GameSessionManager.Instance ?? FindObjectOfType<GameSessionManager>();
            sessionManager?.BeginSession();
            StartMoleLoop();
            gameplayStarted = true;
        }
    }

    private void StartMoleLoop()
    {
        if (moleLoopActive)
        {
            return;
        }

        if ((normalMolePrefab == null && goldMolePrefab == null) || moleSpawnHoles == null || moleSpawnHoles.Length == 0)
        {
            Debug.LogWarning("[SingleSceneSetupBootstrap] Mole loop skipped: missing prefabs or spawn holes");
            return;
        }

        remainingMoleTime = moleGameTimeSeconds > 0f ? moleGameTimeSeconds : float.PositiveInfinity;
        moleLoopActive = true;

        InitializeMoleHoles();

        moleSpawnRoutine = StartCoroutine(SpawnMoles());
        if (moleGameTimeSeconds > 0f)
        {
            moleTimerRoutine = StartCoroutine(MoleTimerCountdown());
        }
    }

    private void StopMoleLoop()
    {
        if (!moleLoopActive && moleSpawnRoutine == null && moleTimerRoutine == null)
        {
            return;
        }

        moleLoopActive = false;

        if (moleSpawnRoutine != null)
        {
            StopCoroutine(moleSpawnRoutine);
            moleSpawnRoutine = null;
        }

        if (moleTimerRoutine != null)
        {
            var timerRoutine = moleTimerRoutine;
            moleTimerRoutine = null;
            StopCoroutine(timerRoutine);
        }
        else
        {
            moleTimerRoutine = null;
        }

        if (activeMoles.Count > 0)
        {
            var snapshot = new List<SingleSceneMoleRuntime>(activeMoles);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var runtime = snapshot[i];
                if (runtime != null)
                {
                    Destroy(runtime.gameObject);
                }
            }

            activeMoles.Clear();
        }

        availableMoleHoles.Clear();
        occupiedMoleHoles.Clear();
    }

    private void InitializeMoleHoles()
    {
        availableMoleHoles.Clear();
        occupiedMoleHoles.Clear();

        if (moleSpawnHoles == null)
        {
            return;
        }

        for (int i = 0; i < moleSpawnHoles.Length; i++)
        {
            var hole = moleSpawnHoles[i];
            if (hole == null || availableMoleHoles.Contains(hole))
            {
                continue;
            }

            availableMoleHoles.Add(hole);
        }
    }

    private IEnumerator SpawnMoles()
    {
        // 最初の10秒は待機（穴だけ表示）
        yield return new WaitForSeconds(10f);

        while (moleLoopActive)
        {
            if (availableMoleHoles.Count > 0)
            {
                int maxSpawnable = Mathf.Max(1, availableMoleHoles.Count);
                int molesToSpawn = Random.Range(1, maxSpawnable + 1);

                for (int i = 0; i < molesToSpawn && moleLoopActive; i++)
                {
                    if (availableMoleHoles.Count == 0)
                    {
                        break;
                    }

                    int holeIndex = Random.Range(0, availableMoleHoles.Count);
                    Transform hole = availableMoleHoles[holeIndex];
                    GameObject prefab = SelectMolePrefab();

                    if (prefab == null)
                    {
                        Debug.LogWarning("[SingleSceneSetupBootstrap] Mole spawn skipped: no prefab available");
                        moleLoopActive = false;
                        break;
                    }

                    GameObject spawned = Instantiate(prefab, hole.position, hole.rotation);
                    var runtime = spawned.GetComponent<SingleSceneMoleRuntime>() ?? spawned.AddComponent<SingleSceneMoleRuntime>();
                    activeMoles.Add(runtime);
                    runtime.Initialize(this, hole, moleLifetimeSeconds);

                    availableMoleHoles.RemoveAt(holeIndex);
                    occupiedMoleHoles.Add(hole);

                    float minDelay = Mathf.Min(moleMinSpawnDelaySeconds, moleMaxSpawnDelaySeconds);
                    float maxDelay = Mathf.Max(moleMinSpawnDelaySeconds, moleMaxSpawnDelaySeconds);
                    float wait = Mathf.Clamp(Random.Range(minDelay, maxDelay), 0f, float.MaxValue);

                    if (wait > 0f)
                    {
                        yield return new WaitForSeconds(wait);
                    }
                }
            }

            yield return null;
        }

        moleSpawnRoutine = null;
    }

    private IEnumerator MoleTimerCountdown()
    {
        while (moleLoopActive)
        {
            remainingMoleTime -= Time.deltaTime;
            if (remainingMoleTime <= 0f)
            {
                // 100秒ごとにリセット
                remainingMoleTime = 100f;
                // すべてのモグラを消去して穴をリセット
                var snapshot = new List<SingleSceneMoleRuntime>(activeMoles);
                foreach (var mole in snapshot)
                {
                    if (mole != null)
                    {
                        Destroy(mole.gameObject);
                    }
                }
                activeMoles.Clear();
                availableMoleHoles.Clear();
                occupiedMoleHoles.Clear();
                InitializeMoleHoles();
            }
            yield return null;
        }

        moleTimerRoutine = null;
    }

    internal void HandleMoleReleased(Transform hole, SingleSceneMoleRuntime runtime)
    {
        if (hole == null)
        {
            return;
        }

        if (runtime != null)
        {
            activeMoles.Remove(runtime);
        }

        occupiedMoleHoles.Remove(hole);

        if (!availableMoleHoles.Contains(hole))
        {
            availableMoleHoles.Add(hole);
        }
    }

    private GameObject SelectMolePrefab()
    {
        bool goldAvailable = goldMolePrefab != null;
        bool normalAvailable = normalMolePrefab != null;

        if (!goldAvailable && !normalAvailable)
        {
            return null;
        }

        if (goldAvailable && remainingMoleTime <= goldMoleSwitchTimeSeconds && Random.Range(0f, 1f) < Mathf.Clamp01(goldMoleChance))
        {
            return goldMolePrefab;
        }

        if (normalAvailable)
        {
            return normalMolePrefab;
        }

        return goldMolePrefab;
    }
}

internal sealed class SingleSceneMoleRuntime : MonoBehaviour
{
    private Gameplay_SingleSceneSetupBootstrap owner;
    private Transform hole;
    private Coroutine autoDespawnRoutine;
    private bool released;

    public void Initialize(Gameplay_SingleSceneSetupBootstrap owner, Transform hole, float lifetimeSeconds)
    {
        this.owner = owner;
        this.hole = hole;

        if (hole != null)
        {
            SendMessage("SetHole", hole, SendMessageOptions.DontRequireReceiver);
        }

        if (autoDespawnRoutine != null)
        {
            StopCoroutine(autoDespawnRoutine);
        }

        if (lifetimeSeconds > 0f)
        {
            autoDespawnRoutine = StartCoroutine(AutoDespawn(lifetimeSeconds));
        }
    }

    private IEnumerator AutoDespawn(float lifetimeSeconds)
    {
        yield return new WaitForSeconds(lifetimeSeconds);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        ReleaseHole();
    }

    private void OnDestroy()
    {
        ReleaseHole();
    }

    private void ReleaseHole()
    {
        if (released)
        {
            return;
        }

        released = true;
        owner?.HandleMoleReleased(hole, this);
        owner = null;
        hole = null;

        if (autoDespawnRoutine != null)
        {
            StopCoroutine(autoDespawnRoutine);
            autoDespawnRoutine = null;
        }
    }

    // プレイヤーがモグラを叩いた場合に呼ばれる
    public void Hit()
    {
        // モグラを消去
        Destroy(gameObject);

        // 穴はOnDestroyで自動的に解放される
    }
}
