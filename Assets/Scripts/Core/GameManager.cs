using UnityEngine;

namespace OblivionsEdge.Core
{
    /// <summary>
    /// Lifecycle states for the game session.
    /// </summary>
    public enum GameState
    {
        Initializing,
        InGame,
        Paused,
        GameOver,
        Victory
    }

    /// <summary>
    /// Foundation coordinator for session state, level bootstrap, and global events.
    /// Acts as an architecture anchor for Member 1 and the development team.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameState _initialState = GameState.InGame;

        public GameState CurrentState { get; private set; } = GameState.Initializing;

        public event System.Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SetState(_initialState);
        }

        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] Game state transitioned to: {newState}");
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.InGame)
            {
                SetState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                SetState(GameState.InGame);
            }
        }
    }
}
