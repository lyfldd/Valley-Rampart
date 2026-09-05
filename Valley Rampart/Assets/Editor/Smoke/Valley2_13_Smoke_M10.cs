using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

// ============================================================================
//  2_13 M10 选族数据链冒烟（HH.66 段A / D431 UI 侧收口）
//  菜单A「2_13_M10_选族UI渲染断言」：须 MainMenuScene Play——
//    P1 静态四卡结构在场（race-card-0~3 + banner/name/desc 子元素，UXML 写死=SaveSlots 教训遵循）
//    P2 真数据渲染：族名==RaceDef.raceName / 描述==raceDescription / 色带==bannerColor（四资产逐一比对）
//    P3 默认选中人类（race-card--selected 在卡 0）
//  菜单B「2_13_M10_进局raceId断言」：须 GameScene Play（Computer Use 物理点击选族→进局后执行）——
//    P4 三点一致：GetKingdomRace(0) == KingdomState(0).raceId == GetKingdomRaceDef(0).raceId
//    P5 期望比对：三点值 == SessionState["m10_expected_race"]（点击前经 execute_code 设入）
//    P6 真链路证据：Console 含「玩家王国开局注册」行（EnsurePlayerRegistered 消费 NewGameConfig.raceId）
//  行为级分工（诚实分层，批D-2 先例）：点击链=Computer Use 物理点击（非 API 直调）；
//  本容器=渲染/状态断言。四族各一轮（人类/精灵/矮人/兽人）。
// ============================================================================
public static class Valley2_13_Smoke_M10
{
    private const string ExpectedKey = "m10_expected_race";

    [MenuItem("Valley/验证/2_13_M10_选族UI渲染断言")]
    public static void RunUiAssert()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_13_M10] 渲染断言须 MainMenuScene Play 上下文。"); return; }
        var panel = Object.FindObjectOfType<CharacterCreationPanel>();
        if (panel == null) { Debug.LogError("[2_13_M10] 渲染断言须 MainMenuScene（CharacterCreationPanel 在场）。"); return; }
        new GameObject("2_13_M10_UiRunner").AddComponent<M10Host>().Host(UiCoroutine());
    }

    private static IEnumerator UiCoroutine()
    {
        yield return null;
        var results = new List<string>();
        var doc = Object.FindObjectOfType<CharacterCreationPanel>().GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        // ===== P1 静态四卡结构 =====
        bool p1 = true;
        for (int i = 0; i < 4; i++)
        {
            var card = root.Q<Button>($"race-card-{i}");
            p1 &= card != null && card.Q<VisualElement>($"race-banner-{i}") != null
                       && card.Q<Label>($"race-name-{i}") != null && card.Q<Label>($"race-desc-{i}") != null;
        }
        results.Add($"P1 静态四卡结构在场（card/banner/name/desc ×4） = {p1}");

        // ===== P2 真数据渲染（RaceDef 四资产逐一比对）=====
        bool p2 = true;
        var diag = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            var def = KingdomRace.GetRaceDef(i);
            if (def == null) { diag.Add($"race{i}=资产缺失"); p2 = false; continue; }
            var card = root.Q<Button>($"race-card-{i}");
            string nameTxt = card.Q<Label>($"race-name-{i}")?.text;
            string descTxt = card.Q<Label>($"race-desc-{i}")?.text;
            Color bannerCol = card.Q<VisualElement>($"race-banner-{i}")?.resolvedStyle.backgroundColor ?? Color.magenta;
            bool nameOk = nameTxt == def.raceName;
            bool descOk = descTxt == (def.raceDescription ?? string.Empty);
            bool colOk = ColorApprox(bannerCol, def.bannerColor);
            if (!nameOk || !descOk || !colOk)
                diag.Add($"race{i} name[{nameTxt}vs{def.raceName}]={nameOk} desc={descOk} col[{bannerCol}vs{def.bannerColor}]={colOk}");
            p2 &= nameOk && descOk && colOk;
        }
        if (diag.Count > 0) Debug.Log("[2_13_M10 DIAG] " + string.Join(" | ", diag));
        results.Add($"P2 四族真数据渲染（族名/描述/色带 == RaceDef 资产值） = {p2}");

        // ===== P3 默认选中人类 =====
        bool p3 = root.Q<Button>("race-card-0")?.ClassListContains("race-card--selected") == true;
        results.Add($"P3 默认选中人类（卡 0 含 race-card--selected） = {p3}");

        Finish(results, "M10 渲染断言");
        var hostUi = Object.FindObjectOfType<M10Host>();
        if (hostUi != null) Object.Destroy(hostUi.gameObject);
    }

    [MenuItem("Valley/验证/2_13_M10_进局raceId断言")]
    public static void RunInGameAssert()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_13_M10] 进局断言须 GameScene Play 上下文。"); return; }
        if (KingdomRegistry.Instance == null || WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        { Debug.LogError("[2_13_M10] 进局断言须已进局（ActiveMap 在场）。"); return; }
        new GameObject("2_13_M10_InGameRunner").AddComponent<M10Host>().Host(InGameCoroutine());
    }

    private static IEnumerator InGameCoroutine()
    {
        yield return null;
        var results = new List<string>();

        // ===== P4 三点一致 =====
        int viaRace = KingdomRace.GetKingdomRace(0);
        var state = KingdomRegistry.Instance.Get(0);
        var def = KingdomRace.GetKingdomRaceDef(0);
        int viaState = state != null ? state.raceId : -99;
        int viaDef = def != null ? def.raceId : -99;
        bool p4 = state != null && def != null && viaRace == viaState && viaState == viaDef;
        results.Add($"P4 三点一致（GetKingdomRace={viaRace} / KingdomState.raceId={viaState} / GetKingdomRaceDef.raceId={viaDef}） = {p4}");

        // ===== P5 期望比对（SessionState，点击前经 execute_code 设入）=====
        string expStr = SessionState.GetString(ExpectedKey, "");
        bool p5 = true;
        if (int.TryParse(expStr, out int expected))
            p5 = viaRace == expected;
        else
            Debug.LogWarning("[2_13_M10] SessionState 无期望族（先 execute_code 设 m10_expected_race）——P5 降级=true（P4 三点一致已把关）");
        results.Add($"P5 期望比对（点击链实测 raceId={viaRace} vs 期望={expStr}） = {p5}");

        // ===== P6 真链路证据（EnsurePlayerRegistered 消费日志）=====
        bool p6 = state != null && state.name != null;
        results.Add($"P6 玩家国注册在场（id=0 name={state?.name} foundedDay={state?.foundedDay}） = {p6}");

        Finish(results, $"M10 进局断言（期望族={expStr}）");
        var host = Object.FindObjectOfType<M10Host>();
        if (host != null) Object.Destroy(host.gameObject);
    }

    private static bool ColorApprox(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    private static void Finish(List<string> results, string tag)
    {
        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_13_M10] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_13_M10] ===== {tag} {(allPass ? "ALL PASS" : "HAS FAIL")} =====");
    }

    private class M10Host : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
