using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 通用 toast（轻提示）单例 + 守卫警报角标（策划 Q3 MVP-A / 2_13 checklist 11）。
///
/// 职责一（复用基建）：
///   - 提供 ToastManager.Instance.Show(string message, float duration) API，
///     消息一次性顶部居中显示，duration 秒后自动淡出移除。
///   - 渲染机制：运行时动态创建 UIDocument + PanelSettings + rootVisualElement + Label，
///     不手工改动任何 scene/prefab 资产（保持 git 干净）。
///
/// 职责二（守卫警报角标）：
///   - 订阅 GuardRegionLostEvent（GuardDeploymentSystem.RemoveGuardRegion 发布），
///     收到后弹「守卫告警：X 区受袭」，X 取 Building.def.displayName → def.id → 世界坐标兜底。
///   - 来袭方向字段暂无（后做），不现编方向。
///
/// 生命周期：继承 Singleton&lt;T&gt;，首次访问 Instance 时自动创建不可销毁的 GameObject；
/// RuntimeInitializeOnLoadMethod 兜底确保进入任何场景即就绪并完成订阅。
/// </summary>
public class ToastManager : Singleton<ToastManager>
{
    /// <summary>toast 默认停留时长（秒）。</summary>
    private const float DefaultDuration = 3f;
    /// <summary>默认淡出时长（秒）。</summary>
    private const float DefaultFade = 0.6f;
    /// <summary>toast 顶层排序（对齐现有 PanelSettings 的 SortingOrder=32000，确保盖在最上）。</summary>
    private const int SortingOrder = 32000;

    private UIDocument _document;
    private PanelSettings _panelSettings;
    private VisualElement _toastLayer;
    private bool _initialized;

    /// <summary>
    /// 进入任意场景时兜底触发单例创建（轻量，主菜单无害）。
    /// Singleton 在 Instance 首次访问时 AddComponent 并执行 Awake，完成 UI 搭建与订阅。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCreated()
    {
        if (Instance != null)
            Instance.EnsureReady();
    }

    private void Awake()
    {
        EnsureReady();
    }

    /// <summary>
    /// 兜底：UIDocument 的 rootVisualElement 在附加 panelSettings 后可能延迟一帧就绪，
    /// Start 再确认一次（首次可能实例化于 Awake 内，root 当时仍为 null）。
    /// </summary>
    private void Start()
    {
        EnsureReady();
    }

    /// <summary>幂等地初始化 UIDocument / PanelSettings / toast 图层。</summary>
    private void EnsureReady()
    {
        if (_initialized) return;

        try
        {
            // 运行时动态创建 PanelSettings（avoid 手工改场景资产）。
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 950);
            _panelSettings.sortingOrder = SortingOrder;

            // 运行时动态挂 UIDocument（本组件要求无场景预置资产）。
            if (_document == null)
                _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            var root = _document.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[ToastManager] rootVisualElement 就绪前被访问，忽略本次初始化。");
                return;
            }

            // 整屏面板不拦截任何点击（toast 是纯提示，不夺交互）。
            root.pickingMode = PickingMode.Ignore;

            // 顶部居中 toast 图层（flex 列布局，多条自动向下堆叠）。
            _toastLayer = new VisualElement { name = "toast-layer" };
            _toastLayer.style.position = Position.Absolute;
            _toastLayer.style.top = 24;
            _toastLayer.style.left = 0;
            _toastLayer.style.right = 0;
            _toastLayer.style.alignItems = Align.Center;
            _toastLayer.pickingMode = PickingMode.Ignore;
            root.Add(_toastLayer);

            _initialized = true;
            Debug.Log("[ToastManager] 已就绪（运行时创建 UIDocument + PanelSettings）。");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ToastManager] 初始化失败: {e}");
        }
    }

    /// <summary>入栈前只需在 OnEnable 订阅（EnsureReady 由 Awake 完成）。</summary>
    private void OnEnable()
    {
        EventBus.Subscribe<GuardRegionLostEvent>(OnGuardRegionLost);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GuardRegionLostEvent>(OnGuardRegionLost);
    }

    /// <summary>守卫区域丢失 → 弹守卫告警角标（方向字段后做，不现编）。</summary>
    private void OnGuardRegionLost(GuardRegionLostEvent evt)
    {
        string regionName = ResolveRegionName(evt.ResourceNode);
        Show($"守卫告警：{regionName} 区受袭", DefaultDuration);
    }

    /// <summary>
    /// 解析受袭区域名：Building.def.displayName → def.id → 世界坐标兜底。
    /// </summary>
    private static string ResolveRegionName(Building node)
    {
        if (node == null) return "未知";
        var def = node.def;
        if (def != null)
        {
            if (!string.IsNullOrEmpty(def.displayName)) return def.displayName;
            if (!string.IsNullOrEmpty(def.id)) return def.id;
        }
        return $"({node.transform.position.x:0},{node.transform.position.y:0})";
    }

    /// <summary>
    /// 通用 toast 入口：顶部居中显示 message，duration 秒后淡出移除。
    /// </summary>
    public void Show(string message, float duration = DefaultDuration)
    {
        EnsureReady();
        if (_toastLayer == null)
        {
            Debug.LogWarning("[ToastManager] toast 图层未就绪，消息被丢弃。");
            return;
        }

        Label label = new Label(message);
        label.style.marginTop = 6;
        label.style.paddingLeft = 16;
        label.style.paddingRight = 16;
        label.style.paddingTop = 8;
        label.style.paddingBottom = 8;
        label.style.fontSize = 18;
        label.style.color = new Color(1f, 0.95f, 0.85f);
        label.style.backgroundColor = new Color(0.55f, 0.08f, 0.05f, 0.92f);
        label.style.borderTopLeftRadius = 6;
        label.style.borderTopRightRadius = 6;
        label.style.borderBottomLeftRadius = 6;
        label.style.borderBottomRightRadius = 6;
        label.style.opacity = 1f;
        label.pickingMode = PickingMode.Ignore;

        _toastLayer.Add(label);
        Debug.Log($"[ToastManager] {message}");
        StartCoroutine(FadeOutAfter(label, Mathf.Max(0f, duration), DefaultFade));
    }

    /// <summary>停留 duration 后淡出至透明度 0，再移除节点。</summary>
    private IEnumerator FadeOutAfter(VisualElement el, float hold, float fade)
    {
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            el.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / fade));
            yield return null;
        }

        el.RemoveFromHierarchy();
    }
}