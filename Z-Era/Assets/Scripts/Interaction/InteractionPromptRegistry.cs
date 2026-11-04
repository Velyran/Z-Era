using System.Collections.Generic;
using TMPro;

/// <summary>
/// 交互提示文本占用登记：NPC 对话、门等多个交互组件可能共用同一个
/// DialoguePromptText（HUD 上只有一个），而每个组件每帧都可能走到
/// "清空提示"分支，不加占用登记就会把同一帧里别人刚写好的提示擦掉，
/// 表现为"提示永远不显示"。
///
/// 规则：谁最后写入提示谁占用；只有占用者可以清空。
/// </summary>
public static class InteractionPromptRegistry
{
    // 每个提示文本当前的占用者（交互组件实例）。
    private static readonly Dictionary<TMP_Text, object> owners =
        new Dictionary<TMP_Text, object>();

    // 指定文本当前是否由 owner 占用。
    public static bool IsOwner(TMP_Text text, object owner)
    {
        return text != null &&
               owners.TryGetValue(text, out object current) &&
               current == owner;
    }

    // 声明占用（写入提示前调用）。
    public static void Acquire(TMP_Text text, object owner)
    {
        if (text == null)
        {
            return;
        }

        owners[text] = owner;
    }

    // 归还占用并清空文本，仅在确实由 owner 占用时生效，
    // 避免误清别人的提示。
    public static void Release(TMP_Text text, object owner)
    {
        if (!IsOwner(text, owner))
        {
            return;
        }

        owners.Remove(text);
        text.text = string.Empty;
    }
}
