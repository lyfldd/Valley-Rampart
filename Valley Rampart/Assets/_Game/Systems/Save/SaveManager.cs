using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 存档管理器。纯工具层：序列化 → 写磁盘；读磁盘 → 反序列化 → 分发。
/// 不耦合任何业务字段，不知道"血量""金币""天数"是什么。
/// </summary>
[DefaultExecutionOrder(-90)]
public class SaveManager : Singleton<SaveManager>
{
    private const int CurrentSaveVersion = 1;
    private const string SaveFolderName = "Saves";
    private const string SaveFileExtension = ".json";

    // 注册表：SaveId → ISaveable
    private readonly Dictionary<string, ISaveable> _saveables = new Dictionary<string, ISaveable>();

    // 场景对象重建器列表
    private readonly List<ISaveableSpawner> _spawners = new List<ISaveableSpawner>();

    // ===== 自动存档 =====

    /// <summary>当前活跃槽位（NewGame 时设为 selectedSlotId，ContinueGame 时设为 LoadSlotId）。</summary>
    public string CurrentSlotId { get; private set; }

    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalDays { get; set; } = 3;

    private int _daysSinceLastAutoSave;

    protected override void Awake()
    {
        base.Awake();

        // 从全局设置读取自动存档配置
        LoadSettings();
    }

    private void Start()
    {
        // 订阅天数变化事件，用于自动存档
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);

        // 订阅状态变化，用于 GameOver 时标记存档已结束
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        if (!AutoSaveEnabled || string.IsNullOrEmpty(CurrentSlotId)) return;

        _daysSinceLastAutoSave++;
        if (_daysSinceLastAutoSave >= AutoSaveIntervalDays)
        {
            Save(CurrentSlotId);
            _daysSinceLastAutoSave = 0;
        }
    }

    // ===== GameOver 存档标记 =====

    /// <summary>GameOver 时把当前槽位的存档标记为"已结束"。</summary>
    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState != GameState.GameOver) return;
        if (string.IsNullOrEmpty(CurrentSlotId)) return;

        MarkCurrentSaveFinished();
    }

    /// <summary>将当前槽位的存档标记为已结束（覆盖写盘，不重新抓快照）。</summary>
    public void MarkCurrentSaveFinished()
    {
        string path = GetSavePath(CurrentSlotId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] 当前槽位 {CurrentSlotId} 无存档文件，跳过标记。");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            GameSaveRoot root = JsonUtility.FromJson<GameSaveRoot>(json);
            if (root.isFinished)
            {
                Debug.Log($"[SaveManager] 存档 {CurrentSlotId} 已标记为结束，跳过。");
                return;
            }

            root.isFinished = true;
            string updatedJson = JsonUtility.ToJson(root, prettyPrint: true);

            // 原子写入
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, updatedJson);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            Debug.Log($"[SaveManager] 存档 {CurrentSlotId} 已标记为结束。");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 标记存档结束失败: {e}");
        }
    }

    /// <summary>重置自动存档计数器（新建游戏时调用，防止上一局的计数残留）。</summary>
    public void ResetAutoSaveCounter()
    {
        _daysSinceLastAutoSave = 0;
    }

    // ===== 注册 / 注销 =====

    public void RegisterSaveable(ISaveable saveable)
    {
        if (saveable == null) return;

        if (_saveables.TryGetValue(saveable.SaveId, out var existing))
        {
            Debug.LogWarning($"[SaveManager] SaveId 重复: {saveable.SaveId}，已存在 {existing}，新的 {saveable} 被忽略。");
            return;
        }
        _saveables.Add(saveable.SaveId, saveable);
    }

    public void UnregisterSaveable(ISaveable saveable)
    {
        if (saveable == null) return;
        if (string.IsNullOrEmpty(saveable.SaveId)) return;
        _saveables.Remove(saveable.SaveId);
    }

    public void RegisterSpawner(ISaveableSpawner spawner)
    {
        if (!_spawners.Contains(spawner)) _spawners.Add(spawner);
    }

    // ===== 查询 / 清理 =====

    /// <summary>检查指定 SaveId 是否已注册（用于 SpawnFromSave 去重）。</summary>
    public bool HasSaveable(string saveId)
    {
        return !string.IsNullOrEmpty(saveId) && _saveables.ContainsKey(saveId);
    }

    /// <summary>
    /// R6: 清理已销毁的 MonoBehaviour 引用。场景切换后旧场景的单位已被 Unity 销毁，
    /// 但 _saveables 字典仍持有 C# 引用。此方法扫描并移除这些"幽灵引用"。
    /// 应在 GameBootstrap.Awake 中调用。
    /// </summary>
    public void CleanupDestroyedSaveables()
    {
        var toRemove = new List<string>();
        foreach (var kvp in _saveables)
        {
            if (kvp.Value is MonoBehaviour mb && mb == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _saveables.Remove(id);
        }

        if (toRemove.Count > 0)
        {
            Debug.Log($"[SaveManager] 清理了 {toRemove.Count} 个已销毁的 ISaveable 引用");
        }
    }

    /// <summary>
    /// R4: 原子化更换 SaveId。先移除旧 key 再添加新 key，封装在一处便于未来优化。
    /// </summary>
    public void ChangeSaveId(string oldId, string newId, ISaveable saveable)
    {
        if (saveable == null) return;
        if (string.IsNullOrEmpty(newId)) return;

        if (!string.IsNullOrEmpty(oldId) && _saveables.ContainsKey(oldId))
        {
            _saveables.Remove(oldId);
        }

        if (_saveables.ContainsKey(newId))
        {
            Debug.LogWarning($"[SaveManager] ChangeSaveId: 目标 ID '{newId}' 已存在，覆盖。");
            _saveables[newId] = saveable;
        }
        else
        {
            _saveables.Add(newId, saveable);
        }
    }

    // ===== 保存 =====

    public bool Save(string slotId)
    {
        CurrentSlotId = slotId;

        try
        {
            var root = new GameSaveRoot
            {
                saveVersion = CurrentSaveVersion,
                saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                slotName = slotId
            };

            // 先填充 summary（修复 P1-2：存档槽 UI 读不到摘要）
            root.summary = BuildSummary();

            // 遍历所有注册对象，收集状态。SaveManager 不关心每个模块存了什么。
            foreach (var kvp in _saveables)
            {
                ISaveable saveable = kvp.Value;

                // 跳过已销毁的 MonoBehaviour
                if (saveable is MonoBehaviour mb && mb == null) continue;

                SavePayload payload = saveable.SaveState();

                root.modules.Add(new ModuleSaveEntry
                {
                    saveId = saveable.SaveId,
                    typeName = payload.typeName,
                    json = payload.json,
                    version = payload.version,
                    phase = (int)saveable.LoadPhase
                });
            }

            string json = JsonUtility.ToJson(root, prettyPrint: true);
            string path = GetSavePath(slotId);

            // 原子写入：先写 .tmp 再替换，防断电损坏
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            Debug.Log($"[SaveManager] 保存成功: {path}，共 {root.modules.Count} 个模块");
            EventBus.Publish(new GameSavedEvent(slotId, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 保存失败: {e}");
            EventBus.Publish(new GameSavedEvent(slotId, false));
            return false;
        }
    }

    // ===== 加载 =====

    public bool Load(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] 存档不存在: {path}");
            return false;
        }

        try
        {
            CurrentSlotId = slotId;
            _daysSinceLastAutoSave = 0;

            string json = File.ReadAllText(path);
            GameSaveRoot root = JsonUtility.FromJson<GameSaveRoot>(json);

            // 拒绝加载已结束的存档（GameOver 后的死档）
            if (root.isFinished)
            {
                Debug.LogWarning($"[SaveManager] 存档 {slotId} 已标记为结束，拒绝加载。请选择其他存档或新建游戏。");
                return false;
            }

            // 全局版本迁移（整体格式升级，比如改了 GameSaveRoot 结构）
            if (root.saveVersion > CurrentSaveVersion)
            {
                Debug.LogError($"[SaveManager] 存档版本 {root.saveVersion} 高于当前支持 {CurrentSaveVersion}，拒绝加载。");
                return false;
            }

            // 阶段 1: 全局模块恢复
            DistributePayloads(root.modules, SaveLoadPhase.Global);

            // 阶段 1.5: 场景对象重建（让 spawner 把实例创建出来并注册 ISaveable）
            foreach (var entry in root.modules)
            {
                if (entry.phase != (int)SaveLoadPhase.Scene) continue;
                foreach (var spawner in _spawners)
                {
                    if (entry.saveId.StartsWith(spawner.SaveIdPrefix))
                    {
                        spawner.SpawnFromSave(entry);
                        break;
                    }
                }
            }

            // 阶段 2: 场景模块恢复 LoadState（此时实例已注册）
            DistributePayloads(root.modules, SaveLoadPhase.Scene);

            Debug.Log($"[SaveManager] 加载成功: {path}");
            EventBus.Publish(new GameLoadedEvent(slotId, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 加载失败: {e}");
            EventBus.Publish(new GameLoadedEvent(slotId, false));
            return false;
        }
    }

    /// <summary>按阶段分发载荷给已注册的 ISaveable。</summary>
    private void DistributePayloads(List<ModuleSaveEntry> modules, SaveLoadPhase phase)
    {
        foreach (var entry in modules)
        {
            if (entry.phase != (int)phase) continue;

            if (!_saveables.TryGetValue(entry.saveId, out var saveable))
            {
                Debug.LogWarning($"[SaveManager] 存档模块 {entry.saveId} 未找到对应对象，跳过。");
                continue;
            }

            var payload = new SavePayload
            {
                typeName = entry.typeName,
                json = entry.json,
                version = entry.version
            };

            try
            {
                saveable.LoadState(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 模块 {entry.saveId} LoadState 失败: {e}");
            }
        }
    }

    // ===== 删除 / 查询 =====

    public bool Delete(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool HasSave(string slotId) => File.Exists(GetSavePath(slotId));

    /// <summary>检查任意槽位是否有存档（主菜单"继续游戏"按钮启用判断）。</summary>
    public bool HasAnySave(params string[] slotIds)
    {
        if (slotIds == null || slotIds.Length == 0)
        {
            slotIds = new[] { "slot_1", "slot_2", "slot_3" };
        }
        foreach (var id in slotIds)
        {
            if (HasSave(id)) return true;
        }
        return false;
    }

    /// <summary>读取存档元数据（用于存档列表 UI，不触发 LoadState）。</summary>
    public GameSaveRoot GetSaveMeta(string slotId)
    {
        string path = GetSavePath(slotId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<GameSaveRoot>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    // ===== 摘要生成 =====

    /// <summary>从当前游戏状态构造存档摘要。任意字段不可用时给默认值，不抛异常。</summary>
    private GameSaveSummary BuildSummary()
    {
        var summary = new GameSaveSummary();

        try
        {
            if (RulerController.Instance != null)
            {
                summary.rulerName = RulerController.Instance.RulerName;
            }
        }
        catch { }

        try
        {
            if (TimeManager.Instance != null)
            {
                summary.currentDay = TimeManager.Instance.CurrentDay;
                summary.currentSeason = (int)TimeManager.Instance.CurrentSeason;
            }
        }
        catch { }

        try
        {
            if (WorldManager.Instance != null)
            {
                summary.difficulty = WorldManager.Instance.Difficulty;
            }
        }
        catch { }

        return summary;
    }

    private string GetSavePath(string slotId)
    {
        string folder = Path.Combine(Application.persistentDataPath, SaveFolderName);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        return Path.Combine(folder, slotId + SaveFileExtension);
    }

    // ===== 全局设置（自动存档开关/间隔）=====

    private void LoadSettings()
    {
        string path = GetSettingsPath();
        if (!File.Exists(path)) return;

        try
        {
            string json = File.ReadAllText(path);
            var settings = JsonUtility.FromJson<GameSettings>(json);
            if (settings != null)
            {
                AutoSaveEnabled = settings.autoSaveEnabled;
                AutoSaveIntervalDays = settings.autoSaveIntervalDays;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 读取全局设置失败: {e}");
        }
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new GameSettings
            {
                autoSaveEnabled = AutoSaveEnabled,
                autoSaveIntervalDays = AutoSaveIntervalDays
            };
            string json = JsonUtility.ToJson(settings, prettyPrint: true);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 保存全局设置失败: {e}");
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Application.persistentDataPath, "settings.json");
    }

    [Serializable]
    private class GameSettings
    {
        public bool autoSaveEnabled = true;
        public int autoSaveIntervalDays = 3;
    }
}
