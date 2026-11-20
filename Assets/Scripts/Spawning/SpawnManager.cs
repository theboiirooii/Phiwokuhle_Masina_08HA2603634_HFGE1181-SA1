using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    [Header("General Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private UnitSizeHandler playerSizeHandler;
    [SerializeField] private Vector2 spawnOffsetMinimum = new Vector2(-16.5f, -11.5f);
    [SerializeField] private Vector2 spawnOffsetMaximum = new Vector2(16.5f, 11.5f);
    [SerializeField] private float enemySpawnTime = 4f;
    [SerializeField] private float foodSpawnTime = 2f;
    [SerializeField] private LayerMask overlapLayers;
    [SerializeField] private int initialEnemyCount = 3;
    [SerializeField] private int initialFoodCount = 6;
    [SerializeField] private int maximumSpawnAttempts = 30;

    private float enemyTimer;
    private float foodTimer;

    [Header("Enemy Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float enemyMaximumSize = 1.5f;
    [SerializeField] private float enemyMinimumSize = 0.45f;
    [SerializeField] private float enemyDespawnSize = 0.25f;
    [SerializeField] private GameObject targetIndicatorPrefab;
    [SerializeField] private Transform targetIndicatorParent;

    [Header("Food Spawn Settings")]
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private float foodMinimumSize = 0.2f;
    [SerializeField] private float foodMaximumSize = 0.6f;
    [SerializeField] private float foodDespawnSize = 0.15f;

    [Header("Indexing")]
    [SerializeField] private List<UnitSizeHandler> enemySizeHandlers = new List<UnitSizeHandler>();
    [SerializeField] private List<SizeHandler> foodSizeHandlers = new List<SizeHandler>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        enemyTimer = enemySpawnTime;
        foodTimer = foodSpawnTime;

        for (int i = 0; i < initialFoodCount; i++)
        {
            SpawnFood();
        }

        for (int i = 0; i < initialEnemyCount; i++)
        {
            SpawnEnemy();
        }
    }

    private void Update()
    {
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.GetGameState() != GameState.Playing)
        {
            return;
        }

        enemyTimer -= Time.deltaTime;
        if (enemyTimer <= 0f)
        {
            enemyTimer = enemySpawnTime;
            SpawnEnemy();
        }

        foodTimer -= Time.deltaTime;
        if (foodTimer <= 0f)
        {
            foodTimer = foodSpawnTime;
            SpawnFood();
        }

        CheckEnemySize();
        CheckFoodSize();
    }

    public void CleanEnemyArray(UnitSizeHandler e)
    {
        enemySizeHandlers.Remove(e);
    }

    public void CheckEnemySize()
    {
        if (playerSizeHandler == null)
        {
            return;
        }

        float minimumSize = playerSizeHandler.GetSize() * enemyDespawnSize;

        for (int i = enemySizeHandlers.Count - 1; i >= 0; i--)
        {
            UnitSizeHandler enemy = enemySizeHandlers[i];

            if (enemy == null)
            {
                enemySizeHandlers.RemoveAt(i);
                continue;
            }

            if (enemy.GetSize() < minimumSize)
            {
                DestroyEnemy(enemy);
            }
        }
    }

    public void SpawnEnemy()
    {
        if (enemyPrefab == null || playerSizeHandler == null)
        {
            return;
        }

        float playerSize = playerSizeHandler.GetSize();
        float randomSpawnSize = Random.Range(playerSize * enemyMinimumSize,
            playerSize * enemyMaximumSize);

        if (!TryGetFreePosition(randomSpawnSize, out Vector2 randomPosition))
        {
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, randomPosition, Quaternion.identity);
        enemy.GetComponent<SizeHandler>().SetSize(randomSpawnSize);
        enemySizeHandlers.Add(enemy.GetComponent<UnitSizeHandler>());

        SpawnTargetIndicator(enemy.transform);
    }

    public void SpawnTargetIndicator(Transform target)
    {
        if (targetIndicatorPrefab == null || targetIndicatorParent == null)
        {
            return;
        }

        GameObject targetIndicator = Instantiate(targetIndicatorPrefab,
            targetIndicatorParent.position, Quaternion.identity);
        targetIndicator.transform.SetParent(targetIndicatorParent, true);
        targetIndicator.GetComponent<TargetIndicator>().SetTarget(target);
    }

    public void DestroyEnemy(UnitSizeHandler enemyToBeDestroyed)
    {
        enemySizeHandlers.Remove(enemyToBeDestroyed);
        Destroy(enemyToBeDestroyed.gameObject, 1f);
    }

    public void CleanFoodArray(SizeHandler f)
    {
        foodSizeHandlers.Remove(f);
    }

    public void CheckFoodSize()
    {
        if (playerSizeHandler == null)
        {
            return;
        }

        float minimumSize = playerSizeHandler.GetSize() * foodDespawnSize;

        for (int i = foodSizeHandlers.Count - 1; i >= 0; i--)
        {
            SizeHandler food = foodSizeHandlers[i];

            if (food == null)
            {
                foodSizeHandlers.RemoveAt(i);
                continue;
            }

            if (food.GetSize() < minimumSize)
            {
                DestroyFood(food);
            }
        }
    }

    public void SpawnFood()
    {
        if (foodPrefab == null || playerSizeHandler == null)
        {
            return;
        }

        float playerSize = playerSizeHandler.GetSize();
        float randomSpawnSize = Random.Range(playerSize * foodMinimumSize,
            playerSize * foodMaximumSize);

        if (!TryGetFreePosition(randomSpawnSize, out Vector2 randomPosition))
        {
            return;
        }

        GameObject food = Instantiate(foodPrefab, randomPosition, Quaternion.identity);
        food.GetComponent<SizeHandler>().SetSize(randomSpawnSize);
        foodSizeHandlers.Add(food.GetComponent<SizeHandler>());
    }

    public void DestroyFood(SizeHandler foodToBeDestroyed)
    {
        foodSizeHandlers.Remove(foodToBeDestroyed);
        Destroy(foodToBeDestroyed.gameObject, 1f);
    }

    private bool TryGetFreePosition(float size, out Vector2 position)
    {
        for (int attempt = 0; attempt < maximumSpawnAttempts; attempt++)
        {
            position = new Vector2(
                Random.Range(spawnOffsetMinimum.x, spawnOffsetMaximum.x),
                Random.Range(spawnOffsetMinimum.y, spawnOffsetMaximum.y));

            if (!CheckIfOccupied(position, size))
            {
                return true;
            }
        }

        position = Vector2.zero;
        return false;
    }

    private bool CheckIfOccupied(Vector2 randomPosition, float size)
    {
        float radius = Mathf.Max(0.35f, size * 0.5f);
        Collider2D hit = Physics2D.OverlapCircle(randomPosition, radius, overlapLayers);
        return hit != null;
    }
}
