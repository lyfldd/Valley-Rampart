using UnityEngine;

/// <summary>
/// MonoBehaviour 单例基类。所有 Manager 继承此类。
/// 自动处理重复实例销毁、DontDestroyOnLoad、退出保护。
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;
    private static bool _isQuitting = false;

    // Editor 关闭 Domain Reload 后静态字段不会自动归零，
    // SubsystemRegistration 在每次进入 Play Mode 时最早执行，确保干净起步。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isQuitting = false;
    }

    public static T Instance
    {
        get
        {
            // 退出时：对象还活着就给，不再隐式创建
            // （原逻辑一刀切返回 null，导致 TeardownScene 拿不到存活的单例 → NRE/清理跳过）
            if (_isQuitting)
            {
                if (_instance != null)
                {
                    try { return _instance; }
                    catch { return null; }  // _instance 是 fake null，放弃
                }
                return null;  // 对象确实没了才返回 null，且不再隐式创建
            }

            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();

                if (_instance == null)
                {
                    // R1: 隐式自动创建是潜在风险源头，用 Warning 提示开发者
                    Debug.LogWarning($"[{typeof(T).Name}] 场景中未找到实例，自动创建。建议在场景中显式放置以避免隐式分离。");
                    GameObject go = new GameObject($"[Singleton] {typeof(T).Name}");
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 已存在实例，销毁重复对象。");
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    /// <summary>
    /// 源头防范：对象销毁时主动清静态引用，杜绝 _instance 指向 fake null。
    /// 配合 Instance getter 的 _instance != null 检查，双重保险。
    /// 子类 override 时必须调 base.OnDestroy()，否则引用不清。
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
