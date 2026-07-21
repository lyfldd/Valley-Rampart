using UnityEngine;

/// <summary>
/// 引擎层引导器。两个场景（MainMenuScene / GameScene）都挂。
/// 负责创建 Core 层单例：GameStateManager、EventBus、InputManager、SaveManager 等。
/// 不做任何业务初始化（不加载 UnitDataManager、不创建单位、不挂业务系统）。
///
/// 执行顺序：
///   - [-200] CoreBootstrap.Awake  → 创建单例 → SetState(Booting)
///   - 业务层（GameBootstrap / MainMenuBootstrap）从 Booting 继续推进
///
/// GameState 转换：Booting → (各场景自己的) Splash / Loading / MainMenu / ...
/// </summary>
[DefaultExecutionOrder(-200)]
public class CoreBootstrap : MonoBehaviour
{
    [Tooltip("是否在 Awake 立即把状态推到 Booting。两个场景都建议勾选，确保 Core 先就绪。")]
    [SerializeField] private bool setBootingOnAwake = true;

    protected virtual void Awake()
    {
        // 按依赖顺序触发所有单例创建
        // Singleton<T>.Instance getter 内部会自动 new GameObject + AddComponent
        var _ = GameStateManager.Instance;
        var __ = InputManager.Instance;
        var ___ = SaveManager.Instance;
        var ____ = WorldManager.Instance;

        // 只在 GameStateManager 是首次创建（CurrentState 还是默认的 Booting 且从未切换过）时
        // 才设 Booting。跨场景切换时 GameStateManager 是 DontDestroyOnLoad 的 Singleton，
        // 状态会从上一个场景延续下来，不能被覆盖。
        // 判断依据：如果 _instance 已存在（不是这次 Awake 创建的），说明是跨场景延续，跳过。
        // 由于 Singleton getter 不会告诉我们是不是新建的，这里用更稳的方式：
        //   - 如果当前状态不是 Booting，说明已经经过状态机推进，跳过
        //   - 如果是 Booting，可能是首次启动，也可能是从主菜单回来——两种情况都需要重置回 Booting 让 GameBootstrap 重新走流程
        // 但实际上 GameBootstrap.Start 会 SetState(Loading) 覆盖掉 Booting，所以这里设不设都行。
        // 真正的问题是：从主菜单切到游戏场景时，CoreBootstrap.Awake 跑得比 GameBootstrap.Start 早，
        // SetState(Booting) 会把 SaveSlotSelect 覆盖成 Booting——这本身不影响功能，
        // 因为 GameBootstrap.Start 会立刻 SetState(Loading)。
        // 但 log 里出现 "SaveSlotSelect → Booting" 看起来很怪，所以改为只在首次启动时设。
        if (setBootingOnAwake && _isFirstCoreBootstrap)
        {
            GameStateManager.Instance.SetState(GameState.Booting);
            _isFirstCoreBootstrap = false;
        }

        Debug.Log("[CoreBootstrap] Core 层单例已就绪");
    }

    // 标记是否是首次执行 CoreBootstrap.Awake（跨场景 DontDestroyOnLoad 的 Singleton 不重置）
    private static bool _isFirstCoreBootstrap = true;
}
