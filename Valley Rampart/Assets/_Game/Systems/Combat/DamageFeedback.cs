using UnityEngine;

/// <summary>
/// 受击闪红反馈（3.4 P0 第 10 项）。
///
/// 订阅 UnitDamagedEvent，受击时 SpriteRenderer 闪红 0.1s。
/// 挂在 NPC Prefab 上，与 UnitController 同级（不修改 UnitController）。
/// 死亡直接消失（UnitController.Die -> Destroy），无淡出。
///
/// 详见 3.4_伤害管线设计.md 决策 15。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DamageFeedback : MonoBehaviour
{
    [Tooltip("闪红时长（秒），默认 0.1s")]
    [SerializeField] private float _flashDuration = 0.1f;

    private SpriteRenderer _renderer;
    private IDamageable _self;
    private Color _originalColor;
    private float _flashTimer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _self = GetComponent<IDamageable>();
        if (_renderer != null) _originalColor = _renderer.color;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDamagedEvent>(OnDamaged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDamagedEvent>(OnDamaged);
    }

    private void OnDamaged(UnitDamagedEvent evt)
    {
        if (evt.Unit == null || evt.Unit != _self) return;
        _flashTimer = _flashDuration;
    }

    private void Update()
    {
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_renderer != null)
            {
                float ratio = Mathf.Clamp01(_flashTimer / _flashDuration);
                _renderer.color = Color.Lerp(_originalColor, Color.red, ratio);
            }
        }
        else if (_renderer != null && _renderer.color != _originalColor)
        {
            _renderer.color = _originalColor;
        }
    }
}
