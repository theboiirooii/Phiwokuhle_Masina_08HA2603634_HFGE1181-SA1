using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float wanderDistance = 6f;
    [SerializeField] private float wanderSpeed = 1.8f;
    [SerializeField] private float idleTime = 1f;

    [Header("Navigation Settings")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private Vector2 boundsMinimum = new Vector2(-16.5f, -11.5f);
    [SerializeField] private Vector2 boundsMaximum = new Vector2(16.5f, 11.5f);
    [SerializeField] private float obstacleCheckRadius = 0.35f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float maximumTravelTime = 12f;

    [Header("Feedback Settings")]
    [SerializeField] private Animator anim;

    private UnitSizeHandler sizeHandler;
    private Vector3 lastPosition;

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        sizeHandler = GetComponent<UnitSizeHandler>();
        lastPosition = transform.position;
    }

    private void Start()
    {
        StartCoroutine(IdleAtLocation());
    }

    private void Update()
    {
        if (anim == null || Time.deltaTime <= 0f)
        {
            return;
        }

        float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
        anim.SetFloat("Speed", speed);
        lastPosition = transform.position;
    }

    public Vector3 SelectNewLocation()
    {
        Vector2 offset = Random.insideUnitCircle * wanderDistance;
        Vector2 newLocation = (Vector2)transform.position + offset;

        newLocation.x = Mathf.Clamp(newLocation.x, boundsMinimum.x, boundsMaximum.x);
        newLocation.y = Mathf.Clamp(newLocation.y, boundsMinimum.y, boundsMaximum.y);

        return new Vector3(newLocation.x, newLocation.y, 0f);
    }

    public IEnumerator IdleAtLocation()
    {
        yield return new WaitForSecondsRealtime(idleTime);
        StartCoroutine(MoveTowardsLocation());
    }

    public IEnumerator MoveTowardsLocation()
    {
        Vector3 newLocation = SelectNewLocation();
        float travelTimer = 0f;

        while (Vector2.Distance(transform.position, newLocation) > 0.05f)
        {
            yield return new WaitWhile(() => GameStateManager.Instance != null &&
                                             GameStateManager.Instance.GetGameState() != GameState.Playing);

            Vector3 nextPosition = Vector3.MoveTowards(transform.position, newLocation,
                wanderSpeed * Time.deltaTime);

            if (IsBlocked(nextPosition))
            {
                break;
            }

            transform.position = nextPosition;

            Vector2 moveDirection = newLocation - transform.position;
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                    turnSpeed * Time.deltaTime);
            }

            travelTimer += Time.deltaTime;
            if (travelTimer > maximumTravelTime)
            {
                break;
            }

            yield return null;
        }

        yield return StartCoroutine(IdleAtLocation());
    }

    private bool IsBlocked(Vector3 position)
    {
        float size = sizeHandler != null ? sizeHandler.GetSize() : 1f;
        float radius = Mathf.Max(0.2f, obstacleCheckRadius * size);
        return Physics2D.OverlapCircle(position, radius, obstacleLayers) != null;
    }
}
