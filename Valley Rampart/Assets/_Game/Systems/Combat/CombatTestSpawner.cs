using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗系统验证场景生成器（3.4 + 3.0.1 + 3.0.1_3 编队）。
/// 挂在 GameScene 的任意 GameObject 上，游戏启动后自动生成测试单位 + 编组。
///
/// 验证场景（3.0.1_3 编队版）：
///   我方：1 将军 + 3 近战 + 2 弓手（满编 6 人）+ 1 工人旁观
///   敌方：左单线 3 敌（含 1 远程）先刷；右侧延迟 5s 增援 2 敌（双线走查）
///   编组：生成完成后将军 FormationController 招募满编，默认防守意图列队
///
/// Debug 热键（场景可见验证）：
///   1 = 防守意图（列队守槽）
///   2 = 进攻意图（将军带头推进向敌）
///   3 = 撤退意图（近战殿后，弓手先回）
///   4 = 切守城编队（无将军，绑城墙锚点）——需先按 5 解散将军编队
///   5 = 解散编队（测试减员状态清理）
///   6 = 补员（测试补充机制，同初始招募流程）
///   7 = 残编测试（杀掉 1 近战触发防抖重排）
///
/// 3.4 验证：有 NPC 死亡 + 远程投射物命中 + 受击闪红 + 无报错
/// 3.0.1 验证：NPCBrain 驱动攻击 + 工人受威胁撤退 + 受击->威胁3->撤退闭环
/// 3.0.1_3 验证：列队/跟随/守阵交战/破阵回援/进攻推进/守城/减员重排/补员
/// </summary>
public class CombatTestSpawner : MonoBehaviour
{
    [Header("生成位置（y=-3 为地面基线）")]
    [SerializeField] private bool _autoSpawn = true;

    [Header("编队配置")]
    [Tooltip("阵型查找表 SO（若留空，运行时 Resources.Load）")]
    [SerializeField] private FormationTable _formationTable;

    [Header("敌方增援（双线走查 §7.2）")]
    [Tooltip("右侧增援延迟时间（秒）")]
    [SerializeField] private float _reinforceDelay = 5f;
    [Tooltip("右侧增援数量")]
    [SerializeField] private int _reinforceCount = 2;

    private FormationController _generalFormation;  // 将军编队
    private FormationController _garrisonFormation; // 守城编队（无将军）
    private GameObject _wallAnchorLeft;             // 左城墙锚点
    private GameObject _wallAnchorRight;            // 右城墙锚点
    private UnitController _generalUnit;            // 将军单位引用

    private void Start()
    {
        if (_autoSpawn) StartCoroutine(SpawnAfterInit());
    }

    /// <summary>等待 UnitDataManager 初始化后生成测试单位。</summary>
    private IEnumerator SpawnAfterInit()
    {
        // 等待 UnitDataManager 就绪
        while (UnitDataManager.Instance == null || !UnitDataManager.Instance.IsInitialized)
            yield return null;

        // 等待 GridSystem 就绪
        while (GridSystem.Instance == null || GridSystem.Instance.Config == null)
            yield return null;

        yield return new WaitForSeconds(0.5f); // 等一帧让其他系统稳定

        CreateWallAnchors();
        SpawnTestUnits();
        FormUpGeneral();
        StartCoroutine(SpawnReinforcement());
    }

    /// <summary>创建城墙锚点（§14.7 守城编队静态锚点，2 个空 Transform）</summary>
    private void CreateWallAnchors()
    {
        _wallAnchorLeft = new GameObject("WallAnchor_Left");
        _wallAnchorLeft.transform.position = new Vector2(-12f, -3f);

        _wallAnchorRight = new GameObject("WallAnchor_Right");
        _wallAnchorRight.transform.position = new Vector2(-10f, -3f);

        Debug.Log("[3.0.1_3] 城墙锚点创建：Left@-12, Right@-10");
    }

    [ContextMenu("生成测试单位")]
    public void SpawnTestUnits()
    {
        Debug.Log("[3.0.1_3 验证] 开始生成测试单位（满编 6 人）...");

        // ===== 我方满编（左侧，x=-9~-5）=====
        // 1 将军（挂 FormationController）
        GameObject generalGo = SpawnUnit(Faction.Human_Player, Occupation.General, new Vector2(-7f, -3f));
        _generalUnit = generalGo != null ? generalGo.GetComponent<UnitController>() : null;

        // 3 近战士兵
        SpawnUnit(Faction.Human_Player, Occupation.Warrior, new Vector2(-9f, -3f));
        SpawnUnit(Faction.Human_Player, Occupation.Warrior, new Vector2(-8f, -3f));
        SpawnUnit(Faction.Human_Player, Occupation.Warrior, new Vector2(-6f, -3f));

        // 2 弓手
        SpawnUnit(Faction.Human_Player, Occupation.Archer, new Vector2(-10f, -3f));
        SpawnUnit(Faction.Human_Player, Occupation.Archer, new Vector2(-5f, -3f));

        // 1 工人旁观（无攻击能力）
        SpawnUnit(Faction.Human_Player, Occupation.Civilian, new Vector2(-13f, -3f));

        // ===== 敌方左单线（右侧，x=4~6）=====
        SpawnUnit(Faction.Undead, Occupation.Warrior, new Vector2(5f, -3f));
        SpawnUnit(Faction.Undead, Occupation.Warrior, new Vector2(4f, -3f));
        SpawnUnit(Faction.Undead, Occupation.Archer, new Vector2(6f, -3f));

        Debug.Log("[3.0.1_3 验证] 我方满编 6 人 + 工人旁观 vs 敌方 3 人（左单线先刷，右侧延迟增援）");
    }

    /// <summary>右侧敌方增援（双线走查 §7.2）</summary>
    private IEnumerator SpawnReinforcement()
    {
        yield return new WaitForSeconds(_reinforceDelay);
        Debug.Log($"[3.0.1_3 验证] 右侧增援到达：{_reinforceCount} 敌");
        for (int i = 0; i < _reinforceCount; i++)
        {
            SpawnUnit(Faction.Undead, Occupation.Warrior, new Vector2(8f + i, -3f));
        }
    }

    /// <summary>将军编组（FormationController 招募满编）</summary>
    private void FormUpGeneral()
    {
        if (_generalUnit == null)
        {
            Debug.LogError("[3.0.1_3] 将军未生成，无法编组！");
            return;
        }

        // 给将军挂 FormationController（若未挂）
        _generalFormation = _generalUnit.GetComponent<FormationController>();
        if (_generalFormation == null)
        {
            _generalFormation = _generalUnit.gameObject.AddComponent<FormationController>();
        }

        // 加载阵型表
        if (_formationTable == null)
            _formationTable = Resources.Load<FormationTable>("Formations/FormationTable");
        _generalFormation.formationTable = _formationTable;

        // 绑定将军 + 招募（绕开 ScheduleCenterStub 自管）
        _generalFormation.BindGeneral(_generalUnit);
        _generalFormation.SetAdvanceTarget(new Vector2(5f, -3f));  // 推进目标=敌方初始位置
        _generalFormation.RecruitStandard();

        Debug.Log("[3.0.1_3 验证] 将军编组完成，默认防守意图。热键 1-7 切换演示。");
    }

    /// <summary>切换/创建守城编队（无将军，绑城墙锚点）</summary>
    private void FormUpGarrison()
    {
        if (_garrisonFormation == null)
        {
            var go = new GameObject("GarrisonController");
            go.transform.position = _wallAnchorLeft.transform.position;
            _garrisonFormation = go.AddComponent<FormationController>();
            _garrisonFormation.formationTable = _formationTable != null ? _formationTable : Resources.Load<FormationTable>("Formations/FormationTable");
            // 守城编队模式：isGarrison=true + 锚点=城墙点 Transform（Awake 时 isGarrison 默认 false，需显式初始化）
            _garrisonFormation.InitGarrison(_wallAnchorLeft.transform);
            // 守城编队不绑定将军（_generalUnit 保持 null），锚点 = 城墙点 Transform
            // DispatchOrders 在 _generalUnit=null 且 isGarrison=true 时用第一个成员作锚点占位（已知限制：该成员移动会带偏全队，守城成员静止守槽可接受）
        }
        _garrisonFormation.RecruitStandard();
        Debug.Log("[3.0.1_3 验证] 守城编队组队完成（无将军，城墙锚点，isGarrison=true）。");
    }

    private void Update()
    {
        HandleDebugKeys();
    }

    /// <summary>Debug 热键（场景可见验证清单 §3.3）</summary>
    private void HandleDebugKeys()
    {
        // 1 = 防守意图
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (_generalFormation != null)
            {
                _generalFormation.SetIntent(TacticIntent.Defense);
                Debug.Log("[热键 1] 防守意图：列队守槽，弓手殿后，将军居中。");
            }
        }
        // 2 = 进攻意图（将军带头推进）
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (_generalFormation != null)
            {
                _generalFormation.SetIntent(TacticIntent.Charge);
                Debug.Log("[热键 2] 进攻意图：将军带头推进，士兵跟槽位。将军靠威胁焦点自驱动推进（P1 改 TaskStimulus）。");
            }
        }
        // 3 = 撤退意图
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (_generalFormation != null)
            {
                _generalFormation.SetIntent(TacticIntent.Retreat);
                Debug.Log("[热键 3] 撤退意图：弓手先走，近战殿后。");
            }
        }
        // 4 = 守城编队（无将军）
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            FormUpGarrison();
            Debug.Log("[热键 4] 守城编队组队（无将军，城墙锚点）。");
        }
        // 5 = 解散将军编队（测试减员状态清理）
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            if (_generalFormation != null)
            {
                _generalFormation.DisbandAll();
                Debug.Log("[热键 5] 将军编队解散，全体状态清理（ClearFormationState）。");
            }
        }
        // 6 = 补员（测试补充机制）
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            if (_generalFormation != null)
            {
                _generalFormation.RecruitReinforcement();
                Debug.Log("[热键 6] 补员（同初始招募流程）。");
            }
        }
        // 7 = 残编测试（杀掉编队中第一个近战成员触发防抖重排）
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            KillFirstMeleeMember();
        }
    }

    /// <summary>杀掉编队中第一个近战成员（残编测试 §7.3 / §15.3）</summary>
    private void KillFirstMeleeMember()
    {
        if (_generalFormation == null || _generalUnit == null) return;
        // 找场景内一个编队中的近战士兵，调其 TakeDamage 致死
        var brains = FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);
        foreach (var brain in brains)
        {
            if (!brain.HasFormationSlot) continue;
            var unit = brain.GetComponent<UnitController>();
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.occupation != Occupation.Warrior) continue;
            // 致死一击
            unit.TakeDamage(unit.CurrentHp);
            Debug.Log($"[热键 7] 残编测试：杀掉近战士兵，触发 1s 防抖重排。");
            return;
        }
        Debug.Log("[热键 7] 无可杀的近战编队成员。");
    }

    /// <summary>生成单个单位并注册到 GridSystem。</summary>
    private GameObject SpawnUnit(Faction faction, Occupation occupation, Vector2 position)
    {
        GameObject go = UnitFactory.Instance?.SpawnUnit(faction, occupation, position);
        if (go == null)
        {
            Debug.LogError($"[3.0.1_3 验证] 生成失败: {faction}_{occupation}");
            return null;
        }

        // 注册到 GridSystem（空间分区查目标/投射物到达检测依赖此注册）
        var controller = go.GetComponent<UnitController>();
        if (controller != null && GridSystem.Instance != null)
        {
            GridCoord coord = GridSystem.Instance.WorldToCoord(position);
            GridSystem.Instance.TryEnter(controller, coord);
        }

        Debug.Log($"[3.0.1_3 验证] 生成: {faction}_{occupation} @ {position}");
        return go;
    }
}
