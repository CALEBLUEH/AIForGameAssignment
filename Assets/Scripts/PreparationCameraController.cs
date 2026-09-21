using UnityEngine;
using UnityEngine.EventSystems;

public class PreparationCameraController : MonoBehaviour
{
    public Vector3 target = new Vector3(0f, 1.4f, 0f);
    public float distance = 14f;
    public float yaw = -16f;
    public float pitch = 14f;
    public float idleOrbitSpeed = 5f;
    public float dragSpeed = 0.18f;

    private void LateUpdate()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (Input.GetMouseButton(1) && !overUI)
        {
            yaw += Input.GetAxis("Mouse X") * dragSpeed * 20f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * dragSpeed * 20f, 7f, 32f);
        }
        else yaw += idleOrbitSpeed * Time.unscaledDeltaTime;
        if (!overUI) distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y, 10f, 18f);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }
}
