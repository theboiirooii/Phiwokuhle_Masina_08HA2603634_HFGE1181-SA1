using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum StickType
    {
        Move,
        Aim
    }

    public static Vector2 MoveInput;
    public static Vector2 AimInput;

    [SerializeField] private StickType stickType = StickType.Move;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 70f;
    [SerializeField] private float deadZone = 0.15f;

    private RectTransform baseRect;
    private Canvas canvas;
    private int activePointerId = -1;
    private Vector2 value;

    private void Awake()
    {
        baseRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        SetValue(Vector2.zero);
    }

    private void OnDisable()
    {
        activePointerId = -1;
        SetValue(Vector2.zero);

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (activePointerId == -1)
        {
            activePointerId = eventData.pointerId;
        }
        else if (activePointerId != eventData.pointerId)
        {
            return;
        }

        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position,
                eventCamera, out Vector2 localPoint))
        {
            float radius = baseRect.sizeDelta.x * 0.5f;
            value = Vector2.ClampMagnitude(localPoint / radius, 1f);

            if (value.magnitude < deadZone)
            {
                value = Vector2.zero;
            }

            if (handle != null)
            {
                handle.anchoredPosition = value * handleRange;
            }

            SetValue(value);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
        {
            return;
        }

        activePointerId = -1;
        value = Vector2.zero;
        SetValue(Vector2.zero);

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }

    private void SetValue(Vector2 newValue)
    {
        value = newValue;

        if (stickType == StickType.Move)
        {
            MoveInput = newValue;
        }
        else
        {
            AimInput = newValue;
        }
    }
}
