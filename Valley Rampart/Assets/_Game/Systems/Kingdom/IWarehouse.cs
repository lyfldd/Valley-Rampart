using System.Collections.Generic;

/// <summary>
/// 仓库抽象（`09` §五／§七）：资源不凭空位移，一切获取/消耗走仓库操作。
/// ⭐ **本片（`M1-A`／`09#37`）已改「多资源容器」口径** —— 一个仓能装**所有**前缀匹配上的资源
/// （`09` §5.2 硬规则 3：⛔ 不做单一资源仓），故 `Query` 返回**该仓全部存量**（条目列表）。
///
/// ⭐ 五个动作（`09` §七）：**增**（`Deposit` · 放到满为止·部分成功）／**删**（`CanTake` 先问 ＋ `Take` 后扣）／
/// **改**（`Transform` · 原子一次）／**查**（`Query`）／**转移**（`09` §7.4 ＝ 第 5 个动作，由上层用
/// 「源删＋目标增」拼出 ⇒ ⛔ 本契约不新增方法面，搬运路径归 `M2`／`M5`）。
///
/// 实现：王国仓库（`StorageComponent`）、国库仓（`TreasureVault` 挂载的 `StorageComponent`）、
/// 工人背包（`WorkerInventory`，移动仓库）。
/// </summary>
/// <remarks>
/// ⚠️ **勘正（`M1-A`／`09#37`）**：本接口原先注释为「与 sim harness/Core 的 `IWarehouse` **签名逐字对齐**
/// （2_9 sim 对拍 + 2_12 D43/D51/D255）。单侧改签名必须记 HH 回策划」
/// ⇒ ⭐ **该对齐义务已废**：`09` §5.3 已裁【**用户授权：老设计不留、不管 sim 口径统一，Unity 端大于一切**】
/// ⇒ 本接口**可自由改**，⛔ **不再有"改签名须记 HH"的义务**。
///
/// ⚠️ **变更订阅（`09` §7.5-⑦）不在本接口面**（`M1-B` 件1 定 · ⛔ 不新增事件成员）：
/// 事件声明在实现侧（`StorageComponent.OnStorageChanged`），语义＝**只在真实量变的写口发**
/// （空转不发／部分成功也发／`RestoreContents` N＋1 次／⛔ 无节流）＋ **只带本仓引用不带 diff**
/// ⇒ **订阅方必须全量重读**（`Query` 等），⛔ 不得增量累加；节流归订阅方自理。
/// </remarks>
public interface IWarehouse
{
    /// <summary>
    /// 查（`09` §7.5-①）：返回该仓**全部存量**条目（多资源 ⇒ ⛔ 不再是"单一资源查询"）。
    /// 空仓 ⇒ 空列表（⛔ 非 null）。
    /// </summary>
    List<ResourceAmount> Query();

    /// <summary>可否取（`09` §7.2「先问后扣」的第一问）。</summary>
    bool CanTake(ResourceType type, int amt);

    /// <summary>减（删 · 尽力档 · `09` §7.2）：从本仓拿走 ≤`amt`，返回实际取走量。</summary>
    int Take(ResourceType type, int amt);

    /// <summary>
    /// 增（`09` §7.1）：放入（标签匹配 ＋ 容量允许）；返回**实际入仓量**。
    /// ⭐ 语义＝**容量不够就放到满为止（部分成功）**，⛔ 不整笔拒绝。
    /// </summary>
    int Deposit(ResourceType type, int amt);

    /// <summary>改（加工 · `09` §7.3）：就地加工增值，返回实际产出量（配方表见 <see cref="RecipeCatalog"/>）。</summary>
    int Transform(ResourceType @in, ResourceType @out, int amt);
}
