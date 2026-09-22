using UnityEngine;

public sealed class PreparationCharacterDisplay : MonoBehaviour
{
    [SerializeField] private int rosterIndex;
    [SerializeField] private Transform modelPivot;
    [SerializeField] private Renderer podiumRenderer;
    [SerializeField] private Collider podiumCollider;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private Color unselectedColor = new Color(0.08f, 0.58f, 0.78f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.72f, 0.08f, 1f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock propertyBlock;
    private bool isSelected;

    public int RosterIndex => rosterIndex;
    public Transform ModelPivot => modelPivot;
    public Animator CharacterAnimator => characterAnimator;
    public bool IsSelected => isSelected;

    private void OnEnable()
    {
        PlayIdle();
    }

    public void Configure(int index, Transform pivot, Renderer renderer, Collider selectionCollider, Animator animator)
    {
        rosterIndex = index;
        modelPivot = pivot;
        podiumRenderer = renderer;
        podiumCollider = selectionCollider;
        characterAnimator = animator;
    }

    public bool OwnsPodiumCollider(Collider candidate) =>
        podiumCollider != null && candidate == podiumCollider;

    public void RotateModel(float degrees)
    {
        if (modelPivot != null) modelPivot.Rotate(0f, degrees, 0f, Space.Self);
    }

    public void PlayIdle()
    {
        if (characterAnimator == null) return;
        characterAnimator.applyRootMotion = false;
        characterAnimator.Play("Idle", 0, 0f);
    }

    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        if (podiumRenderer == null) return;
        propertyBlock ??= new MaterialPropertyBlock();
        podiumRenderer.GetPropertyBlock(propertyBlock);
        Color color = isSelected ? selectedColor : unselectedColor;
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetColor(BaseColorId, color);
        podiumRenderer.SetPropertyBlock(propertyBlock);
    }
}
