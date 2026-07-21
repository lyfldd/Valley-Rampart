using System;
using UnityEngine;

/// <summary>
/// 加载阶段。决定 LoadState 的调用顺序。
/// Global 阶段先执行（TimeManager、RulerController 等常驻系统），
/// Scene 阶段后执行（单位、物品等动态对象）。
/// </summary>
public enum SaveLoadPhase
{
    Global,
    Scene
}

/// <summary>
/// 模块存档载荷。每个 ISaveable 把自己的数据序列化成 JSON 字符串塞进来，
/// SaveManager 只搬运字符串，不关心内容。
/// </summary>
[Serializable]
public struct SavePayload
{
    /// <summary>数据类型的 AssemblyQualifiedName，用于反序列化时找回类型。</summary>
    public string typeName;

    /// <summary>模块数据序列化后的 JSON 字符串。</summary>
    public string json;

    /// <summary>数据版本号（模块自治，独立于全局 saveVersion），用于模块内迁移。</summary>
    public int version;
}

/// <summary>
/// 单个模块的存档条目。
/// </summary>
[Serializable]
public class ModuleSaveEntry
{
    public string saveId;
    public string typeName;
    public string json;
    public int version;
    public int phase;  // (int)SaveLoadPhase，存档里冗余一份，便于加载时排序
}

/// <summary>
/// 可存档对象契约。所有需要持久化的业务模块都实现此接口。
/// 业务模块自己管自己的数据定义、序列化、反序列化、版本迁移；
/// SaveManager 只负责收集和分发。
/// </summary>
public interface ISaveable
{
    /// <summary>全局唯一存档 ID。</summary>
    string SaveId { get; }

    /// <summary>加载阶段。全局系统 = Global，场景对象 = Scene。</summary>
    SaveLoadPhase LoadPhase { get; }

    /// <summary>保存时调用：把自身状态序列化成 SavePayload 返回。</summary>
    SavePayload SaveState();

    /// <summary>加载时调用：接收存档载荷，自行反序列化并恢复状态。</summary>
    void LoadState(SavePayload payload);
}

/// <summary>
/// 场景对象重建器。需要从存档动态创建实例的模块实现此接口。
/// SaveManager 在 Global 阶段后、Scene 阶段前调用 SpawnFromSave。
/// </summary>
public interface ISaveableSpawner
{
    /// <summary>该 spawner 负责的模块 SaveId 前缀（如 "Unit_"）。</summary>
    string SaveIdPrefix { get; }

    /// <summary>
    /// 根据存档条目创建实例并注册为 ISaveable。
    /// 参数是 ModuleSaveEntry 而不是 SavePayload——因为 spawner 创建实例后
    /// 需要把存档里的 saveId 赋给新实例（覆盖 Initialize 时分配的新 GUID），
    /// 否则 SaveManager 在 Scene 阶段按 saveId 分发 LoadState 时找不到对象。
    /// </summary>
    void SpawnFromSave(ModuleSaveEntry entry);
}
