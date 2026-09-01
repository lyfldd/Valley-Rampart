using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 设置面板（2_13 步骤5 / D240，D256 归本篇）。跨场景独立面板（MainMenuScene/GameScene 各挂一个 SettingsUI GameObject）。
/// 音量（AudioListener.volume，PlayerPrefs 持久）/ 语言（D240 占位：i18n 未建，仅存偏好——让渡登记）/
/// 倍速（D240/D241：0.5x/1x/2x/3x → TimeManager.SetGameSpeed；sim 对拍锁定 1x）/ 快捷键清单（只读）。
/// 打开方：MainMenuPanel settings-button / PausePanel settings-button。关闭：关闭按钮。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SettingsPanel : MonoBehaviour
{
    // PlayerPrefs 键（用户偏好=非游戏数值，不入 SO/存档）
    private const string KeyVolume = "settings_volume";
    private const string KeyLanguage = "settings_language";

    private bool _bound;
    private Slider _volumeSlider;
    private DropdownField _languageSelect;
    private readonly Button[] _speedButtons = new Button[4];    // 0.5x/1x/2x/3x
    private static readonly float[] SpeedValues = { 0.5f, 1f, 2f, 3f };

    private void OnEnable()
    {
        if (!_bound) Bind();
        SetVisible(false);
        // 恢复持久化偏好
        if (_volumeSlider != null)
            _volumeSlider.value = PlayerPrefs.HasKey(KeyVolume) ? PlayerPrefs.GetFloat(KeyVolume) : 1f;
        if (_languageSelect != null)
            _languageSelect.value = PlayerPrefs.HasKey(KeyLanguage) ? PlayerPrefs.GetString(KeyLanguage) : "简体中文";
    }

    private void Bind()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _volumeSlider = root.Q<Slider>("volume-slider");
        _languageSelect = root.Q<DropdownField>("language-select");
        _speedButtons[0] = root.Q<Button>("speed-05");
        _speedButtons[1] = root.Q<Button>("speed-10");
        _speedButtons[2] = root.Q<Button>("speed-20");
        _speedButtons[3] = root.Q<Button>("speed-30");
        var close = root.Q<Button>("settings-close-button");

        if (_volumeSlider != null) _volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);
        if (_languageSelect != null) _languageSelect.RegisterValueChangedCallback(OnLanguageChanged);
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            int idx = i;    // 闭包捕获
            if (_speedButtons[i] != null) _speedButtons[i].clicked += () => OnSpeedClicked(idx);
        }
        if (close != null) close.clicked += Hide;

        _bound = true;
    }

    // ===== 对外 API =====

    /// <summary>显示设置面板（MainMenuPanel / PausePanel 调用）。</summary>
    public void Show()
    {
        SetVisible(true);
        RefreshSpeedHighlight();
    }

    /// <summary>隐藏设置面板。</summary>
    public void Hide() => SetVisible(false);

    // ===== 回调 =====

    private void OnVolumeChanged(ChangeEvent<float> evt)
    {
        AudioListener.volume = evt.newValue;
        PlayerPrefs.SetFloat(KeyVolume, evt.newValue);
    }

    private void OnLanguageChanged(ChangeEvent<string> evt)
    {
        // D240 占位：i18n 体系未建，仅持久化偏好（让渡登记，HH.46）
        PlayerPrefs.SetString(KeyLanguage, evt.newValue);
        Debug.Log($"[SettingsPanel] 语言偏好已保存：{evt.newValue}（i18n 未建，界面文案暂不切换）");
    }

    private void OnSpeedClicked(int idx)
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.SetGameSpeed(SpeedValues[idx]);
        RefreshSpeedHighlight();
        Debug.Log($"[SettingsPanel] 倍速切换：{SpeedValues[idx]}x");
    }

    /// <summary>倍速按钮高亮当前档（TimeManager.CurrentTimeScale 最近档）。</summary>
    private void RefreshSpeedHighlight()
    {
        float cur = TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeScale : 1f;
        int best = 1;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < SpeedValues.Length; i++)
        {
            float d = Mathf.Abs(SpeedValues[i] - cur);
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            if (_speedButtons[i] == null) continue;
            if (i == best) _speedButtons[i].AddToClassList("speed-button--active");
            else _speedButtons[i].RemoveFromClassList("speed-button--active");
        }
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
