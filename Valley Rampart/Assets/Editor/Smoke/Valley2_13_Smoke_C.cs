using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

// ============================================================================
//  2_13 批C 冒烟（D240 设置面板/D241 倍速/D118 信息面板/11C P1 输入档/M10 选族）
//  用法：菜单「Valley/验证/2_13_批C_流程UI与输入档」——须 GameScene Play（先 Play 再点）。
//  自含断言；UI 真点击/真渲染行为留 Menu 流程 Play 硬性条目（批D，诚实分层）：
//    P1 设置面板 Show/Hide + 音量写入 AudioListener + PlayerPrefs
//    P2 倍速档位：SetGameSpeed(0.5/1/2/3) → CurrentTimeScale 生效 + 还原 1x
//    P3 D118 数据源链路：GuardDeploymentSystem.IsGuarded / LODSystem.GetHeatAt 可调 + HeatLabel 分档
//    P4 控制组：NumberKeyPressedEvent(Ctrl+N) 保存 → 清选 → (N) 调用恢复（真响应）
//    P5 R 训练菜单：ToggleTrainingMenuPressedEvent → BuildingMenuPanel 打开 + _currentTab==Military
//    P6 M10 选族暂存：NewGameConfig.raceId 字段在场（默认 0）+ CharacterCreationPanel 映射逻辑在场（静态）
//    P7 SettingsPanel 场景挂载在场（GameScene 实测 + MainMenuScene 场景文件含 SettingsUI/SettingsPanel 静态）
// ============================================================================
public static class Valley2_13_Smoke_C
{
    [MenuItem("Valley/验证/2_13_批C_流程UI与输入档")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_13_C冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_13_C_SmokeRunner").AddComponent<CHost>().Host(RunCoroutine());
    }

    private static readonly List<GameObject> s_gos = new List<GameObject>();

    public static IEnumerator RunCoroutine()
    {
        yield return null;
        var results = new List<string>();
        float ts0 = Time.timeScale;
        Time.timeScale = 1f;
        try
        {
            // ===== P1 设置面板 =====
            bool p1 = ProbeSettingsPanel();
            results.Add($"P1 设置面板 Show/Hide + 音量写入（AudioListener+PlayerPrefs） = {p1}");

            // ===== P2 倍速档位 =====
            bool p2 = ProbeGameSpeed();
            results.Add($"P2 倍速档位 SetGameSpeed（0.5/1/2/3 → CurrentTimeScale + 还原1x） = {p2}");

            // ===== P3 D118 数据源 =====
            bool p3 = ProbeD118Sources();
            results.Add($"P3 D118 数据源链路（IsGuarded 可调 + GetHeatAt 可调 + HeatLabel 分档） = {p3}");

            // ===== P4 控制组 =====
            bool p4 = false;
            var e4 = ProbeControlGroups(v => p4 = v);
            while (e4.MoveNext()) yield return null;
            results.Add($"P4 控制组（Ctrl+数字保存 → 清选 → 数字调用恢复选中集） = {p4}");

            // ===== P5 R 训练菜单 =====
            bool p5 = ProbeTrainingMenu();
            results.Add($"P5 R 训练菜单（事件 → BuildingMenuPanel 打开 + 军事页） = {p5}");

            // ===== P6 M10 选族暂存 =====
            bool p6 = ProbeRaceId();
            results.Add($"P6 M10 选族暂存（NewGameConfig.raceId 在场+默认0 + 映射逻辑在场） = {p6}");

            // ===== P7 场景挂载 =====
            bool p7 = ProbeSceneMount();
            results.Add($"P7 SettingsPanel 场景挂载（GameScene 实测 + MainMenuScene 静态） = {p7}");
        }
        finally
        {
            Time.timeScale = ts0;
            foreach (var go in s_gos) if (go != null) Object.Destroy(go);
            s_gos.Clear();
        }

        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_13_C冒烟] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_13_C冒烟] ===== {(allPass ? "ALL PASS（P1~P7）" : "HAS FAIL")} =====");
    }

    // ===== P1 =====

    private static bool ProbeSettingsPanel()
    {
        var panel = Object.FindObjectOfType<SettingsPanel>();
        if (panel == null)
        {
            Debug.Log("[2_13_C冒烟DIAG] P1 SettingsPanel 不在场");
            return false;
        }
        var doc = panel.GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return false;
        var root = doc.rootVisualElement;

        // Show → 可见 + 音量写入
        float vol0 = AudioListener.volume;
        panel.Show();
        bool shown = root.style.display == DisplayStyle.Flex;
        var slider = root.Q<Slider>("volume-slider");
        if (slider == null) return false;
        slider.value = 0.7f;    // 模拟用户拖动（触发 RegisterValueChangedCallback）
        bool volApplied = Mathf.Abs(AudioListener.volume - 0.7f) < 0.001f
                       && Mathf.Abs(PlayerPrefs.GetFloat("settings_volume", -1f) - 0.7f) < 0.001f;

        // Hide → 隐藏 + 还原音量
        panel.Hide();
        bool hidden = root.style.display == DisplayStyle.None;
        AudioListener.volume = vol0;
        PlayerPrefs.DeleteKey("settings_volume");
        return shown && volApplied && hidden;
    }

    // ===== P2 =====

    private static bool ProbeGameSpeed()
    {
        if (TimeManager.Instance == null) return false;
        float orig = TimeManager.Instance.CurrentTimeScale;
        bool ok = true;
        foreach (var s in new[] { 0.5f, 2f, 3f })
        {
            TimeManager.Instance.SetGameSpeed(s);
            if (Mathf.Abs(TimeManager.Instance.CurrentTimeScale - s) > 0.001f) { ok = false; break; }
        }
        TimeManager.Instance.SetGameSpeed(1f);   // 还原（sim 对拍锁定 1x 口径）
        bool restored = Mathf.Abs(TimeManager.Instance.CurrentTimeScale - 1f) < 0.001f;
        if (Mathf.Abs(orig - 1f) > 0.001f) TimeManager.Instance.SetGameSpeed(orig);
        return ok && restored;
    }

    // ===== P3 =====

    private static bool ProbeD118Sources()
    {
        // IsGuarded 静态 API 可调（守卫系统未接入区域时= false，风险回退口径）
        bool guardedApi = true;
        try { GuardDeploymentSystem.IsGuarded(new GridCoord(0, 0, 0)); }
        catch (System.Exception e) { Debug.Log($"[2_13_C冒烟DIAG] P3 IsGuarded 异常：{e.Message}"); guardedApi = false; }

        // GetHeatAt 可调（LODSystem 未初始化时返回 0，Ports 契约）
        float heat = LODSystem.Instance != null ? LODSystem.Instance.GetHeatAt(Vector2.zero) : 0f;
        bool heatApi = heat >= 0f;

        // HeatLabel 分档断言（反射私有静态方法）
        var mi = typeof(BuildingPanel).GetMethod("HeatLabel", BindingFlags.NonPublic | BindingFlags.Static);
        bool label = mi != null
            && (string)mi.Invoke(null, new object[] { 0f }) == "无"
            && (string)mi.Invoke(null, new object[] { 0.2f }) == "低"
            && (string)mi.Invoke(null, new object[] { 0.4f }) == "中"
            && (string)mi.Invoke(null, new object[] { 0.7f }) == "高";
        return guardedApi && heatApi && label;
    }

    // ===== P4 =====

    private static IEnumerator ProbeControlGroups(System.Action<bool> done)
    {
        var sel = SelectionController.Instance;
        if (sel == null) { done(false); yield break; }

        // 造两单位入选（Prefab 实例化：问题报告 §八 规约——冒烟单位必须走生产 Prefab；Initialize 保 IsAlive）
        var def = Resources.Load<UnitData>("UnitData/Human_Player_Worker");
        if (def == null || def.prefab == null) { done(false); yield break; }
        var u1 = Object.Instantiate(def.prefab, new Vector3(31f, 31f, 0f), Quaternion.identity);
        var u2 = Object.Instantiate(def.prefab, new Vector3(32f, 31f, 0f), Quaternion.identity);
        s_gos.Add(u1); s_gos.Add(u2);
        var c1 = u1.GetComponent<UnitController>();
        var c2 = u2.GetComponent<UnitController>();
        foreach (var c in new[] { c1, c2 })
        {
            c.kingdomId = 0;
            c.SetFaction(Faction.PlayerCamp);
            c.Initialize(def);      // CurrentHp 注入（未 Initialize 则 IsAlive=false 被控制组过滤）
        }
        yield return null;

        sel.SelectUnit(c1);
        sel.Selected.Add(c2);   // 模拟框选两单位
        yield return null;

        // Ctrl+1 保存 → 清选 → 1 调用
        EventBus.Publish(new NumberKeyPressedEvent(1, true));
        sel.ClearSelection();
        yield return null;
        EventBus.Publish(new NumberKeyPressedEvent(1, false));
        yield return null;
        bool restored = sel.Selected.Contains(c1) && sel.Selected.Contains(c2);
        Debug.Log($"[2_13_C冒烟DIAG] P4 restored={restored} count={sel.Selected.Count}");
        done(restored);
    }

    // ===== P5 =====

    private static bool ProbeTrainingMenu()
    {
        var menu = Object.FindObjectOfType<BuildingMenuPanel>();
        if (menu == null)
        {
            Debug.Log("[2_13_C冒烟DIAG] P5 BuildingMenuPanel 不在场");
            return false;
        }
        var ui = UIManager.Instance;
        bool wasOpen = ui != null && ReferenceEquals(ui.Peek(), menu);
        if (wasOpen) ui.Pop();   // 冒烟前保证关态

        EventBus.Publish(new ToggleTrainingMenuPressedEvent());
        bool opened = ui != null && ReferenceEquals(ui.Peek(), menu);
        var f = typeof(BuildingMenuPanel).GetField("_currentTab", BindingFlags.Instance | BindingFlags.NonPublic);
        var tab = f != null ? f.GetValue(menu) : null;
        bool onMilitary = tab is ModuleType m && m == ModuleType.Military;

        // 收尾：关栈还原
        if (opened && ui != null && ReferenceEquals(ui.Peek(), menu)) ui.Pop();
        Debug.Log($"[2_13_C冒烟DIAG] P5 opened={opened} tab={tab}");
        return opened && onMilitary;
    }

    // ===== P6 =====

    private static bool ProbeRaceId()
    {
        // NewGameConfig.raceId 字段在场 + 默认 0
        var f = typeof(NewGameConfig).GetField("raceId");
        if (f == null) return false;
        var cfg = new NewGameConfig();
        bool defOk = (int)f.GetValue(cfg) == 0;

        // 映射逻辑在场（RaceTextToValue 私有方法反射断言：人类0/精灵1/矮人2/兽人3）
        var mi = typeof(CharacterCreationPanel).GetMethod("RaceTextToValue", BindingFlags.NonPublic | BindingFlags.Instance);
        bool mapOk = mi != null;
        if (mapOk)
        {
            var host = new GameObject("s213c_ccp_host");
            s_gos.Add(host);
            var ccp = host.AddComponent<CharacterCreationPanel>();
            mapOk = (int)mi.Invoke(ccp, new object[] { "人类" }) == 0
                 && (int)mi.Invoke(ccp, new object[] { "精灵" }) == 1
                 && (int)mi.Invoke(ccp, new object[] { "矮人" }) == 2
                 && (int)mi.Invoke(ccp, new object[] { "兽人" }) == 3;
        }
        return defOk && mapOk;
    }

    // ===== P7 =====

    private static bool ProbeSceneMount()
    {
        // GameScene：SettingsPanel 实测在场（P1 已证）；此处静态核对 MainMenuScene 场景文件含 SettingsUI 挂载
        //（场景 YAML MonoBehaviour 引用脚本 guid 非类名 → 查 GameObject 名 m_Name: SettingsUI）
        string mainScene = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,
            "Assets/Scenes/MainMenuScene.unity");
        bool mainMounted = System.IO.File.Exists(mainScene)
            && System.IO.File.ReadAllText(mainScene).Contains("m_Name: SettingsUI");
        return Object.FindObjectOfType<SettingsPanel>() != null && mainMounted;
    }

    private class CHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}
