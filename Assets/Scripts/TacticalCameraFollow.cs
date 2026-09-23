using UnityEngine;

public class TacticalCameraFollow : MonoBehaviour
{
    public Vector3 offset = new Vector3(-8f, 25f, -20f);
    public Vector3 lookAhead = new Vector3(7f, 0f, 0f);
    public float smoothTime = 0.35f;
    public Vector2 xLimits = new Vector2(-8f, 58f);
    private Vector3 velocity;

    public float VerticalOffset => offset.y;
    public void SetVerticalOffset(float value) => offset.y = Mathf.Clamp(value, 20f, 40f);

    private void LateUpdate()
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (CombatUnit unit in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (unit.team != CombatUnit.CombatTeam.Player || unit.IsDead) continue;
            center += unit.transform.position;
            count++;
        }
        if (count == 0) return;
        center /= count;
        center.x = Mathf.Clamp(center.x, xLimits.x, xLimits.y);
        Vector3 target = center + offset;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);
        transform.rotation = Quaternion.LookRotation(center + lookAhead - transform.position, Vector3.up);
    }
}
