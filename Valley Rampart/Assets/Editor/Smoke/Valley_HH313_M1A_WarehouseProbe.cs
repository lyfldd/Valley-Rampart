using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// `M1-A` 仓库结构探针（`HH.313` · `09_资源与仓库.md` §十一 判据 1~4 ＋ 判据 6 存档往返）。
/// ⭐ 纯 Edit Mode（不起游戏场景、不进 Play Mode）：`StorageComponent` 的多资源容器语义与资源表
/// 都是纯数据/纯组件行为，无需世界上下文 ⇒ 读数可复现、零副作用。
/// 输出：`Debug.Log` 单条汇总 ＋ 工程根 `M1A_判据读数.txt`。
/// </summary>
public static class Valley_HH313_M1A_WarehouseProbe
{
    private static readonly StringBuilder Sb = new StringBuilder();

    [MenuItem("ValleyRampart/探针/M1-A 仓库与资源表读数（HH.313）")]
    public static void Run()
    {
        Sb.Length = 0;
        Sb.AppendLine("M1-A 判据 1~4 / 6 实测读数（HH.313 · Edit Mode · StorageComponent 单元级）");
        Sb.AppendLine("═══ 判据 1：一栋建筑只改一行 ═══");
        Case1_OneDataLine();
        Sb.AppendLine("═══ 判据 2：一仓装多资源、共占一条容量线 ═══");
        Case2_MultiResourceOneCapacityLine();
        Sb.AppendLine("═══ 判据 3：容量按体积算 ═══");
        Case3_VolumeBasedCapacity();
        Sb.AppendLine("═══ 判据 4：前缀可读 ═══");
        Case4_PrefixReadable();
        Sb.AppendLine("═══ 判据 6：新档能存能读（ResourceList 存档往返） ═══");
        Case6_SaveRoundTrip();
        Sb.AppendLine("═══ 附：资源表落码行数 ═══");
        CountCatalog();

        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "M1A_判据读数.txt"));
        System.IO.File.WriteAllText(full, Sb.ToString(), new UTF8Encoding(false));
        Debug.Log(Sb.ToString());
        Debug.Log($"[M1-A 判据读数] 已写盘 ⇒ {full}");
    }

    // ===== 判据 1 =====

    private static void Case1_OneDataLine()
    {
        // 同一个 StorageComponent 类、同一段代码；唯一差异 = 声明行（`09` §十一 判据 1）
        var mat = NewStorage(20, new[] { "res_material" });
        var food = NewStorage(20, new[] { "res_food" });

        Sb.AppendLine($"声明行 A = res_material  ⇒ Accepts(Stone)={mat.Accepts(ResourceType.Stone)} Accepts(Wood)={mat.Accepts(ResourceType.Wood)} Accepts(Food)={mat.Accepts(ResourceType.Food)} Accepts(Gold)={mat.Accepts(ResourceType.Gold)}");
        Sb.AppendLine($"声明行 B = res_food      ⇒ Accepts(Stone)={food.Accepts(ResourceType.Stone)} Accepts(Food)={food.Accepts(ResourceType.Food)} Accepts(SpecialFood)={food.Accepts(ResourceType.SpecialFood)} Accepts(Meat)={food.Accepts(ResourceType.Meat)}");
        Sb.AppendLine($"实投验证：A.Add(Stone,7)={mat.Add(ResourceType.Stone, 7)}；A.Add(Food,7)={mat.Add(ResourceType.Food, 7)}（标签不匹配 ⇒ 拒收 0）");
        Sb.AppendLine($"          B.Add(Stone,7)={food.Add(ResourceType.Stone, 7)}；B.Add(Food,7)={food.Add(ResourceType.Food, 7)}");
        Sb.AppendLine($"⇒ 收什么完全由**一行声明数据**决定；StorageComponent 代码零分支（无 outputResource 参与）。");

        Dispose(mat);
        Dispose(food);
    }

    // ===== 判据 2 =====

    private static void Case2_MultiResourceOneCapacityLine()
    {
        var st = NewStorage(20, new[] { "res_material" });   // 分类仓：全部材料
        int a = st.Add(ResourceType.Stone, 5);
        int b = st.Add(ResourceType.Wood, 4);
        int c = st.Add(ResourceType.Ore, 3);
        Sb.AppendLine($"仓：声明 res_material、capacity={st.capacity}");
        Sb.AppendLine($"投入 Stone5/Wood4/Ore3 ⇒ 实际入仓 {a}/{b}/{c}");
        Sb.AppendLine($"三种并存读数：GetAmount(Stone)={st.GetAmount(ResourceType.Stone)} GetAmount(Wood)={st.GetAmount(ResourceType.Wood)} GetAmount(Ore)={st.GetAmount(ResourceType.Ore)}");
        Sb.AppendLine($"一条容量线：TotalCount={st.TotalCount} UsedSpace={st.UsedSpace} FreeSpace={st.FreeSpace} capacity={st.capacity}（⇒ 共占同一条容量线，⛔ 非每资源一条）");
        var q = st.Query();
        var sbq = new StringBuilder();
        for (int i = 0; i < q.Count; i++) sbq.Append(q[i].type).Append('×').Append(q[i].amount).Append(i < q.Count - 1 ? ", " : "");
        Sb.AppendLine($"Query() 条目数={q.Count} ⇒ {sbq}");
        Dispose(st);
    }

    // ===== 判据 3 =====

    private static void Case3_VolumeBasedCapacity()
    {
        var st = NewStorage(10, new[] { "res" });   // 容量 10
        int r1 = st.Add(ResourceType.Stone, 10);
        int r2 = st.Add(ResourceType.Stone, 1);
        Sb.AppendLine($"容量 10 ＋ 石材体积={ResourceCatalog.VolumeOf(ResourceType.Stone)} ⇒ Add(Stone,10)={r1}；再加 Add(Stone,1)={r2}（已满拒绝）；CanAccept(Stone)={st.CanAccept(ResourceType.Stone)}；UsedSpace={st.UsedSpace}");
        // 体积 0 资源（金币）：不占容量
        var st2 = NewStorage(10, new[] { "res" });
        st2.Add(ResourceType.Stone, 10);
        int goldOk = st2.Add(ResourceType.Gold, 999);
        Sb.AppendLine($"体积 0 资源（金币=体积 {ResourceCatalog.VolumeOf(ResourceType.Gold)}）⇒ 满仓仍可入 Add(Gold,999)={goldOk}；UsedSpace 仍={st2.UsedSpace}（不占容量）");
        Sb.AppendLine($"⭐ 口径确认：CanAccept = floor(剩余容量 ÷ 体积)（[StorageComponent.cs] CanAccept）；体积 0 ⇒ int.MaxValue");
        Sb.AppendLine("⚠️ 局限（如实记）：资源表体积初值**全 1、仅金 0**（`09` §4.2 已定 · 数值批可调）⇒ **无体积=3 的资源样本**，");
        Sb.AppendLine("   「容量 10 ÷ 体积 3 = 3 个」这一步无法用现网资源实测；上式已由 体积=1（10÷1=10）与 体积=0 两侧端点读数夹住。");
        Dispose(st);
        Dispose(st2);
    }

    // ===== 判据 4 =====

    private static void Case4_PrefixReadable()
    {
        var st = NewStorage(30, new[] { "res" });
        st.Add(ResourceType.Stone, 5);
        st.Add(ResourceType.Wood, 4);
        st.Add(ResourceType.Ore, 3);
        st.Add(ResourceType.Food, 6);
        Sb.AppendLine($"仓内：Stone5/Wood4/Ore3/Food6");
        Sb.AppendLine($"查子树 res_material ⇒ SumByPrefix(\"res_material\")={st.SumByPrefix("res_material")}（＝石5＋木4＋矿3＝12，一次拿到合计）");
        Sb.AppendLine($"查子树 res           ⇒ SumByPrefix(\"res\")={st.SumByPrefix("res")}（＝全部 18）");
        Sb.AppendLine($"查子树 res_food      ⇒ SumByPrefix(\"res_food\")={st.SumByPrefix("res_food")}（＝粮 6）");
        Sb.AppendLine($"查单项               ⇒ GetAmount(Stone)={st.GetAmount(ResourceType.Stone)} GetAmount(Ore)={st.GetAmount(ResourceType.Ore)}");
        Sb.AppendLine($"段边界反例           ⇒ SumByPrefix(\"res_mat\")={st.SumByPrefix("res_mat")}（段未对齐 ⇒ 0，`09` §3.2）");
        Dispose(st);
    }

    // ===== 判据 6 =====

    private static void Case6_SaveRoundTrip()
    {
        var src = NewStorage(50, new[] { "res" });
        src.Add(ResourceType.Stone, 7);
        src.Add(ResourceType.Wood, 3);
        src.Add(ResourceType.Ore, 2);
        src.Add(ResourceType.StoneAmmo, 1);
        var vaultContents = ResourceList.Empty
            .Add(ResourceType.Gold, 123)
            .Add(ResourceType.Metal, 9)
            .Add(ResourceType.Crystal, 4);

        var data = new BuildingSaveData
        {
            defId = "Warehouse",
            coordX = 12, coordY = 34,
            level = 2, hp = 90, maxHp = 100,
            storageContents = src.Contents,
            treasuryContents = vaultContents
        };
        string json = JsonUtility.ToJson(data);
        var back = JsonUtility.FromJson<BuildingSaveData>(json);

        Sb.AppendLine($"存档前 storageContents = {src.Contents}");
        Sb.AppendLine($"序列化 json = {json}");
        Sb.AppendLine($"读回 storageContents  = {back.storageContents}");
        Sb.AppendLine($"读回 treasuryContents = {back.treasuryContents}");
        Sb.AppendLine($"逐项一致：storage={Same(src.Contents, back.storageContents)} treasury={Same(vaultContents, back.treasuryContents)} 容量线回灌：{Backfill(back.storageContents, 50)}");
        Sb.AppendLine($"空仓往返（全 0 ⇒ 空列表、⛔ 非 null）：items==null？{ResourceList.Empty.items == null}；条数={ResourceList.Empty.Count}");
        Dispose(src);
    }

    /// <summary>读档回灌：`RestoreContents` 逐条目还原并 clamp 到容量线。</summary>
    private static string Backfill(ResourceList saved, int capacity)
    {
        var st = NewStorage(capacity, new[] { "res" });
        st.RestoreContents(saved);
        string s = $"条数={st.Query().Count} TotalCount={st.TotalCount} UsedSpace={st.UsedSpace}/{st.capacity} 文本={st.Contents}";
        Dispose(st);
        return s;
    }

    private static bool Same(ResourceList a, ResourceList b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.items.Length; i++)
            if (b.Get(a.items[i].type) != a.items[i].amount) return false;
        return true;
    }

    // ===== 附：资源表落码行数 =====

    private static void CountCatalog()
    {
        int n = 0;
        var sb = new StringBuilder();
        foreach (var t in ResourceCatalog.AllTypes) { n++; sb.Append(t).Append('/').Append(ResourceCatalog.PrimaryPathOf(t)).Append("  "); }
        Sb.AppendLine($"ResourceCatalog 落码行数 = {n}（与 ResourceType 枚举现值 1:1；⭐ 第 14 项 medkit 归 M6 未落 ⇒ 无孤儿资源告警，`D789` Q3=A）");
        Sb.AppendLine(sb.ToString());
        int enumCount = System.Enum.GetValues(typeof(ResourceType)).Length;
        Sb.AppendLine($"ResourceType 枚举项数 = {enumCount}（表内 {n} 项，未落码 {enumCount - n} 项）");
    }

    // ===== 工具 =====

    private static StorageComponent NewStorage(int capacity, string[] paths)
    {
        var go = new GameObject("M1A_Probe_Storage");
        var st = go.AddComponent<StorageComponent>();
        st.capacity = capacity;
        st.SetDeclaredPaths(paths);
        return st;
    }

    private static void Dispose(StorageComponent st)
    {
        if (st != null) Object.DestroyImmediate(st.gameObject);
    }
}
