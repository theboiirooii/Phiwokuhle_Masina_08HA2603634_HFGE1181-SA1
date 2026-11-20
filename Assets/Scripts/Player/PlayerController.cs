using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerInputActions InputActions;

    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private Rigidbody2D rb;
    private float movementMultiplier = 1f;
    private Vector2 moveDirection;
    private Vector2 rotationDirection;

    [Header("Collision Settings")]
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Feedback")]
    [SerializeField] private Animator anim;

    private SizeHandler sizeHandler;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
        sizeHandler = GetComponent<SizeHandler>();
        InputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        InputActions.Player.Enable();

        InputActions.Player.Move.performed += OnMovePerformed;
        InputActions.Player.Move.canceled += OnMoveCanceled;

        InputActions.Player.Look.performed += OnLookPerformed;
        InputActions.Player.Look.canceled += OnLookCanceled;
    }

    private void OnDisable()
    {
        InputActions.Player.Move.performed -= OnMovePerformed;
        InputActions.Player.Move.canceled -= OnMoveCanceled;

        InputActions.Player.Look.performed -= OnLookPerformed;
        InputActions.Player.Look.canceled -= OnLookCanceled;

        InputActions.Player.Disable();
    }

    private void Update()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.GetGameState() != GameState.Playing)
        {
            rb.linearVelocity = Vector2.zero;
            if (anim != null)
            {
                anim.SetFloat("Speed", 0f);
            }
            return;
        }

        MovementHandler();
        RotationHandler();
        UpdateAnimation();
    }

    public void MovementHandler()
    {
        Vector2 input = VirtualJoystick.MoveInput;

        if (input.sqrMagnitude < 0.0025f)
        {
            input = moveDirection;
        }

        movementMultiplier = sizeHandler != null
            ? 1f / Mathf.Sqrt(Mathf.Max(1f, sizeHandler.GetSize()))
            : 1f;

        rb.linearVelocity = Vector2.ClampMagnitude(input, 1f) * (movementSpeed * movementMultiplier);
    }

    public void RotationHandler()
    {
        Vector2 aimInput = VirtualJoystick.AimInput;
        if (aimInput.sqrMagnitude > 0.04f)
        {
            FaceDirection(aimInput);
            return;
        }

        if (!Application.isMobilePlatform && Mouse.current != null && Camera.main != null)
        {
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

            Vector3 direction = mouseWorldPosition - transform.position;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.up = direction;
                return;
            }
        }

        Vector2 facing = moveDirection.sqrMagnitude > 0.01f ? moveDirection : VirtualJoystick.MoveInput;
        if (facing.sqrMagnitude > 0.01f)
        {
            FaceDirection(facing);
        }
    }

    private void FaceDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (anim != null)
        {
            anim.SetFloat("Speed", rb.linearVelocity.magnitude);
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveDirection = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveDirection = Vector2.zero;
    }

    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        rotationDirection = context.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        rotationDirection = Vector2.zero;
    }
}
