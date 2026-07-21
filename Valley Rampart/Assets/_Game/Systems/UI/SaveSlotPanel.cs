using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 存档槽选择面板。挂在 SaveSlotsUI GameObject 上。
/// 启动时枚举 3 个槽位，为每个槽创建 SaveSlotItem 卡片并填充操作按钮。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SaveSlotPanel : MonoBehaviour
{
    public static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private VisualElement _slotsList;
    private bool _buttonsBound;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _controller = FindObjectOfType<MainMenuController>();
        _slotsList = root.Q<VisualElement>("slots-list");

        if (!_buttonsBound)
        {
            root.Q<Button>("slots-back-button").clicked += _controller.OnSaveSlotsBackClicked;
            _buttonsBound = true;
        }

        RebuildSlots();
    }

    private void OnDisable()
    {
        // UI Toolkit 的 clicked 是 event，需要 -= 退订，否则反复 SetActive 会重复注册
        if (_buttonsBound && _controller != null)
        {
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                doc.rootVisualElement.Q<Button>("slots-back-button").clicked -= _controller.OnSaveSlotsBackClicked;
            }
            _buttonsBound = false;
        }
    }

    private void RebuildSlots()
    {
        _slotsList.Clear();

        for (int i = 0; i < SlotIds.Length; i++)
        {
            string slotId = SlotIds[i];
            var meta = SaveManager.Instance.GetSaveMeta(slotId);
            var item = new SaveSlotItem(slotId, meta);

            var actions = item.Q<VisualElement>(className: "slot-actions");
            if (actions != null)
            {
                // 继续按钮
                var continueBtn = new Button(() => _controller.OnSaveSlotSelected(slotId));
                continueBtn.text = meta != null ? "继续" : "新建到此处";
                continueBtn.AddToClassList("slot-button");
                if (meta != null && !meta.isFinished) continueBtn.AddToClassList("slot-button--primary");
                continueBtn.SetEnabled(meta != null && !meta.isFinished);
                actions.Add(continueBtn);

                // 删除按钮
                var deleteBtn = new Button(() => OnDeleteClicked(slotId));
                deleteBtn.text = "删除";
                deleteBtn.AddToClassList("slot-button");
                deleteBtn.AddToClassList("slot-button--delete");
                deleteBtn.SetEnabled(meta != null);
                actions.Add(deleteBtn);
            }

            _slotsList.Add(item);
        }
    }

    private void OnDeleteClicked(string slotId)
    {
        SaveManager.Instance.Delete(slotId);
        RebuildSlots();
    }
}
