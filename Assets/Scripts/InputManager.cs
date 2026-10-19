using UnityEngine;
using System;
using UnityEngine.EventSystems;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<InputManager>() != null) return;
        new GameObject("InputManager_Runtime").AddComponent<InputManager>();
    }

    public float minSwipeDistance = 30f;

    private Vector2 _touchStart;
    private bool _swipeDetected;
    private bool _ignoreTouch;
    private bool _suppressUntilPointersReleased;
    private readonly InputIntentBuffer _intentBuffer = new InputIntentBuffer();

    public event Action<SwipeDirection, bool> SwipeResolved;
    public int PendingInputCount => _intentBuffer.Count;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            ClearInput();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui != null && ui.TryCloseRunSettings()) return;
            if (gameManager.State == GameState.Playing)
                gameManager.Pause();
            else if (gameManager.State == GameState.Paused)
                gameManager.Resume();
            return;
        }

        if (gameManager.State != GameState.Playing)
        {
            ClearInput();
            return;
        }

        // The pointer-up that activates the Start button can arrive after the
        // game enters Playing. Do not reinterpret that UI gesture as a swipe.
        if (_suppressUntilPointersReleased)
        {
            bool pointersReleased = Input.touchCount == 0
                && !Input.GetMouseButton(0)
                && !Input.GetMouseButtonUp(0);
            if (pointersReleased)
                _suppressUntilPointersReleased = false;
            return;
        }

        // Keyboard input - queue all pressed keys instead of returning early
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            QueueSwipe(SwipeDirection.Left, InputIntentSource.Keyboard,
                Time.unscaledTime);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            QueueSwipe(SwipeDirection.Right, InputIntentSource.Keyboard,
                Time.unscaledTime);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
            QueueSwipe(SwipeDirection.Up, InputIntentSource.Keyboard,
                Time.unscaledTime);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftControl))
            QueueSwipe(SwipeDirection.Down, InputIntentSource.Keyboard,
                Time.unscaledTime);

        // Touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _ignoreTouch = EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                _touchStart = touch.position;
                _swipeDetected = false;
            }
            else if (touch.phase == TouchPhase.Moved && !_swipeDetected && !_ignoreTouch)
            {
                DetectSwipe(touch.position, InputIntentSource.Touch);
            }
            else if (touch.phase == TouchPhase.Ended && !_swipeDetected && !_ignoreTouch)
            {
                DetectSwipe(touch.position, InputIntentSource.Touch);
            }
        }

        // Mouse drag is useful in WebGL and desktop builds as well as the editor.
        // Ignore emulated mouse events while a real touch is active.
        if (Input.touchCount == 0 && Input.mousePresent && Input.GetMouseButtonDown(0))
        {
            _ignoreTouch = EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
            _touchStart = Input.mousePosition;
            _swipeDetected = false;
        }
        if (Input.touchCount == 0 && Input.mousePresent
            && Input.GetMouseButtonUp(0) && !_swipeDetected && !_ignoreTouch)
        {
            DetectSwipe(Input.mousePosition, InputIntentSource.Mouse);
        }
    }

    void DetectSwipe(Vector2 endPos, InputIntentSource source)
    {
        Vector2 delta = endPos - _touchStart;
        float shortEdge = Mathf.Min(Screen.width, Screen.height);
        float threshold = ResolveSwipeThreshold(
            minSwipeDistance, shortEdge, Screen.dpi);
        SwipeDirection direction = ClassifySwipe(delta, threshold);
        if (direction == SwipeDirection.None) return;

        _swipeDetected = true;
        QueueSwipe(direction, source, Time.unscaledTime);
    }

    public void QueueSwipe(SwipeDirection direction, InputIntentSource source,
        float issuedAt)
    {
        if (direction == SwipeDirection.None) return;
        BufferedSwipeCommand command = _intentBuffer.Enqueue(
            direction, source, issuedAt, out BufferedSwipeCommand evicted);
        if (evicted.sequence != 0)
            ResolveIntent(evicted, InputIntentOutcome.Dropped, -1, issuedAt);
        AIRunTelemetry.RecordInputQueued(command);
    }

    public bool TryPeekSwipe(out BufferedSwipeCommand command)
    {
        return TryPeekSwipe(Time.unscaledTime, out command);
    }

    public bool TryPeekSwipe(float now, out BufferedSwipeCommand command)
    {
        while (_intentBuffer.TryPopExpired(now,
                   out BufferedSwipeCommand expired))
            ResolveIntent(expired, InputIntentOutcome.Expired, -1, now);
        return _intentBuffer.TryPeek(out command);
    }

    public void ResolveSwipe(BufferedSwipeCommand command,
        InputIntentOutcome outcome, int lane)
    {
        if (outcome == InputIntentOutcome.Pending) return;
        if (!_intentBuffer.TryResolveHead(command.sequence,
                out BufferedSwipeCommand resolved))
            return;
        ResolveIntent(resolved, outcome, lane, Time.unscaledTime);
    }

    public void DeferSwipe(BufferedSwipeCommand command)
    {
        _intentBuffer.TryDeferHead(command.sequence);
    }

    public SwipeDirection GetSwipe()
    {
        if (!TryPeekSwipe(out BufferedSwipeCommand command))
            return SwipeDirection.None;
        return _intentBuffer.TryResolveHead(command.sequence, out _)
            ? command.direction
            : SwipeDirection.None;
    }

    public void ReportSwipeResult(SwipeDirection direction, bool accepted)
    {
        if (direction == SwipeDirection.None) return;
        SwipeResolved?.Invoke(direction, accepted);
    }

    public static SwipeDirection ClassifySwipe(Vector2 delta, float threshold)
    {
        if (delta.magnitude < Mathf.Max(1f, threshold))
            return SwipeDirection.None;

        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        // A small vertical bias makes an intentionally upward, slightly
        // diagonal phone gesture reliably count as Jump without turning a
        // clearly horizontal lane swipe into a vertical action.
        if (absY >= absX * 0.85f)
            return delta.y >= 0f ? SwipeDirection.Up : SwipeDirection.Down;

        return delta.x >= 0f ? SwipeDirection.Right : SwipeDirection.Left;
    }

    public void ClearInput()
    {
        float now = Time.unscaledTime;
        while (_intentBuffer.TryDequeue(out BufferedSwipeCommand command))
            ResolveIntent(command, InputIntentOutcome.Dropped, -1, now);
        _swipeDetected = false;
        _ignoreTouch = false;
        _suppressUntilPointersReleased = true;
    }

    public static float ResolveSwipeThreshold(
        float configuredMinimum, float shortScreenEdge, float screenDpi)
    {
        float screenThreshold = Mathf.Max(0f, shortScreenEdge) * 0.045f;
        float physicalThreshold = screenDpi > 0f ? screenDpi * 0.14f : 0f;
        return Mathf.Max(Mathf.Max(1f, configuredMinimum),
            Mathf.Min(96f, Mathf.Max(screenThreshold, physicalThreshold)));
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) ClearInput();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResolveIntent(BufferedSwipeCommand command,
        InputIntentOutcome outcome, int lane, float resolvedAt)
    {
        AIRunTelemetry.RecordInputResolved(
            command, outcome, lane, resolvedAt);
        SwipeResolved?.Invoke(command.direction,
            outcome == InputIntentOutcome.Executed);
    }
}
