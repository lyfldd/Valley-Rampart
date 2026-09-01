using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

// ============================================================================
//  2_13 批D 冒烟（四职责承接：列国名单③/王国情报②/幸福可见①/染色让渡登记）
//  用法：菜单「Valley/验证/2_13_批D_四职责承接」——须 GameScene Play（先 Play 再点）。
//  自含断言；UI 真点击/真渲染行为留 Menu 流程 Play 硬性条目（诚实分层）：
//    P1 列国名单 Push/行数==Registry.Count/Pop 还原
//    P2 行点击链路 OpenKingdomIntel → 情报面板 Push + 栈顶 + 国名回填
//    P3 情报面板字段非占位（国名/资源/人口/军力/税收系数）
//    P4 幸福三惩罚因子 API 值域（增长/士气∈[0,1]，税收>0）
//    P5 HUD「列国」入口按钮在场（GameScene TopLeftHUD 实测 Q）
//    P6 FocusOnKingdom 不炸 + CameraRig 在场（玩家锚点/抽象国跳过均合法）
// ============================================================================
public static class Valley2_13_Smoke_D
{
    [MenuItem("Valley/验证/2_13_批D_四职责承接")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_13_D冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_13_D_SmokeRunner").AddComponent<DHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        yield return null;
        var results = new List<string>();
        try
        {
            // ===== 探针数据源：确保有可测王国（bootstrap 未注册玩家时兜底注册冒烟国，仍走 Registry 真路径）=====
            KingdomState probe = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(0) : null;
            if (probe == null && KingdomRegistry.Instance != null)
            {
                int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
                probe = KingdomRegistry.Instance.RegisterNewKingdom("冒烟测试国", new Color(0.8f, 0.2f, 0.2f), day, -1);
                Debug.Log("[2_13_D冒烟DIAG] 玩家国未注册 → 兜底注册冒烟测试国 id=" + probe.id);
            }
            int probeId = probe != null ? probe.id : -1;

            var listPanel = Object.FindObjectOfType<KingdomListPanel>();
            var intelPanel = Object.FindObjectOfType<KingdomIntelPanel>();
            if (listPanel == null || intelPanel == null)
                Debug.Log($"[2_13_D冒烟DIAG] 面板缺席 list={listPanel != null} intel={intelPanel != null}（场景挂载缺口，P1~P3 将 FAIL）");

            // ===== P1 列国名单 Push/行数/Pop =====
            bool p1 = false;
            if (listPanel != null && UIManager.Instance != null && KingdomRegistry.Instance != null)
            {
                int depth0 = StackDepth();
                UIManager.Instance.Push(listPanel, new Interactor(Faction.PlayerCamp, Vector3.zero));
                var listRoot = listPanel.GetComponent<UIDocument>().rootVisualElement;
                var scrollView = listRoot.Q<ScrollView>("kingdom-list");
                int rows = scrollView != null ? scrollView.childCount : -1;
                bool visible = listRoot.style.display == DisplayStyle.Flex;
                bool pushOk = ReferenceEquals(UIManager.Instance.Peek(), listPanel) && StackDepth() == depth0 + 1;
                UIManager.Instance.CloseCurrent();
                bool restored = StackDepth() == depth0
                    && listPanel.GetComponent<UIDocument>().rootVisualElement.style.display == DisplayStyle.None;
                p1 = pushOk && restored && visible && rows == KingdomRegistry.Instance.Count;
                Debug.Log($"[2_13_D冒烟DIAG] P1 rows={rows} registryCount={KingdomRegistry.Instance.Count} pushOk={pushOk} restored={restored} visible={visible}");
            }
            results.Add($"P1 列国名单 Push/行数==Registry.Count/Pop 还原 = {p1}");

            // ===== P2 行点击链路（真路径 OpenKingdomIntel）=====
            bool p2 = false;
            if (listPanel != null && intelPanel != null && UIManager.Instance != null && probeId >= 0)
            {
                int depth0 = StackDepth();
                UIManager.Instance.Push(listPanel, new Interactor(Faction.PlayerCamp, Vector3.zero));
                listPanel.OpenKingdomIntel(probeId);    // 行点击真路径（FocusOnKingdom+OpenKingdomIntel 的后半程）
                var intelRoot = intelPanel.GetComponent<UIDocument>().rootVisualElement;
                var nameLabel = intelRoot.Q<Label>("intel-name");
                bool intelVisible = intelRoot.style.display == DisplayStyle.Flex;
                bool onTop = ReferenceEquals(UIManager.Instance.Peek(), intelPanel);
                bool nameOk = nameLabel != null && probe != null && nameLabel.text == probe.name;
                // 清栈还原
                if (ReferenceEquals(UIManager.Instance.Peek(), intelPanel)) UIManager.Instance.CloseCurrent();
                if (ReferenceEquals(UIManager.Instance.Peek(), listPanel)) UIManager.Instance.CloseCurrent();
                bool restored = StackDepth() == depth0;
                p2 = intelVisible && onTop && nameOk && restored;
                Debug.Log($"[2_13_D冒烟DIAG] P2 intelVisible={intelVisible} onTop={onTop} nameOk={nameOk} restored={restored}");
            }
            results.Add($"P2 行点击链路 → 情报面板 Push + 栈顶 + 国名回填 = {p2}");

            // ===== P3 情报面板字段非占位 =====
            bool p3 = false;
            if (intelPanel != null && probeId >= 0)
            {
                intelPanel.ShowKingdom(probeId);
                intelPanel.Open(new Interactor(Faction.PlayerCamp, Vector3.zero));
                var root = intelPanel.GetComponent<UIDocument>().rootVisualElement;
                string name = root.Q<Label>("intel-name")?.text;
                string res = root.Q<Label>("intel-resources")?.text;
                string pop = root.Q<Label>("intel-pop")?.text;
                string mil = root.Q<Label>("intel-military")?.text;
                string tax = root.Q<Label>("intel-tax-factor")?.text;
                p3 = !string.IsNullOrEmpty(name) && name != "未知王国" && name != "—"
                     && !string.IsNullOrEmpty(res) && res != "—"
                     && !string.IsNullOrEmpty(pop) && pop != "—"
                     && !string.IsNullOrEmpty(mil) && mil != "—"
                     && !string.IsNullOrEmpty(tax) && tax != "—";
                Debug.Log($"[2_13_D冒烟DIAG] P3 name={name} res={res} pop={pop} mil={mil} tax={tax}");
                intelPanel.Close();
            }
            results.Add($"P3 情报面板字段非占位（国名/资源/人口/军力/税收系数） = {p3}");

            // ===== P4 幸福三惩罚因子 API 值域 =====
            bool p4 = false;
            if (HappinessSystem.Instance != null && probeId >= 0)
            {
                var hs = HappinessSystem.Instance;
                float growth = hs.GetPopulationGrowthFactor(probeId);
                float morale = hs.GetRetreatThresholdModifier(probeId);
                float tax = hs.GetTaxCoefficient(probeId);
                p4 = growth >= 0f && growth <= 1f && morale >= 0f && morale <= 1f && tax > 0f;
                Debug.Log($"[2_13_D冒烟DIAG] P4 growth={growth:F2} morale={morale:F2} tax={tax:F2}");
            }
            else
            {
                Debug.Log("[2_13_D冒烟DIAG] P4 HappinessSystem 缺席或无可测王国");
            }
            results.Add($"P4 幸福三惩罚因子 API 值域（增长/士气∈[0,1]，税收>0） = {p4}");

            // ===== P5 HUD「列国」入口按钮在场 =====
            bool p5 = false;
            var hud = Object.FindObjectOfType<TopLeftHUD>();
            if (hud != null)
            {
                var hudDoc = hud.GetComponent<UIDocument>();
                var btn = hudDoc != null && hudDoc.rootVisualElement != null
                    ? hudDoc.rootVisualElement.Q<Button>("kingdom-list-button")
                    : null;
                p5 = btn != null;
            }
            results.Add($"P5 HUD「列国」入口按钮在场（TopLeftHUD 实测） = {p5}");

            // ===== P6 FocusOnKingdom 不炸 =====
            bool p6 = false;
            if (listPanel != null && probeId >= 0)
            {
                try
                {
                    listPanel.FocusOnKingdom(probeId);
                    p6 = Object.FindObjectOfType<CameraRig>() != null;
                }
                catch (System.Exception ex)
                {
                    Debug.Log("[2_13_D冒烟DIAG] P6 FocusOn 异常: " + ex.Message);
                    p6 = false;
                }
            }
            results.Add($"P6 FocusOnKingdom 不炸 + CameraRig 在场 = {p6}");
        }
        finally
        {
            // 清栈兜底（防断言失败残留栈污染后续探针）
            if (UIManager.Instance != null)
                while (UIManager.Instance.HasPanelOpen && StackDepth() < 10) UIManager.Instance.CloseCurrent();
        }

        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_13_D冒烟] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_13_D冒烟] ===== {(allPass ? "ALL PASS（P1~P6）" : "HAS FAIL")} =====");
    }

    private static int StackDepth()
    {
        var fi = typeof(UIManager).GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance);
        var stack = fi?.GetValue(UIManager.Instance) as Stack<IUIStackEntry>;
        return stack != null ? stack.Count : -1;
    }
}

/// <summary>冒烟宿主协程载体（跑完自毁）。</summary>
public class DHost : MonoBehaviour
{
    public void Host(IEnumerator routine) => StartCoroutine(Wrap(routine));

    private IEnumerator Wrap(IEnumerator routine)
    {
        yield return routine;
        Destroy(gameObject);
    }
}
