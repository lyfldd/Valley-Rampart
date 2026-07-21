using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 右上角资源面板：显示国家四种资源（Gold/Stone/Wood/Food）。
///
/// 刷新策略：
///   - 资源数值：事件驱动（RulerResourceChangedEvent）
///   - 初始显示：Start 时主动从 RulerController 读一次当前值
///
/// 挂载位置：Canvas/ResourceHUD。
/// Inspector 拖入对应 Text 即可，未拖入的字段不显示。
/// </summary>
public class ResourceHUD : MonoBehaviour
{
    [Header("资源显示")]
    [Tooltip("金币文字。")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Tooltip("石料文字。")]
    [SerializeField] private TextMeshProUGUI stoneText;

    [Tooltip("木材文字。")]
    [SerializeField] private TextMeshProUGUI woodText;

    [Tooltip("粮食文字。")]
    [SerializeField] private TextMeshProUGUI foodText;

    private void Awake()
    {
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    private void Start()
    {
        RefreshAll();
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    // ===== 事件驱动刷新 =====

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        UpdateResourceText(evt.Type, evt.NewValue);
    }

    // ===== 全量刷新（开局主动读一次）=====

    private void RefreshAll()
    {
        var ruler = RulerController.Instance;
        if (ruler == null) return;

        UpdateResourceText(ResourceType.Gold, ruler.Gold);
        UpdateResourceText(ResourceType.Stone, ruler.Stone);
        UpdateResourceText(ResourceType.Wood, ruler.Wood);
        UpdateResourceText(ResourceType.Food, ruler.Food);
    }

    // ===== 单项更新 =====

    private void UpdateResourceText(ResourceType type, int value)
    {
        var target = type switch
        {
            ResourceType.Gold => goldText,
            ResourceType.Stone => stoneText,
            ResourceType.Wood => woodText,
            ResourceType.Food => foodText,
            _ => null
        };

        if (target != null)
            target.text = FormatValue(value);
    }

    /// <summary>
    /// 格式化资源数值，大数字用 K/M 后缀。
    /// </summary>
    private static string FormatValue(int value)
    {
        if (value >= 1000000)
            return $"{value / 1000000f:F1}M";
        if (value >= 1000)
            return $"{value / 1000f:F1}K";
        return value.ToString();
    }
}
