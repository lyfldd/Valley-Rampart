using UnityEngine.UIElements;

/// <summary>
/// 单个存档槽 UI 组件。从 GetSaveMeta 读到的数据展示成卡片。
/// 由 SaveSlotPanel 创建，挂在 slots-list 下。
/// </summary>
public class SaveSlotItem : VisualElement
{
    public string SlotId { get; }
    public bool HasSave { get; }
    public bool IsFinished { get; }

    public SaveSlotItem(string slotId, GameSaveRoot meta)
    {
        SlotId = slotId;
        HasSave = meta != null;
        IsFinished = HasSave && meta.isFinished;

        AddToClassList("slot-card");
        if (!HasSave) AddToClassList("slot-card--empty");
        if (IsFinished) AddToClassList("slot-card--finished");

        // 左：信息
        var info = new VisualElement();
        info.AddToClassList("slot-info");

        var titleLabel = new Label($"存档槽 {ExtractIndex(slotId)}");
        titleLabel.AddToClassList("slot-title");

        var metaLabel = new Label();
        metaLabel.AddToClassList("slot-meta");
        if (HasSave)
        {
            string rulerName = string.IsNullOrEmpty(meta.summary?.rulerName) ? "未知君主" : meta.summary.rulerName;
            int day = meta.summary?.currentDay ?? 0;
            metaLabel.text = $"{rulerName} · 第{day}天 · {meta.saveTime}";
        }
        else
        {
            metaLabel.text = "空槽位";
        }

        info.Add(titleLabel);
        info.Add(metaLabel);
        Add(info);

        // 右：状态 + 操作
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        if (HasSave)
        {
            var status = new Label(IsFinished ? "已结束" : "");
            status.AddToClassList("slot-status");
            if (IsFinished) status.AddToClassList("slot-status--finished");
            actions.Add(status);
        }
        else
        {
            // 空槽位：放置一个占位以保持布局
            var placeholder = new Label("可用");
            placeholder.AddToClassList("slot-status");
            actions.Add(placeholder);
        }

        Add(actions);
    }

    public void AddActionButton(string text, System.Action onClick, string extraClass = null, bool primary = false)
    {
        var actions = this.Q<VisualElement>(className: "slot-actions");
        if (actions == null) return;

        var btn = new Button(onClick);
        btn.text = text;
        btn.AddToClassList("slot-button");
        if (primary) btn.AddToClassList("slot-button--primary");
        if (extraClass != null) btn.AddToClassList(extraClass);
        actions.Add(btn);
        return;
    }

    public Button GetLastButton()
    {
        var actions = this.Q<VisualElement>(className: "slot-actions");
        if (actions == null || actions.childCount == 0) return null;
        return actions[actions.childCount - 1] as Button;
    }

    private static int ExtractIndex(string slotId)
    {
        // slot_1 -> 1, slot_2 -> 2 ...
        int idx = slotId.LastIndexOf('_');
        if (idx < 0 || idx + 1 >= slotId.Length) return 0;
        int.TryParse(slotId.Substring(idx + 1), out int result);
        return result;
    }
}
