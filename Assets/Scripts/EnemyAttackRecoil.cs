using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AutoCombatAI))]
public sealed class EnemyAttackRecoil : MonoBehaviour
{
    [SerializeField] private AutoCombatAI combatAI;
    [SerializeField] private Transform visualRoot;
    [Min(0f)] [SerializeField] private float recoilDistance = 0.16f;
    [Min(0f)] [SerializeField] private float recoilAngle = 5f;
    [Min(0.05f)] [SerializeField] private float recoilDuration = 0.18f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private float elapsed;
    private bool recoiling;

    public Transform VisualRoot => visualRoot;

    private void Awake()
    {
        if (combatAI == null) combatAI = GetComponent<AutoCombatAI>();
        if (visualRoot == null && transform.childCount > 0) visualRoot = transform.GetChild(0);
        CaptureRestPose();
    }

    private void OnEnable()
    {
        if (combatAI == null) combatAI = GetComponent<AutoCombatAI>();
        if (combatAI != null)
        {
            combatAI.AttackPerformed -= PlayRecoil;
            combatAI.AttackPerformed += PlayRecoil;
        }
    }

    private void OnDisable()
    {
        if (combatAI != null) combatAI.AttackPerformed -= PlayRecoil;
        ResetPose();
    }

    private void LateUpdate()
    {
        if (!recoiling || visualRoot == null) return;
        elapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, recoilDuration));
        float kick = Mathf.Sin(normalized * Mathf.PI);
        visualRoot.localPosition = restPosition + Vector3.back * (recoilDistance * kick);
        visualRoot.localRotation = restRotation * Quaternion.Euler(-recoilAngle * kick, 0f, 0f);
        if (normalized >= 1f)
        {
            recoiling = false;
            ResetPose();
        }
    }

    public void Configure(AutoCombatAI ai, Transform modelRoot)
    {
        if (combatAI != null) combatAI.AttackPerformed -= PlayRecoil;
        combatAI = ai;
        visualRoot = modelRoot;
        CaptureRestPose();
        if (isActiveAndEnabled && combatAI != null) combatAI.AttackPerformed += PlayRecoil;
    }

    public void PlayRecoil()
    {
        if (visualRoot == null) return;
        elapsed = 0f;
        recoiling = true;
    }

    private void CaptureRestPose()
    {
        if (visualRoot == null) return;
        restPosition = visualRoot.localPosition;
        restRotation = visualRoot.localRotation;
    }

    private void ResetPose()
    {
        recoiling = false;
        if (visualRoot == null) return;
        visualRoot.localPosition = restPosition;
        visualRoot.localRotation = restRotation;
    }
}
