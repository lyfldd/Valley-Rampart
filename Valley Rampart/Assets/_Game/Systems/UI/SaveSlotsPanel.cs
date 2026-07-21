using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 存档槽面板 UI 绑定。挂在 SaveSlotsUI GameObject 上。
/// 启动时枚举 3 个槽位，渲染为卡片列表。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SaveSlotsPanel : MonoBehaviour
{
    private static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private VisualElement _slotsList;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _controller = FindObjectOfType<MainMenuController>();
        _slotsList = root.Q<VisualElement>("slots-list");

        root.Q<Button>("slots-back-button").clicked += _controller.OnSaveSlotsBackClicked;

        RebuildSlots();
    }

    private void RebuildSlots()
    {
        _slotsList.Clear();

        for (int i = 0; i < SlotIds.Length; i++)
        {
            string slotId = SlotIds[i];
            _slotsList.Add(BuildSlotCard(slotId, i + 1));
        }
    }

    private VisualElement BuildSlotCard(string slotId, int displayIndex)
    {
        var meta = SaveManager.Instance.GetSaveMeta(slotId);
        bool hasSave = meta != null;
        bool isFinished = hasSave && meta.isFinished;

        var card = new VisualElement();
        card.AddToClassList("slot-card");
        if (!hasSave) card.AddToClassList("slot-card--empty");
        if (isFinished) card.AddToClassList("slot-card--finished");

        // 左：信息
        var info = new VisualElement();
        info.AddToClassList("slot-info");

        var titleLabel = new Label($"存档槽 {displayIndex}");
        titleLabel.AddToClassList("slot-title");

        var metaLabel = new Label();
        metaLabel.AddToClassList("slot-meta");
        if (hasSave)
        {
            metaLabel.text = $"{meta.summary.rulerName} · 第{meta.summary.currentDay}天 · {meta.saveTime}";
        }
        else
        {
            metaLabel.text = "空槽位";
        }

        info.Add(titleLabel);
        info.Add(metaLabel);
        card.Add(info);

        // 右：状态 + 操作
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        if (hasSave)
        {
            var status = new Label(isFinished ? "已结束" : "");
            status.AddToClassList("slot-status");
            if (isFinished) status.AddToClassList("slot-status--finished");
            actions.Add(status);
        }

        // 继续按钮
        var continueBtn = new Button(() => _controller.OnSaveSlotSelected(slotId));
        continueBtn.text = hasSave ? "继续" : "新建到此处";
        continueBtn.AddToClassList("slot-button");
        if (hasSave && !isFinished) continueBtn.AddToClassList("slot-button--primary");
        continueBtn.SetEnabled(hasSave && !isFinished);
        actions.Add(continueBtn);

        // 删除按钮
        var deleteBtn = new Button(() => OnDeleteClicked(slotId));
        deleteBtn.text = "删除";
        deleteBtn.AddToClassList("slot-button");
        deleteBtn.AddToClassList("slot-button--delete");
        deleteBtn.SetEnabled(hasSave);
        actions.Add(deleteBtn);

        card.Add(actions);
        return card;
    }

    private void OnDeleteClicked(string slotId)
    {
        // 注：打包后 UnityEditor 不可用，运行时直接删除并打 log。
        // 后续做正式确认弹窗时再换成自定义的 in-game dialog。
        Debug.Log($"[SaveSlotsPanel] 删除存档 {slotId}");
        SaveManager.Instance.Delete(slotId);
        RebuildSlots();
    }
}
