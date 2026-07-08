using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class Watch : MonoBehaviour
{
    [SerializeField, Interface(typeof(IHand))]
    private UnityEngine.Object _hand;

    public DataPanelUI dataPanelUI;
    public ToolPanelUI toolPanelUI;
    public GeminiClient geminiClient;

    [UnityEngine.Serialization.FormerlySerializedAs("liftDistance")]
    [UnityEngine.Serialization.FormerlySerializedAs("heightAboveHand")]
    public float yOffsetFromHand = 0.03f;

    public bool stackAcrossForearm = true;

    public TMP_FontAsset bodyFont;

    private const float HandPlacement = 0.6f;
    private const HandJointId AlongJoint = HandJointId.HandMiddle1;
    private const HandJointId IndexJoint = HandJointId.HandIndex1;
    private const HandJointId PinkyJoint = HandJointId.HandPinky1;

    private static readonly Color GeminiInactiveBg = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color GeminiActiveBg = MetaTokens.Alpha(MetaTokens.Blue, 0.30f);
    private static readonly Color GeminiInactiveText = MetaTokens.NeutralC0;
    private static readonly Color GeminiActiveText = MetaTokens.BlueLight;

    private IHand _ihand;
    private Canvas _canvas;
    private Transform _canvasTransform;
    private Button _dataButton;
    private Button _toolButton;
    private Button _geminiButton;
    private TextMeshProUGUI _geminiText;
    private PointerHighlight _geminiHighlight;
    private bool _geminiActive;

    private void CoverButtonBacks()
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name != "ButtonBack") continue;
            rects[i].offsetMin = Vector2.zero;
            rects[i].offsetMax = Vector2.zero;
        }
    }

    private void Awake()
    {
        _ihand = _hand as IHand;
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas != null)
        {
            _canvasTransform = _canvas.transform;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 5;
        }

        BindButtons();
        CoverButtonBacks();
        ApplyFonts();
        SetGeminiVisual(_geminiActive);

        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void ApplyFonts()
    {
        if (bodyFont == null) return;

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++) texts[i].font = bodyFont;
    }

    private void BindButtons()
    {
        Transform dataT = UITransformSearch.FindDeep(transform, "Data Panel_Btn");
        Transform toolT = UITransformSearch.FindDeep(transform, "Tool Panel_Btn");

        if (dataT != null)
        {
            _dataButton = dataT.GetComponent<Button>();
            if (_dataButton != null)
                _dataButton.onClick.AddListener(OnDataClicked);
            ApplyFactoryButtonStyle(dataT);
            PointerHighlight.AttachButtonFeedback(dataT, MetaTokens.BlueLight);
            StripHorizontalTextPadding(dataT);
            HintTrigger.Attach(dataT.gameObject, "Data Panel",
                "This button opens the Data Panel.");
        }

        if (toolT != null)
        {
            _toolButton = toolT.GetComponent<Button>();
            if (_toolButton != null)
                _toolButton.onClick.AddListener(OnToolClicked);
            ApplyFactoryButtonStyle(toolT);
            PointerHighlight.AttachButtonFeedback(toolT, MetaTokens.BlueLight);
            StripHorizontalTextPadding(toolT);
            HintTrigger.Attach(toolT.gameObject, "Tool Panel",
                "This button opens the Tool Panel.");
        }

        Transform geminiT = UITransformSearch.FindDeep(transform, "Gemini_Btn");
        if (geminiT != null)
        {
            _geminiButton = geminiT.GetComponent<Button>();
            if (_geminiButton != null)
                _geminiButton.onClick.AddListener(OnGeminiClicked);
            Transform textT = geminiT.Find("Text");
            if (textT != null) _geminiText = textT.GetComponent<TextMeshProUGUI>();
            ApplyFactoryButtonStyle(geminiT);
            _geminiHighlight = PointerHighlight.AttachButtonFeedback(geminiT, MetaTokens.BlueLight, wireText: false);
            StripHorizontalTextPadding(geminiT);
            HintTrigger.Attach(geminiT.gameObject, "Assistant",
                "This button enables the voice assistant.");
        }
    }

    private static void ApplyFactoryButtonStyle(Transform buttonT)
    {
        if (buttonT == null) return;
        Image baseImg = buttonT.GetComponent<Image>();
        if (baseImg != null) baseImg.color = UIButton.BaseBg;

        RectTransform inner = buttonT.Find("Inner") as RectTransform;
        if (inner != null)
        {
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = Vector2.zero;
            inner.offsetMax = Vector2.zero;
        }
    }

    private static void StripHorizontalTextPadding(Transform buttonT)
    {
        if (buttonT == null) return;
        RectTransform textRt = buttonT.Find("Text") as RectTransform;
        if (textRt == null) return;
        textRt.offsetMin = new Vector2(0f, textRt.offsetMin.y);
        textRt.offsetMax = new Vector2(0f, textRt.offsetMax.y);
    }

    private void OnDataClicked()
    {
        if (dataPanelUI != null) dataPanelUI.TogglePanel();
    }

    private void OnToolClicked()
    {
        if (toolPanelUI != null) toolPanelUI.TogglePanel();
    }

    private void OnGeminiClicked()
    {
        SetGeminiActive(!_geminiActive);
    }

    public bool IsGeminiActive => _geminiActive;

    public void SetGeminiActive(bool active)
    {
        if (_geminiActive == active) return;
        _geminiActive = active;
        if (!active) SheetNotices.HideNotice();
        if (geminiClient != null) geminiClient.SetActive(active);
        SetGeminiVisual(active);
    }

    private void SetGeminiVisual(bool active)
    {
        if (_geminiHighlight != null)
            _geminiHighlight.SetRest(active ? GeminiActiveBg : GeminiInactiveBg);

        if (_geminiText != null)
            _geminiText.color = active ? GeminiActiveText : GeminiInactiveText;
    }

    private void OnEnable()
    {
        if (geminiClient == null) return;
        geminiClient.StatusChanged += OnGeminiStatusChanged;
        geminiClient.InactivityWarned += OnInactivityWarned;
        geminiClient.ActivityResumed += OnActivityResumed;
        geminiClient.InactivityExpired += OnInactivityExpired;
    }

    private void OnDisable()
    {
        if (geminiClient == null) return;
        geminiClient.StatusChanged -= OnGeminiStatusChanged;
        geminiClient.InactivityWarned -= OnInactivityWarned;
        geminiClient.ActivityResumed -= OnActivityResumed;
        geminiClient.InactivityExpired -= OnInactivityExpired;
    }

    private void OnGeminiStatusChanged(GeminiStatus status)
    {
        if (status != GeminiStatus.Failed && status != GeminiStatus.MicDenied) return;
        SetGeminiActive(false);
        SheetNotices.Show(this, "Assistant Off",
            status == GeminiStatus.MicDenied
                ? "Microphone access is denied, so the assistant cannot listen."
                : "The assistant could not stay connected and turned itself off.");
    }

    private void OnInactivityWarned(int minutesSilent)
    {
        SheetNotices.ShowPersistent("Assistant Inactivity",
            $"The Assistant will turn off in 1 minute because you have not talked in " +
            $"{minutesSilent} minute{(minutesSilent == 1 ? "" : "s")}.");
    }

    private void OnActivityResumed()
    {
        SheetNotices.HideNotice();
    }

    private void OnInactivityExpired()
    {
        SetGeminiActive(false);
        SheetNotices.Show(this, "Assistant Off", "The Assistant was turned off due to inactivity.");
    }

    private void LateUpdate()
    {
        if (_ihand == null || _canvasTransform == null) return;

        if (!_ihand.IsTrackedDataValid)
        {
            SetVisible(false);
            return;
        }

        if (!_ihand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist) ||
            !_ihand.GetJointPose(AlongJoint, out Pose middle) ||
            !_ihand.GetJointPose(IndexJoint, out Pose index) ||
            !_ihand.GetJointPose(PinkyJoint, out Pose pinky))
        {
            SetVisible(false);
            return;
        }

        Vector3 along = middle.position - wrist.position;
        Vector3 across = index.position - pinky.position;
        if (along.sqrMagnitude < 1e-8f || across.sqrMagnitude < 1e-8f)
        {
            SetVisible(false);
            return;
        }

        along.Normalize();
        across.Normalize();

        Vector3 outward = Vector3.Cross(along, across).normalized;

        Vector3 stackUp = -(stackAcrossForearm ? across : along);
        stackUp = Vector3.ProjectOnPlane(stackUp, outward).normalized;
        if (stackUp.sqrMagnitude < 1e-8f)
        {
            SetVisible(false);
            return;
        }

        Vector3 anchorPosition = Vector3.Lerp(wrist.position, middle.position, HandPlacement);
        Quaternion rotation = Quaternion.LookRotation(-outward, stackUp);
        Vector3 position = anchorPosition + outward * yOffsetFromHand;
        _canvasTransform.SetPositionAndRotation(position, rotation);

        SetVisible(true);
    }

    private bool _wasVisible;

    private void SetVisible(bool visible)
    {
        if (_canvas == null) return;
        if (_canvas.gameObject.activeSelf != visible)
            _canvas.gameObject.SetActive(visible);
        if (visible && !_wasVisible && geminiClient != null)
            geminiClient.NotifyIntent();
        _wasVisible = visible;
    }
}
