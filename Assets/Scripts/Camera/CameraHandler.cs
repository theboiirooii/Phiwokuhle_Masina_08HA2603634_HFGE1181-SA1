using UnityEngine;

public class CameraHandler : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private GameObject target;
    [SerializeField] private Vector3 relativePosition;
    [SerializeField] private float baseZOffset = -10f;
    [SerializeField] private float baseOrthographicSize = 4.5f;
    [SerializeField] private float zoomPerSize = 0.45f;

    private SizeHandler sizeHandler;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (target != null)
        {
            sizeHandler = target.GetComponent<SizeHandler>();
        }
    }

    private void Start()
    {
        AdjustPosition();
    }

    private void LateUpdate()
    {
        AdjustPosition();
    }

    private void AdjustPosition()
    {
        if (target == null)
        {
            return;
        }

        float size = sizeHandler != null ? sizeHandler.GetSize() : 1f;

        relativePosition = new Vector3(target.transform.position.x, target.transform.position.y,
            baseZOffset - (size - 1f));
        transform.position = relativePosition;

        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = baseOrthographicSize * (1f + Mathf.Max(0f, size - 1f) * zoomPerSize);
        }
    }
}
