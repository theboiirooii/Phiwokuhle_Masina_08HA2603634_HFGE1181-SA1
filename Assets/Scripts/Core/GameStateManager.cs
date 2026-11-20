using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Playing,
    Paused,
    GameOver
}

public class GameStateManager : MonoBehaviour
{
    public PlayerInputActions InputActions;
    public static GameStateManager Instance;

    [SerializeField] private GameState gameState = GameState.Playing;

    private void Awake()
    {
        InputActions = new PlayerInputActions();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameState = GameState.Playing;
        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        InputActions.UI.Enable();
        InputActions.UI.Pause.performed += OnPause;
    }

    private void OnDisable()
    {
        InputActions.UI.Pause.performed -= OnPause;
        InputActions.UI.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetGameState(GameState newState)
    {
        gameState = newState;
        Time.timeScale = newState == GameState.Playing ? 1f : 0f;
    }

    public GameState GetGameState()
    {
        return gameState;
    }

    public void TogglePause()
    {
        if (gameState == GameState.Playing)
        {
            SetGameState(GameState.Paused);
        }
        else if (gameState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
        }
    }

    private void OnPause(InputAction.CallbackContext obj)
    {
        TogglePause();
    }
}
