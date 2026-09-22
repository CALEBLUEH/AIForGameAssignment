using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class CharacterPreviewGrounder : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0f)] private float footToSoleDistance;
    [SerializeField] private float groundOffset;

    private Transform leftFoot;
    private Transform rightFoot;

    private void Awake()
    {
        CacheFeet();
    }

    private void OnEnable()
    {
        CacheFeet();
    }

    private void LateUpdate()
    {
        if (visualRoot == null || leftFoot == null || rightFoot == null) return;

        float lowerFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        float soleDistance = transform.TransformVector(Vector3.up * footToSoleDistance).y;
        float currentSoleY = lowerFootY - soleDistance;
        float targetSoleY = transform.TransformPoint(Vector3.up * groundOffset).y;
        visualRoot.position += Vector3.up * (targetSoleY - currentSoleY);
    }

    private void CacheFeet()
    {
        if (animator == null || !animator.isHuman) return;
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }
}
