using UnityEngine;

/// <summary>
/// MonoBehaviour 单例基类。所有 Manager 继承此类。
/// 自动处理重复实例销毁、DontDestroyOnLoad、退出保护。
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;
    private static bool _isQuitting = false;

    public static T Instance
    {
        get
        {
            if (_isQuitting)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 游戏正在关闭，不再提供实例。");
                return null;
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
}
