using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 睁眼开场（第一章：开头漫画结束后接续，全项目只用这一次）。
///
/// 层级设计（衔接的关键）：
///   本黑幕放在独立 Canvas（Sorting Order 15），压在所有玩法面板
///   （Task/Tip/Setting ≤10）之上、漫画 Plot Canvas（20）之下。
///   漫画收尾的黑屏溶解播完、Plot Canvas 关闭时，露出的就是这张
///   全黑幕——衔接两边都是纯黑，无跳变，也不用改 ComicIntroController。
///
/// 流程：
///   等 ComicIntroController 打开再关闭（IsOpen true → false）
///   → 锁定全部操作 → 全黑停留 → 按 Stages 逐段把"睁眼度"
///   推到目标值（默认：微睁→闭→半睁→闭→全睁→整屏揭开）
///   → 整屏揭开后黑幕已完全透明，此时关闭射线遮挡、隐藏黑幕、
///   解锁——隐藏与画面无跳变。
///
/// 睁眼形状由 Assets/Shaders/UI Eye Open.shader 计算，眼宽/眼高/
/// 眼角尖锐度/上下睑比例/边缘噪声全部在材质上调；本组件只写
/// _EyeOpen（睁眼进度）与 _Aspect、_NoiseTime（每帧），
/// 写的是材质实例，不污染材质资产。时间全部走未缩放时间。
///
/// 锁定沿用 DialogueRunner 的做法：禁用 PlayerController 及其
/// 武器/拾取/后座组件（只禁用原本启用的，结束原样恢复），
/// 光标保持锁定；黑幕 Image 的 Raycast Target 常开，期间挡住
/// 全部 UI 点击，结束后关闭。
/// </summary>
public class EyeOpenIntroController : MonoBehaviour
{
    // 黑幕材质必须使用的 Shader 名（配错时明确报错而不是悄悄不生效）
    private const string ExpectedShaderName = "UI Eye Open";

    // Shader 属性 ID（属性见 Assets/Shaders/UI Eye Open.shader）
    private static readonly int EyeOpenId = Shader.PropertyToID("_EyeOpen");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");

    /// <summary>睁眼的一段动作：把睁眼度从当前值过渡到目标值。</summary>
    [System.Serializable]
    public class EyeStage
    {
        [Tooltip("本段时长（秒，未缩放时间）")]
        [Min(0.01f)]
        public float duration = 0.3f;

        [Tooltip("本段结束时的睁眼度：0 全闭、1 眼睛全睁（眼型最大）、2 整屏揭开（眼睑退出画面，随后隐藏黑幕无跳变）")]
        [Range(0f, 2f)]
        public float openTo = 0.5f;
    }

    [Header("引用")]

    [Tooltip("全屏黑幕 Image（材质 = UI Eye Open、颜色黑、Raycast Target 开启）；留空取本物体上的")]
    [SerializeField]
    private Image eyeImage;

    [Tooltip("玩家控制器：锁定期间禁用它与武器/拾取/后座；留空自动查找")]
    [SerializeField]
    private PlayerController playerController;

    [Tooltip("睁眼期间及第一段对话结束前隐藏的 HUD Canvas；留空则不隐藏")]
    [SerializeField]
    private GameObject hudCanvas;

    [Header("时序（秒，未缩放时间）")]

    [Tooltip("漫画关闭、锁定之后的全黑停留时长")]
    [SerializeField, Min(0f)]
    private float holdClosedTime = 0.5f;

    [Tooltip("睁眼动作序列：逐段把睁眼度推到目标值（0 全闭 / 1 全睁 / 2 整屏揭开）。默认慢醒节奏：微睁→闭→半睁→闭→全睁→整屏揭开；可增删改段")]
    [SerializeField]
    private List<EyeStage> stages = new List<EyeStage>
    {
        new EyeStage { duration = 0.35f, openTo = 0.25f },
        new EyeStage { duration = 0.18f, openTo = 0f },
        new EyeStage { duration = 0.40f, openTo = 0.5f },
        new EyeStage { duration = 0.20f, openTo = 0f },
        new EyeStage { duration = 0.70f, openTo = 1f },
        new EyeStage { duration = 0.70f, openTo = 2f },
    };

    [Tooltip("每段内部的缓动曲线（横轴归一化时间、纵轴进度）")]
    [SerializeField]
    private AnimationCurve easing =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // 运行时材质实例：睁眼进度只写实例，退出播放不污染材质资产
    private Material eyeMaterial;

    // 实例复制前的原材质（资产），销毁实例时还回去
    private Material sourceMaterial;

    // 锁定期间被本组件禁用的组件，解锁时原样恢复
    private readonly List<Behaviour> lockedBehaviours =
        new List<Behaviour>();

    // 第一段对话结束前被禁用的 ESC 响应组件（设置面板等）
    private readonly List<Behaviour> escBlockingBehaviours =
        new List<Behaviour>();

    // HUD 原始显隐状态
    private bool hudWasActive;
    private bool hasHudState;

    // 是否已订阅 DialogueRunner.DialogueEnded
    private bool listeningDialogueEnded;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        // 黑幕从第一帧起就挡点击（漫画画布层级更高，期间点击
        // 仍由漫画接收，互不干扰；漫画关闭后由本黑幕接管拦截）
        eyeImage.raycastTarget = true;

        sourceMaterial = eyeImage.material;
        eyeMaterial = new Material(sourceMaterial);
        eyeImage.material = eyeMaterial;

        // 起始：完全闭合（全黑）
        eyeMaterial.SetFloat(EyeOpenId, 0f);
    }

    private void Start()
    {
        StartCoroutine(RunRoutine());
    }

    private void Update()
    {
        // 睝缘噪声随未缩放时间滚动；宽高比每帧同步，
        // 窗口尺寸变化后眼型不被拉扁
        eyeMaterial.SetFloat(NoiseTimeId, Time.unscaledTime);

        Rect rect = eyeImage.rectTransform.rect;

        if (rect.height > 0f)
        {
            eyeMaterial.SetFloat(AspectId, rect.width / rect.height);
        }
    }

    // 主流程：等漫画 → 锁定 → 全黑停留 → 睁眼 → 收尾解锁
    private IEnumerator RunRoutine()
    {
        // 漫画开启前黑幕就已全黑压在下方（Plot Canvas 20 > 15），
        // 无需提前干预；等它开、再等它收尾关闭
        yield return new WaitUntil(() => ComicIntroController.IsOpen);
        yield return new WaitUntil(() => !ComicIntroController.IsOpen);

        // 关闭那一刻起锁一切：光标保持漫画收尾时的锁定态，
        // 组件禁用挡住移动/武器/拾取，Raycast 挡住 UI 点击
        LockControls();

        if (holdClosedTime > 0f)
        {
            yield return new WaitForSecondsRealtime(holdClosedTime);
        }

        float current = 0f;

        if (stages != null)
        {
            foreach (EyeStage stage in stages)
            {
                if (stage == null)
                {
                    continue;
                }

                yield return StartCoroutine(
                    TransitionRoutine(current, stage)
                );

                current = stage.openTo;
            }
        }

        // 全睁收尾：放开点击、隐藏黑幕
        eyeImage.raycastTarget = false;

        // 隐藏黑幕本体（脚本与 Image 同物体时即隐藏自身）
        eyeImage.gameObject.SetActive(false);

        // 睁眼结束：恢复玩家控制组件，让独白触发器能正常检测并触发；
        // 但 HUD 继续隐藏、ESC 继续拦截，直到第一段对话结束。
        UnlockControls();

        // 监听第一段对话结束事件。注意必须用 DialogueEnded 事件
        // 而不是对话的 endFlag：endFlag 在 EndDialogue 开头立即置位，
        // 此时 RestoreGameplay（会把 HUD 恢复为对话前的隐藏状态）
        // 还没执行；若在 flag 回调里显示 HUD，会被随后的
        // RestoreGameplay 覆盖为隐藏。DialogueEnded 在
        // RestoreGameplay 之后触发，此时设 HUD 为最终值。
        if (!listeningDialogueEnded && DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.DialogueEnded +=
                OnFirstDialogueEnded;
            listeningDialogueEnded = true;
        }
    }

    // 第一段对话结束：恢复 HUD 与 ESC。
    private void OnFirstDialogueEnded()
    {
        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.DialogueEnded -=
                OnFirstDialogueEnded;
        }

        listeningDialogueEnded = false;
        RestoreHudAndEsc();
    }

    // 单段过渡：睁眼度 from → stage.openTo，按缓动曲线推进，
    // 全程未缩放时间（timeScale = 0 也照走）
    private IEnumerator TransitionRoutine(float from, EyeStage stage)
    {
        float time = 0f;

        while (time < stage.duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / stage.duration);

            float eased =
                easing != null && easing.length > 0
                    ? easing.Evaluate(t)
                    : t;

            eyeMaterial.SetFloat(
                EyeOpenId,
                Mathf.Lerp(from, stage.openTo, eased)
            );

            yield return null;
        }

        eyeMaterial.SetFloat(EyeOpenId, stage.openTo);
    }

    // 锁定（沿用 DialogueRunner.LockGameplay 的禁用清单）：
    // 只记录并禁用"当前启用"的组件，解锁时原样恢复，不误伤
    // 本来就关着的（比如剧情/其他系统禁用的）
    private void LockControls()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        lockedBehaviours.Clear();

        if (playerController != null)
        {
            AddLock(playerController);

            Transform controlRoot = playerController.transform;

            foreach (
                WeaponController controller in
                controlRoot.GetComponentsInChildren<WeaponController>(true)
            )
            {
                AddLock(controller);
            }

            foreach (
                WeaponEffects effects in
                controlRoot.GetComponentsInChildren<WeaponEffects>(true)
            )
            {
                AddLock(effects);
            }

            foreach (
                PickupController pickup in
                controlRoot.GetComponentsInChildren<PickupController>(true)
            )
            {
                AddLock(pickup);
            }

            AddLock(controlRoot.GetComponent<CameraRecoil>());
        }

        foreach (Behaviour behaviour in lockedBehaviours)
        {
            behaviour.enabled = false;
        }

        // 隐藏 HUD：睁眼期间不显示任何玩法界面。
        if (hudCanvas != null)
        {
            hasHudState = true;
            hudWasActive = hudCanvas.activeSelf;
            hudCanvas.SetActive(false);
        }
        else
        {
            hasHudState = false;
        }

        // 禁用 ESC 响应组件，睁眼到第一段对话结束前不允许打开设置等界面。
        DisableEscBlockingBehaviours();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockControls()
    {
        foreach (Behaviour behaviour in lockedBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        lockedBehaviours.Clear();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 第一段对话结束后恢复 HUD 与 ESC 响应组件。
    private void RestoreHudAndEsc()
    {
        if (hasHudState && hudCanvas != null)
        {
            hudCanvas.SetActive(hudWasActive);
            hasHudState = false;
        }

        foreach (Behaviour behaviour in escBlockingBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        escBlockingBehaviours.Clear();
    }

    private void DisableEscBlockingBehaviours()
    {
        escBlockingBehaviours.Clear();

        AddEscBlock(FindObjectOfType<SettingsUIRoot>());
        AddEscBlock(FindObjectOfType<TaskPanelController>());
        AddEscBlock(FindObjectOfType<InventoryInput>());
        AddEscBlock(FindObjectOfType<TipPanelController>());

        foreach (Behaviour behaviour in escBlockingBehaviours)
        {
            behaviour.enabled = false;
        }
    }

    private void AddEscBlock(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled)
        {
            escBlockingBehaviours.Add(behaviour);
        }
    }

    private void AddLock(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled)
        {
            lockedBehaviours.Add(behaviour);
        }
    }

    // 引用与配置校验：缺项时给出明确报错并返回 false（Awake 关闭本组件）
    private bool ValidateReferences()
    {
        if (eyeImage == null)
        {
            eyeImage = GetComponent<Image>();
        }

        if (eyeImage == null)
        {
            Debug.LogError(
                "EyeOpenIntroController：找不到黑幕 Image（Eye Image 未指定，本物体上也没有）。",
                this
            );
            return false;
        }

        Material material = eyeImage.material;

        if (material == null || material.shader == null)
        {
            Debug.LogError(
                "EyeOpenIntroController：黑幕 Image 未指定材质（或材质丢失），无法驱动睁眼。",
                this
            );
            return false;
        }

        if (material.shader.name != ExpectedShaderName)
        {
            Debug.LogError(
                "EyeOpenIntroController：黑幕材质的 Shader 是 '" +
                material.shader.name + "'，应为 '" + ExpectedShaderName +
                "'（Assets/Shaders/UI Eye Open.shader）。",
                this
            );
            return false;
        }

        if (playerController == null &&
            FindObjectOfType<PlayerController>() == null)
        {
            Debug.LogWarning(
                "EyeOpenIntroController：场景里没有 PlayerController，" +
                "睁眼期间无法锁定移动（UI 点击仍由黑幕拦截）。",
                this
            );
        }

        return true;
    }

    private void OnDestroy()
    {
        if (listeningDialogueEnded && DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.DialogueEnded -=
                OnFirstDialogueEnded;
            listeningDialogueEnded = false;
        }

        if (eyeImage != null && sourceMaterial != null)
        {
            eyeImage.material = sourceMaterial;
        }

        if (eyeMaterial != null)
        {
            Destroy(eyeMaterial);
        }
    }
}
