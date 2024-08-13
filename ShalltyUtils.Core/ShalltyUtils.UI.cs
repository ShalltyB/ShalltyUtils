extern alias aliasTimeline;
using aliasTimeline::Timeline;
using aliasTimeline.UILib;
using aliasTimeline.UILib.EventHandlers;
using ShalltyUtils.TimelineBaking;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;
using static ShalltyUtils.ShalltyUtils;
using Random = UnityEngine.Random;

namespace ShalltyUtils
{

    // Snippet to get RectTransform values at runtime from REPL
    public static class myUI
    {
        public static void GetRect(GameObject gameObject)
        {
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            Debug.Log($"SetRect({rectTransform.anchorMin.x}f, {rectTransform.anchorMin.y}f, {rectTransform.anchorMax.x}f, {rectTransform.anchorMax.y}f, {rectTransform.offsetMin.x}f, {rectTransform.offsetMin.y}f, {rectTransform.offsetMax.x}f, {rectTransform.offsetMax.y}f);");
        }
    }

    public class UI
	{
        public static GameObject interpolablesMoveParent;
        public static Text keyframeValueName;
        public static InputField keyframeValueField;
        public static string currentValue = "";
        private static InputField inputFieldRandom;
        private static Toggle toggleValueRandom;
        private static Toggle toggleTimeRandom;

        public static Toggle animBakeToggleHead;
        public static Toggle animBakeToggleLeftArm;
        public static Toggle animBakeToggleLeftFingers;
        public static Toggle animBakeToggleRightArm;
        public static Toggle animBakeToggleRightFingers;
        public static Toggle animBakeToggleHips;
        public static Toggle animBakeToggleLeftLeg;
        public static Toggle animBakeToggleRightLeg;
        private static ScrollRect curvePresetScroll;
        private static InputField curvePresetSaveField;
        public static Button animBakeButton;
        public static Button routeBakeButton;
        public static Button dbBakeButton;

        private static Coroutine tooltipCoroutine;

        public static List<CurvePreset> curvePresets = new List<CurvePreset>();
        public static List<Button> curvePresetsButtons = new List<Button>();

        public class CurvePreset
        {
            public AnimationCurve curve;
            public string file;
            public Texture2D image;

            public CurvePreset(string file, AnimationCurve curve, Texture2D image)
            {
                this.file = file;
                this.curve = curve;
                this.image = image;
            }
        }


        public static void Init()
        {
            #region Move Interpolables

            interpolablesMoveParent = new GameObject("ShalltyUtilsInterpolablesMove");
            interpolablesMoveParent.transform.SetParent(_timeline._ui.transform.Find("Timeline Window/Main Container/Timeline/Interpolables/Top"));
            interpolablesMoveParent.transform.localPosition = Vector3.zero;

            var interpolableMoveUp = UIUtility.CreateButton("ShalltyUtilsButton", interpolablesMoveParent.transform, "▲");
            ((RectTransform)interpolableMoveUp.transform).sizeDelta = new Vector2(100f, 25f);
            interpolableMoveUp.transform.localPosition = new Vector2(-50f, 10f);
            interpolableMoveUp.onClick.AddListener(() =>
            {
                _timeline._interpolablesTree.MoveUp(_timeline._selectedInterpolables.Select(elem => (INode)_timeline._interpolablesTree.GetLeafNode(elem)));
                _timeline.UpdateInterpolablesView();
            });
            CreateTooltip(interpolableMoveUp.gameObject, "Move UP selected Interpolable(s)", 300f, 30f);

            var interpolableMoveDown = UIUtility.CreateButton("ShalltyUtilsButton", interpolablesMoveParent.transform, "▼");
            ((RectTransform)interpolableMoveDown.transform).sizeDelta = new Vector2(100f, 25f);
            interpolableMoveDown.transform.localPosition = new Vector2(30f, 10f);
            interpolableMoveDown.onClick.AddListener(() =>
            {
                _timeline._interpolablesTree.MoveDown(_timeline._selectedInterpolables.Select(elem => (INode)_timeline._interpolablesTree.GetLeafNode(elem)));
                _timeline.UpdateInterpolablesView();
            });
            CreateTooltip(interpolableMoveDown.gameObject, "Move DOWN selected Interpolable(s)", 300f, 30f);

            #endregion

            ///////////////////

            #region Main Panel

            var mainButton = UIUtility.CreateButton("ShalltyUtilsButton", _timeline._ui.transform.Find("Timeline Window/Buttons"), "ShalltyUtils");
            UIUtility.AddOutlineToObject(mainButton.transform, Color.black);

            var layoutButton = mainButton.gameObject.AddComponent<LayoutElement>();
            layoutButton.preferredHeight = 35f;

            Image mainPanel = UIUtility.CreatePanel("ShalltyUtilsPanel", _timeline._timelineWindow);
            mainPanel.transform.SetRect(0f, 0f, 0f, 0f, -300f, 3f, 3f, 328f);
            UIUtility.AddOutlineToObject(mainPanel.transform, Color.black);

            mainPanel.gameObject.SetActive(false);

            mainButton.onClick.AddListener(() =>
            {
                if (_timeline._helpPanel.gameObject.activeSelf)
                    _timeline._helpPanel.gameObject.SetActive(false);

                mainPanel.gameObject.SetActive(!mainPanel.gameObject.activeSelf);
            });

            _timeline._ui.transform.Find("Timeline Window/Buttons/Help").GetComponent<Button>().onClick.AddListener(() =>
            {
                mainPanel.gameObject.SetActive(false);
            });

            var panelScroll = UIUtility.CreateScrollView("ShalltyUtilsButton", mainPanel.transform);
            UIUtility.AddOutlineToObject(panelScroll.transform, Color.black);
            panelScroll.transform.SetRect(0f, 0f, 1f, 1f, 10f, 10f, -10f, -10f);
            panelScroll.inertia = false;
            panelScroll.scrollSensitivity = 40f;
            panelScroll.movementType = ScrollRect.MovementType.Clamped;
            panelScroll.content.SetRect();
            panelScroll.content.sizeDelta = new Vector2(0f, 260f);


            var verticalLayout = UIUtility.CreateNewUIObject("VerticalLayout", panelScroll.content);
            verticalLayout.SetRect();
            verticalLayout.gameObject.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(2, 2, 2, 2);

            #endregion

            //////////////////////////////////////
           
            #region Keyframes Groups

            var buttonKeyframesGroups = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Keyframes Groups");
            buttonKeyframesGroups.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonKeyframesGroups.onClick.AddListener(() => { KeyframesGroups.showKeyframeGroupsUI = !KeyframesGroups.showKeyframeGroupsUI; });
            CreateTooltip(buttonKeyframesGroups.gameObject, "Open the Keyframes Groups UI.", 350f, 50f);
            ((RectTransform)buttonKeyframesGroups.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Motion Path

            var motionPathParent = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Motion Path");
            var motionPathParentElement = motionPathParent.gameObject.AddComponent<LayoutElement>();
            motionPathParentElement.preferredHeight = 30f;
            ((RectTransform)motionPathParent.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            var motionPathPanel = UIUtility.CreatePanel("ShalltyUtilsButton", mainPanel.transform.parent);
            motionPathPanel.transform.SetRect(0f, 0f, 0f, 0f, -200f, 400f, 100f, 600f);
            UIUtility.AddOutlineToObject(motionPathPanel.transform, Color.black);
            motionPathPanel.gameObject.SetActive(false);
            motionPathParent.onClick.AddListener(() =>
            {
                motionPathPanel.gameObject.SetActive(!motionPathPanel.gameObject.activeSelf);
            });

            var motionPathDrag = UIUtility.CreatePanel("ShalltyUtilsButton", motionPathPanel.transform);
            motionPathDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            motionPathDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(motionPathPanel.rectTransform, motionPathPanel.rectTransform);

            var motionPathTitle = UIUtility.CreateText("ShalltyUtilsButton", motionPathDrag.transform, "Motion Path");
            motionPathTitle.transform.SetRect();
            motionPathTitle.alignment = TextAnchor.MiddleCenter;

            var motionPathClose = UIUtility.CreateButton("CloseButton", motionPathDrag.transform, "X");
            motionPathClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            motionPathClose.onClick.AddListener(() =>
            {
                motionPathPanel.gameObject.SetActive(false);
            });

            var motionPathToggle = UIUtility.CreateToggle("ShalltyUtilsButton", motionPathPanel.transform, "Show Motion Path");
            motionPathToggle.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -125f, 40.5f, 105f, 59.5f);
            motionPathToggle.GetComponentInChildren<Text>().transform.SetRect(0f, 0f, 1f, 1f, 9f, -5.5f, 9f, 4.5f);
            motionPathToggle.isOn = false;
            motionPathToggle.onValueChanged.AddListener(b => { MotionPath.showMotionPath = b; });

            var motionPathDropdown = UIUtility.CreateDropdown("ShalltyUtilsDropDown", motionPathPanel.transform, "Mode");
            motionPathDropdown.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -135f, -12.5f, 115f, 22.5f);
            motionPathDropdown.options.Clear();
            motionPathDropdown.options.Add(new Dropdown.OptionData("Selected Keyframes"));
            motionPathDropdown.options.Add(new Dropdown.OptionData("Selected Interpolable"));
            motionPathDropdown.options.Add(new Dropdown.OptionData("Selected Interpolable (Range)"));
            motionPathDropdown.value = 1;
            motionPathDropdown.onValueChanged.AddListener(i => { MotionPath.motionPathMode = i; });


            var motionPathRangeLabel = UIUtility.CreateText("ShalltyUtilsButton", motionPathPanel.transform, "◄ Range  ►");
            motionPathRangeLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -75f, -60f, 75f, -20f);

            var motionPathRangeLeftField = UIUtility.CreateInputField("ShalltyUtilsButton", motionPathRangeLabel.transform, "");
            motionPathRangeLeftField.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -145f, -17f, -85f, 13f);
            motionPathRangeLeftField.contentType = InputField.ContentType.IntegerNumber;
            motionPathRangeLeftField.textComponent.alignment = TextAnchor.MiddleCenter;
            motionPathRangeLeftField.caretWidth = 3;
            motionPathRangeLeftField.text = "1";
            ((Text)motionPathRangeLeftField.placeholder).alignment = TextAnchor.MiddleCenter;

            motionPathRangeLeftField.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out MotionPath.rangeLeft) || MotionPath.rangeLeft < 0)
                {
                    MotionPath.rangeLeft = 1;
                    motionPathRangeLeftField.text = "1";
                    return;
                }
            });

            var motionPathRangeRightField = UIUtility.CreateInputField("ShalltyUtilsButton", motionPathRangeLabel.transform, "");
            motionPathRangeRightField.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 85f, -17f, 145f, 13f);
            motionPathRangeRightField.contentType = InputField.ContentType.IntegerNumber;
            motionPathRangeRightField.caretWidth = 3;
            motionPathRangeRightField.textComponent.alignment = TextAnchor.MiddleCenter;
            motionPathRangeRightField.text = "1";
            ((Text)motionPathRangeRightField.placeholder).alignment = TextAnchor.MiddleCenter;

            motionPathRangeRightField.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out MotionPath.rangeRight) || MotionPath.rangeRight < 0)
                {
                    MotionPath.rangeRight = 1;
                    motionPathRangeRightField.text = "1";
                    return;
                }
            });

            #endregion

            #region Interpolable Files

            var buttonSaveInterpolables = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Save Interpolables File");
            buttonSaveInterpolables.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonSaveInterpolables.onClick.AddListener(() => { SaveInterpolablesFile(); });
            CreateTooltip(buttonSaveInterpolables.gameObject, "Save the selected Interpolable(s) into a file.", 250f, 50f);
            ((RectTransform)buttonSaveInterpolables.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);


            var buttonLoadInterpolables = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Load Interpolables File");
            buttonLoadInterpolables.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonLoadInterpolables.onClick.AddListener(() => { LoadInterpolablesFile(); });
            CreateTooltip(buttonLoadInterpolables.gameObject, "Load Interpolables from a file starting from the current time.", 250f, 50f);
            ((RectTransform)buttonLoadInterpolables.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion


            ///////////////////

            #region Animation Baking

            var buttonAnimBake = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Animation Baking");
            buttonAnimBake.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)buttonAnimBake.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            var animBakePanel = UIUtility.CreatePanel("ShalltyUtilsButton", mainPanel.transform.parent);
            animBakePanel.transform.SetRect(0f, 0f, 0f, 0f, 110f, 400f, 410f, 1000f);
            UIUtility.AddOutlineToObject(animBakePanel.transform, Color.black);
            animBakePanel.gameObject.SetActive(false);
            buttonAnimBake.onClick.AddListener(() =>
            {
                animBakePanel.gameObject.SetActive(!animBakePanel.gameObject.activeSelf);
            });

            var animBakeDrag = UIUtility.CreatePanel("ShalltyUtilsButton", animBakePanel.transform);
            animBakeDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            animBakeDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(animBakePanel.rectTransform, animBakePanel.rectTransform);

            var animBakeTitle = UIUtility.CreateText("ShalltyUtilsButton", animBakeDrag.transform, "Animation Baking");
            animBakeTitle.transform.SetRect();
            animBakeTitle.alignment = TextAnchor.MiddleCenter;

            var animBakeClose = UIUtility.CreateButton("CloseButton", animBakeDrag.transform, "X");
            animBakeClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            animBakeClose.onClick.AddListener(() => { animBakePanel.gameObject.SetActive(false); });


            var animBakeGroupsLabel = UIUtility.CreateText("ShalltyUtilsButton", animBakePanel.transform, "BAKE INTERPOLABLES:");
            animBakeGroupsLabel.transform.SetRect(0f, 0.95f, 0.8f, 1f, 30f, -30f, 30f, -30f);


            var animBakeBonesPanel = UIUtility.CreatePanel("ShalltyUtilsButton", animBakePanel.transform);
            animBakeBonesPanel.transform.SetRect(0.05f, 0.45f, 0.95f, 0.90f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(animBakeBonesPanel.transform);

            animBakeToggleHead = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "HEAD");
            animBakeToggleHead.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -20f, 185f, 60f, 205f);
            ((RectTransform)animBakeToggleHead.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleHead.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -16f, 21.5f, -44f, 18.5f);


            animBakeToggleLeftArm = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "LEFT ARM");
            animBakeToggleLeftArm.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -75f, 140f, 35f, 160f);
            ((RectTransform)animBakeToggleLeftArm.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleLeftArm.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -36f, 21.5f, -64f, 18.5f);


            animBakeToggleLeftFingers = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "LEFT FINGERS");
            animBakeToggleLeftFingers.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -75f, 90f, 75f, 110f);
            ((RectTransform)animBakeToggleLeftFingers.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleLeftFingers.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -51f, 21.5f, -79f, 18.5f);


            animBakeToggleRightArm = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "RIGHT ARM");
            animBakeToggleRightArm.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 45f, 140f, 155f, 160f);
            ((RectTransform)animBakeToggleRightArm.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleRightArm.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -36f, 21.5f, -64f, 18.5f);


            animBakeToggleRightFingers = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "RIGHT FINGERS");
            animBakeToggleRightFingers.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 55f, 90f, 205f, 110f);
            ((RectTransform)animBakeToggleRightFingers.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleRightFingers.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -51f, 21.5f, -79f, 18.5f);


            animBakeToggleHips = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "HIPS");
            animBakeToggleHips.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -15f, 40f, 55f, 60f);
            ((RectTransform)animBakeToggleHips.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleHips.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -11f, 21.5f, -39f, 18.5f);


            animBakeToggleLeftLeg = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "LEFT LEG");
            animBakeToggleLeftLeg.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -85f, -10f, 25f, 10f);
            ((RectTransform)animBakeToggleLeftLeg.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleLeftLeg.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -31f, 21.5f, -59f, 18.5f);


            animBakeToggleRightLeg = UIUtility.CreateToggle("ShalltyUtilsButton", animBakePanel.transform, "RIGHT LEG");
            animBakeToggleRightLeg.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 65f, -10f, 175f, 10f);
            ((RectTransform)animBakeToggleRightLeg.transform.Find("Background").transform).sizeDelta = new Vector2(40f, 20f);
            ((RectTransform)animBakeToggleRightLeg.transform.Find("Label").transform).SetRect(0f, 0f, 1f, 1f, -31f, 21.5f, -59f, 18.5f);


            var animBakeFieldsPanel = UIUtility.CreatePanel("ShalltyUtilsButton", animBakePanel.transform);
            animBakeFieldsPanel.transform.SetRect(0.05f, 0.02f, 0.95f, 0.43f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(animBakeFieldsPanel.transform);
            var animBakeFieldsPanelLayout = animBakeFieldsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            animBakeFieldsPanelLayout.spacing = 15f;
            animBakeFieldsPanelLayout.childForceExpandHeight = false;
            animBakeFieldsPanelLayout.padding = new RectOffset(20, 20, 20, 20);

            var animBakeSecondsLabel = UIUtility.CreateText("ShalltyUtilsButton", animBakeFieldsPanel.transform, "MMDD Length:");
            animBakeSecondsLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var animBakeSecondsLabelLayout = animBakeSecondsLabel.gameObject.AddComponent<LayoutElement>();
            animBakeSecondsLabelLayout.preferredHeight = 30f;
            animBakeSecondsLabelLayout.minWidth = 400f;
            CreateTooltip(animBakeSecondsLabel.gameObject, "(ONLY FOR MMDD. Keep it in '-1' if NOT using it) Length of the VMD animation in seconds.", 500f);

            var animBakeSeconds = UIUtility.CreateInputField("ShalltyUtilsButton", animBakeSecondsLabel.transform, "");
            animBakeSeconds.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            animBakeSeconds.contentType = InputField.ContentType.DecimalNumber;
            animBakeSeconds.textComponent.alignment = TextAnchor.MiddleCenter;
            animBakeSeconds.text = "-1";
            animBakeSeconds.caretWidth = 3;
            ((Text)animBakeSeconds.placeholder).alignment = TextAnchor.MiddleCenter;

            animBakeSeconds.onEndEdit.AddListener(s =>
            {
                if (!float.TryParse(s, out BakeAnimation.animBakingSeconds) || BakeAnimation.animBakingSeconds < -1)
                {
                    BakeAnimation.animBakingSeconds = -1f;
                    animBakeSeconds.text = "-1";
                    return;
                }
            });


            var animBakeSpeedLabel = UIUtility.CreateText("ShalltyUtilsButton", animBakeFieldsPanel.transform, "Anim Speed:");
            animBakeSpeedLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var animBakeSpeedLabelLayout = animBakeSpeedLabel.gameObject.AddComponent<LayoutElement>();
            animBakeSpeedLabelLayout.preferredHeight = 30f;
            animBakeSpeedLabelLayout.minWidth = 400f;
            CreateTooltip(animBakeSpeedLabel.gameObject, "Speed of the animation.", 230f, 30f);

            var animBakeSpeed = UIUtility.CreateInputField("ShalltyUtilsButton", animBakeSpeedLabel.transform, "");
            animBakeSpeed.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            animBakeSpeed.contentType = InputField.ContentType.DecimalNumber;
            animBakeSpeed.textComponent.alignment = TextAnchor.MiddleCenter;
            animBakeSpeed.text = "1";
            animBakeSpeed.caretWidth = 3;
            ((Text)animBakeSpeed.placeholder).alignment = TextAnchor.MiddleCenter;

            animBakeSpeed.onEndEdit.AddListener(s =>
            {
                if (!float.TryParse(s, out BakeAnimation.animBakingSpeed) || BakeAnimation.animBakingSpeed < 0)
                {
                    BakeAnimation.animBakingSpeed = 1f;
                    animBakeSpeed.text = "1";
                    return;
                }
            });

            var animBakeLoopLabel = UIUtility.CreateText("ShalltyUtilsButton", animBakeFieldsPanel.transform, "Loop Count:");
            animBakeLoopLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -155f, 280f, -125f);
            var animBakeLoopLabelLayout = animBakeLoopLabel.gameObject.AddComponent<LayoutElement>();
            animBakeLoopLabelLayout.preferredHeight = 30f;
            animBakeLoopLabelLayout.minWidth = 400f;
            CreateTooltip(animBakeLoopLabel.gameObject, "How many times the animation will be looped.", 230f);

            var animBakeLoop = UIUtility.CreateInputField("ShalltyUtilsButton", animBakeLoopLabel.transform, "");
            animBakeLoop.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            animBakeLoop.contentType = InputField.ContentType.IntegerNumber;
            animBakeLoop.textComponent.alignment = TextAnchor.MiddleCenter;
            animBakeLoop.text = "1";
            animBakeLoop.caretWidth = 3;
            ((Text)animBakeLoop.placeholder).alignment = TextAnchor.MiddleCenter;

            animBakeLoop.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out BakeAnimation.animBakingLoops) || BakeAnimation.animBakingLoops < 1)
                {
                    BakeAnimation.animBakingLoops = 1;
                    animBakeLoop.text = "1";
                    return;
                }
            });

            var animBakeFPSLabel = UIUtility.CreateText("ShalltyUtilsButton", animBakeFieldsPanel.transform, "FPS:");
            animBakeFPSLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -215f, 280f, -185f);
            var animBakeFPSLabelLayout = animBakeFPSLabel.gameObject.AddComponent<LayoutElement>();
            animBakeFPSLabelLayout.preferredHeight = 30f;
            animBakeFPSLabelLayout.minWidth = 400f;
            CreateTooltip(animBakeFPSLabel.gameObject, "How many Keyframes per second will be created.", 400f);

            var animBakeFPS = UIUtility.CreateInputField("ShalltyUtilsButton", animBakeFPSLabel.transform, "");
            animBakeFPS.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            animBakeFPS.contentType = InputField.ContentType.IntegerNumber;
            animBakeFPS.textComponent.alignment = TextAnchor.MiddleCenter;
            animBakeFPS.text = "10";
            animBakeFPS.caretWidth = 3;
            ((Text)animBakeFPS.placeholder).alignment = TextAnchor.MiddleCenter;

            animBakeFPS.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out BakeAnimation.animBakingFPS) || BakeAnimation.animBakingFPS < 1)
                {
                    BakeAnimation.animBakingFPS = 10;
                    animBakeFPS.text = "10";
                    return;
                }
            });

            animBakeButton = UIUtility.CreateButton("ShalltyUtilsButton", animBakeFieldsPanel.transform, "Bake Animation");
            animBakeButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -130f, -285f, 130f, -245f);
            var animBakeButtonLayout = animBakeButton.gameObject.AddComponent<LayoutElement>();
            animBakeButtonLayout.preferredHeight = 50f;
            animBakeButton.onClick.AddListener(() => 
            {
                BakeAnimation.Bake();
                animBakeButton.GetComponentInChildren<Text>().text = BakeAnimation.isBakingBones ? "Stop Baking" : "Bake Animation";
            });

            #endregion

            #region DynamicBones baking

            var buttonDBBake = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "DynamicBones Baking");
            buttonDBBake.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)buttonDBBake.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            var dbBakePanel = UIUtility.CreatePanel("ShalltyUtilsButton", mainPanel.transform.parent);
            dbBakePanel.transform.SetRect(0f, 0f, 0f, 0f, 400f, 400f, 750f, 750f);
            UIUtility.AddOutlineToObject(dbBakePanel.transform, Color.black);
            dbBakePanel.gameObject.SetActive(false);
            buttonDBBake.onClick.AddListener(() =>
            {
                dbBakePanel.gameObject.SetActive(!dbBakePanel.gameObject.activeSelf);
            });

            var dbBakeDrag = UIUtility.CreatePanel("ShalltyUtilsButton", dbBakePanel.transform);
            dbBakeDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            dbBakeDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(dbBakePanel.rectTransform, dbBakePanel.rectTransform);

            var dbBakeTitle = UIUtility.CreateText("ShalltyUtilsButton", dbBakeDrag.transform, "DynamicBones Baking");
            dbBakeTitle.transform.SetRect();
            dbBakeTitle.alignment = TextAnchor.MiddleCenter;

            var dbBakeClose = UIUtility.CreateButton("CloseButton", dbBakeDrag.transform, "X");
            dbBakeClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            dbBakeClose.onClick.AddListener(() => { dbBakePanel.gameObject.SetActive(false); });

            var dbBakeFieldsPanel = UIUtility.CreatePanel("ShalltyUtilsButton", dbBakePanel.transform);
            dbBakeFieldsPanel.transform.SetRect(0.05f, 0.05f, 0.95f, 0.9f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(dbBakeFieldsPanel.transform);
            var dbBakeFieldsPanelLayout = dbBakeFieldsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            dbBakeFieldsPanelLayout.spacing = 15f;
            dbBakeFieldsPanelLayout.childForceExpandHeight = false;
            dbBakeFieldsPanelLayout.padding = new RectOffset(20, 20, 20, 20);

            var dbBakeSecondsLabel = UIUtility.CreateText("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "Seconds:");
            dbBakeSecondsLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var dbBakeSecondsLabelLayout = dbBakeSecondsLabel.gameObject.AddComponent<LayoutElement>();
            dbBakeSecondsLabelLayout.preferredHeight = 30f;
            dbBakeSecondsLabelLayout.minWidth = 400f;
            CreateTooltip(dbBakeSecondsLabel.gameObject, "How many seconds will be baked.", 300f, 30f);

            var dbBakeSeconds = UIUtility.CreateInputField("ShalltyUtilsButton", dbBakeSecondsLabel.transform, "");
            dbBakeSeconds.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            dbBakeSeconds.contentType = InputField.ContentType.DecimalNumber;
            dbBakeSeconds.textComponent.alignment = TextAnchor.MiddleCenter;
            dbBakeSeconds.text = "10";
            dbBakeSeconds.caretWidth = 3;
            ((Text)dbBakeSeconds.placeholder).alignment = TextAnchor.MiddleCenter;

            dbBakeSeconds.onEndEdit.AddListener(s =>
            {
                if (!float.TryParse(s, out BakeDynamicBones.dbBakingSeconds) || BakeDynamicBones.dbBakingSeconds < 0)
                {
                    BakeDynamicBones.dbBakingSeconds = 10f;
                    dbBakeSeconds.text = "10";
                    return;
                }
            });


            var dbBakeSpeedLabel = UIUtility.CreateText("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "Wait Seconds:");
            dbBakeSpeedLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var dbBakeSpeedLabelLayout = dbBakeSpeedLabel.gameObject.AddComponent<LayoutElement>();
            dbBakeSpeedLabelLayout.preferredHeight = 30f;
            dbBakeSpeedLabelLayout.minWidth = 400f;
            CreateTooltip(dbBakeSpeedLabel.gameObject, "How many seconds to wait between each keyframe baked", 550f, 30f);

            var dbBakeSpeed = UIUtility.CreateInputField("ShalltyUtilsButton", dbBakeSpeedLabel.transform, "");
            dbBakeSpeed.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            dbBakeSpeed.contentType = InputField.ContentType.DecimalNumber;
            dbBakeSpeed.textComponent.alignment = TextAnchor.MiddleCenter;
            dbBakeSpeed.text = "0";
            dbBakeSpeed.caretWidth = 3;
            ((Text)dbBakeSpeed.placeholder).alignment = TextAnchor.MiddleCenter;

            dbBakeSpeed.onEndEdit.AddListener(s =>
            {
                if (!float.TryParse(s, out BakeDynamicBones.dbBakingWait) || BakeDynamicBones.dbBakingWait < 0)
                {
                    BakeDynamicBones.dbBakingWait = 0f;
                    dbBakeSpeed.text = "0";
                    return;
                }
            });

            var dbBakeFPSLabel = UIUtility.CreateText("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "FPS:");
            dbBakeFPSLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -215f, 280f, -185f);
            var dbBakeFPSLabelLayout = dbBakeFPSLabel.gameObject.AddComponent<LayoutElement>();
            dbBakeFPSLabelLayout.preferredHeight = 30f;
            dbBakeFPSLabelLayout.minWidth = 400f;
            CreateTooltip(dbBakeFPSLabel.gameObject, "How many Keyframes per second will be created.", 400f, 30f);

            var dbBakeFPS = UIUtility.CreateInputField("ShalltyUtilsButton", dbBakeFPSLabel.transform, "");
            dbBakeFPS.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            dbBakeFPS.contentType = InputField.ContentType.IntegerNumber;
            dbBakeFPS.textComponent.alignment = TextAnchor.MiddleCenter;
            dbBakeFPS.text = "30";
            dbBakeFPS.caretWidth = 3;
            ((Text)dbBakeFPS.placeholder).alignment = TextAnchor.MiddleCenter;

            dbBakeFPS.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out BakeDynamicBones.dbBakingFPS) || BakeDynamicBones.dbBakingFPS < 1)
                {
                    BakeDynamicBones.dbBakingFPS = 30;
                    dbBakeFPS.text = "30";
                    return;
                }
            });

            /// dbBakeRealTimeToggle
            /// 

            var dbBakeRealTimeToggle = UIUtility.CreateToggle("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "Real Time?");
            dbBakeRealTimeToggle.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -215f, 280f, -185f);
            var dbBakeRealTimeLabelLayout = dbBakeRealTimeToggle.gameObject.AddComponent<LayoutElement>();
            dbBakeRealTimeLabelLayout.preferredHeight = 30f;
            dbBakeRealTimeLabelLayout.minWidth = 400f;
            CreateTooltip(dbBakeRealTimeToggle.gameObject, "Bake the DynamicBones at real time?", 400f, 30f);

            dbBakeRealTimeToggle.isOn = false;

            dbBakeRealTimeToggle.onValueChanged.AddListener(b => { BakeDynamicBones.dbBakingRealTime = b; });

            /// dbBakePositionToggle
            /// 

            var dbBakePositionToggle = UIUtility.CreateToggle("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "Position?");
            dbBakePositionToggle.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -215f, 280f, -185f);
            var dbBakePositionToggleLayout = dbBakePositionToggle.gameObject.AddComponent<LayoutElement>();
            dbBakePositionToggleLayout.preferredHeight = 30f;
            dbBakePositionToggleLayout.minWidth = 400f;
            CreateTooltip(dbBakePositionToggle.gameObject, "Bake the DynamicBones position?", 400f, 30f);

            dbBakePositionToggle.isOn = false;

            dbBakePositionToggle.onValueChanged.AddListener(b => { BakeDynamicBones.dbBakingPos = b; });


            dbBakeButton = UIUtility.CreateButton("ShalltyUtilsButton", dbBakeFieldsPanel.transform, "Bake DynamicBones");
            dbBakeButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -130f, -285f, 130f, -245f);
            var dbBakeButtonLayout = dbBakeButton.gameObject.AddComponent<LayoutElement>();
            dbBakeButtonLayout.preferredHeight = 50f;
            dbBakeButton.onClick.AddListener(() =>
            {
                BakeDynamicBones.Bake();
                dbBakeButton.GetComponentInChildren<Text>().text = BakeDynamicBones.isBakingDB ? "Stop Baking" : "Bake DynamicBones";
            });

            #endregion

            #region Routes Baking

            var buttonRouteBake = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Routes Baking");
            buttonRouteBake.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)buttonRouteBake.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            var routeBakePanel = UIUtility.CreatePanel("ShalltyUtilsButton", mainPanel.transform.parent);
            routeBakePanel.transform.SetRect(0f, 0f, 0f, 0f, 425f, 660f, 725f, 820f);
            UIUtility.AddOutlineToObject(routeBakePanel.transform, Color.black);
            routeBakePanel.gameObject.SetActive(false);
            buttonRouteBake.onClick.AddListener(() =>
            {
                routeBakePanel.gameObject.SetActive(!routeBakePanel.gameObject.activeSelf);
            });

            var routeBakeDrag = UIUtility.CreatePanel("ShalltyUtilsButton", routeBakePanel.transform);
            routeBakeDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            routeBakeDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(routeBakePanel.rectTransform, routeBakePanel.rectTransform);

            var routeBakeTitle = UIUtility.CreateText("ShalltyUtilsButton", routeBakeDrag.transform, "Routes Baking");
            routeBakeTitle.transform.SetRect();
            routeBakeTitle.alignment = TextAnchor.MiddleCenter;

            var routeBakeClose = UIUtility.CreateButton("CloseButton", routeBakeDrag.transform, "X");
            routeBakeClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            routeBakeClose.onClick.AddListener(() => { routeBakePanel.gameObject.SetActive(false); });

            var routeBakeFieldsPanel = UIUtility.CreatePanel("ShalltyUtilsButton", routeBakePanel.transform);
            routeBakeFieldsPanel.transform.SetRect(0.05f, 0.05f, 0.95f, 0.80f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(routeBakeFieldsPanel.transform);
            var routeBakeFieldsPanelLayout = routeBakeFieldsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            routeBakeFieldsPanelLayout.spacing = 15f;
            routeBakeFieldsPanelLayout.childForceExpandHeight = false;
            routeBakeFieldsPanelLayout.padding = new RectOffset(20, 20, 20, 20);

            var routeBakeFPSLabel = UIUtility.CreateText("ShalltyUtilsButton", routeBakeFieldsPanel.transform, "FPS:");
            routeBakeFPSLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -215f, 280f, -185f);
            var routeBakeFPSLabelLayout = routeBakeFPSLabel.gameObject.AddComponent<LayoutElement>();
            routeBakeFPSLabelLayout.preferredHeight = 30f;
            routeBakeFPSLabelLayout.minWidth = 400f;
            CreateTooltip(routeBakeFPSLabel.gameObject, "How many Keyframes per second will be created.", 400f, 30f);

            var routeBakeFPS = UIUtility.CreateInputField("ShalltyUtilsButton", routeBakeFPSLabel.transform, "");
            routeBakeFPS.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -40f, -15f, 40f, 15f);
            routeBakeFPS.contentType = InputField.ContentType.IntegerNumber;
            routeBakeFPS.textComponent.alignment = TextAnchor.MiddleCenter;
            routeBakeFPS.text = "30";
            routeBakeFPS.caretWidth = 3;
            ((Text)routeBakeFPS.placeholder).alignment = TextAnchor.MiddleCenter;

            routeBakeFPS.onEndEdit.AddListener(s =>
            {
                if (!int.TryParse(s, out BakeRoutes.routeBakingFPS) || BakeRoutes.routeBakingFPS < 1)
                {
                    BakeRoutes.routeBakingFPS = 30;
                    routeBakeFPS.text = "30";
                    return;
                }
            });

            routeBakeButton = UIUtility.CreateButton("ShalltyUtilsButton", routeBakeFieldsPanel.transform, "Bake Route");
            routeBakeButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -130f, -285f, 130f, -245f);
            var routeBakeButtonLayout = routeBakeButton.gameObject.AddComponent<LayoutElement>();
            routeBakeButtonLayout.preferredHeight = 50f;
            routeBakeButton.onClick.AddListener(() =>
            {
                BakeRoutes.Bake();
                routeBakeButton.GetComponentInChildren<Text>().text = BakeRoutes.isBakingRoute ? "Stop Baking" : "Bake Route";
            });

            #endregion

            #region Custom Baking

            var buttonCustomBake = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Custom Baking");
            buttonCustomBake.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)buttonCustomBake.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            buttonCustomBake.onClick.AddListener(() =>
            {
                BakeCustom.showCustomBakeUI = !BakeCustom.showCustomBakeUI;
            });

            #endregion

            ///////////////////

            #region FolderConstraints Rigs

            var buttonFolderConstraintsRigs = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "FolderConstraints Rigs");
            buttonFolderConstraintsRigs.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonFolderConstraintsRigs.onClick.AddListener(() => { FolderConstraintsRig.showFoldersConstraintsRigsUI = !FolderConstraintsRig.showFoldersConstraintsRigsUI; });
            CreateTooltip(buttonFolderConstraintsRigs.gameObject, "Open the FolderConstraints Rigs UI.", 350f, 50f);
            ((RectTransform)buttonFolderConstraintsRigs.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Split Interpolables Axis

            var buttonSplitXYZ = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Split Interpolables Axis");
            buttonSplitXYZ.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonSplitXYZ.onClick.AddListener(() => { SplitInterpolablesXYZ(); });
            CreateTooltip(buttonSplitXYZ.gameObject, "Split the X/Y/Z axis of the selected Interpolable(s) for more control.", 300f, 50f);
            ((RectTransform)buttonSplitXYZ.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Bake X/Y/Z Interpolables

            var buttonSplitXYZFolder = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Bake X/Y/Z Interpolables");
            buttonSplitXYZFolder.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonSplitXYZFolder.onClick.AddListener(() => { ConvertInterpolablesXYZToFolders(); });
            CreateTooltip(buttonSplitXYZFolder.gameObject, "Bake all the X/Y/Z Interpolable(s) into Folders, so that this plugin is not a dependency when sharing the scene.", 500f);
            ((RectTransform)buttonSplitXYZFolder.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Cleanup Redundant Keyframes

            var buttonClearKeys = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Cleanup Redundant Keyframes");
            buttonClearKeys.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonClearKeys.onClick.AddListener(() => { CleanupKeyframes(); });
            CreateTooltip(buttonClearKeys.gameObject, "Remove all the redudant Keyframes that won't affect the animation.", 500f, 30f);
            ((RectTransform)buttonClearKeys.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Cleanup Timeline Data

            var buttonClearTimeline = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Cleanup Timeline");
            buttonClearTimeline.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonClearTimeline.onClick.AddListener(() => { CleanupAllTimeline(); });
            CreateTooltip(buttonClearTimeline.gameObject, "Remove ALL the Timeline data from this scene, and resets timeline variables.", 500f, 30f);
            ((RectTransform)buttonClearTimeline.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Toggle Performance Mode

            var buttonPerformanceMode = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Toggle Performance Mode");
            buttonPerformanceMode.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonPerformanceMode.onClick.AddListener(() => { PerformanceMode.TogglePerformanceMode(); });
            CreateTooltip(buttonPerformanceMode.gameObject, "Turn on/off the Timeline Performance mode, which can increase performance.", 350f);
            ((RectTransform)buttonPerformanceMode.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Convert To Curves

            var buttonCurvesMode = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Convert To Curves");
            buttonCurvesMode.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonCurvesMode.onClick.AddListener(() => { ConvertInterpolablesToCurves(); });
            CreateTooltip(buttonCurvesMode.gameObject, "Normalize all the Keyframes of the selected Interpolable to a single curve (EXPERIMENTAL).", 350f);
            ((RectTransform)buttonCurvesMode.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion

            #region Mesh Sequencer

            var buttonMeshSequence = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Mesh Sequencer");
            buttonMeshSequence.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonMeshSequence.onClick.AddListener(() => { MeshSequencer.showMeshSequencerUI = !MeshSequencer.showMeshSequencerUI; });
            CreateTooltip(buttonMeshSequence.gameObject, "Open the UI to create a Mesh Sequence Animation.", 350f, 50f);
            ((RectTransform)buttonMeshSequence.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            #endregion


            #region Timeline UI Colors

            var timelineColorButton = UIUtility.CreateButton("ShalltyUtilsButton", verticalLayout, "Change Timeline UI Colors");
            timelineColorButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)timelineColorButton.GetComponentInChildren<Text>().transform).sizeDelta = new Vector2(-20f, -10f);

            var timelineColorPanel = UIUtility.CreatePanel("ShalltyUtilsButton", mainPanel.transform.parent);
            timelineColorPanel.transform.SetRect(0f, 0f, 0f, 0f, -200f, 650f, 100f, 850f);
            UIUtility.AddOutlineToObject(timelineColorPanel.transform);
            timelineColorPanel.gameObject.SetActive(false);
            timelineColorPanel.gameObject.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(10, 10, 0, 10);

            timelineColorButton.onClick.AddListener(() => { timelineColorPanel.gameObject.SetActive(!timelineColorPanel.gameObject.activeSelf); });


            var timelineColorDrag = UIUtility.CreatePanel("ShalltyUtilsButton", timelineColorPanel.transform);
            timelineColorDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            timelineColorDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(timelineColorPanel.rectTransform, timelineColorPanel.rectTransform);

            var timelineColorTitle = UIUtility.CreateText("ShalltyUtilsButton", timelineColorDrag.transform, "Timeline UI Colors");
            timelineColorTitle.transform.SetRect();
            timelineColorTitle.alignment = TextAnchor.MiddleCenter;

            var timelineColorClose = UIUtility.CreateButton("CloseButton", timelineColorDrag.transform, "X");
            timelineColorClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            timelineColorClose.onClick.AddListener(() => { timelineColorPanel.gameObject.SetActive(false); });

            var buttonFirstColor = UIUtility.CreateButton("ShalltyUtilsButton", timelineColorPanel.transform, "Primary Color");
            buttonFirstColor.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonFirstColor.onClick.AddListener(() => { TimelineFirstColor(); });

            var buttonSecondColor = UIUtility.CreateButton("ShalltyUtilsButton", timelineColorPanel.transform, "Secondary Color");
            buttonSecondColor.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonSecondColor.onClick.AddListener(() => { TimelineSecondColor(); });

            var buttonFirstTextColor = UIUtility.CreateButton("ShalltyUtilsButton", timelineColorPanel.transform, "Primary Text Color");
            buttonFirstTextColor.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonFirstTextColor.onClick.AddListener(() => { TimelineTextColor(); });

            var buttonSecondTextColor = UIUtility.CreateButton("ShalltyUtilsButton", timelineColorPanel.transform, "Secondary Text Color");
            buttonSecondTextColor.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            buttonSecondTextColor.onClick.AddListener(() => { TimelineText2Color(); });

            #endregion

            //////////////////////////////////////

            #region Keyframe Window

            Image keyframePanel = UIUtility.CreatePanel("ShalltyUtilsPanel", _timeline._ui.transform.Find("Keyframe Window/Main Container"));
            keyframePanel.transform.SetRect(-0.40f, 0f, 0f, 1.065f, 2f, -5f, -2f, 5f);
            keyframePanel.transform.SetSiblingIndex(0);
            UIUtility.AddOutlineToObject(keyframePanel.transform, Color.black);


            var keyframePanelButton = UIUtility.CreateButton("ShalltyUtilsButton", _timeline._ui.transform.Find("Keyframe Window/Main Container"), "► ShalltyUtils");
            keyframePanelButton.transform.SetRect(0f, 1f, 0f, 1f, 0f, 25f, 160f, 60f);
            keyframePanelButton.transform.SetSiblingIndex(0);
            UIUtility.AddOutlineToObject(keyframePanelButton.transform);
            keyframePanelButton.onClick.AddListener(() =>
            {
                if (GraphEditor.keyTimesMode) return;
                keyframePanel.gameObject.SetActive(!keyframePanel.gameObject.activeSelf);
                keyframePanelButton.GetComponentInChildren<Text>().text = keyframePanel.gameObject.activeSelf ? "◄ ShalltyUtils" : "► ShalltyUtils";
            });
            keyframePanel.gameObject.SetActive(false);

            ///////////////////

            #region Editable Values

            var editableValuesPanel = UIUtility.CreatePanel("ShalltyUtilsButton", keyframePanel.transform);
            editableValuesPanel.transform.SetRect(0f, 0.38f, 1f, 1f, 10f, 10f, -10f, -10f);
            UIUtility.AddOutlineToObject(editableValuesPanel.transform);


            keyframeValueName = UIUtility.CreateText("ShalltyUtilsButton", keyframePanel.transform, "Edit Value:");
            keyframeValueName.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, 105f, 100f, 155f);

            keyframeValueField = UIUtility.CreateInputField("ShalltyUtilsButton", keyframePanel.transform, "");
            keyframeValueField.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -155f, 77.5f, 155f, 112.5f);
            keyframeValueField.caretWidth = 3;
            keyframeValueField.onEndEdit.AddListener(s =>
            {
                currentValue = s;
                if (_timeline._selectedKeyframes.Count == 0)
                    return;

                object v = _timeline._selectedKeyframes[0].Value.value;
                if (!string.IsNullOrEmpty(currentValue))
                {
                    try
                    {
                        object deserializedObject = GetObjectFromString(currentValue, v);
                    }
                    catch (Exception e)
                    {
                        ResetKeyframeValue(GetDefaultValue(v));
                        ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
                        ShalltyUtils.Logger.LogError($"Please enter a valid '{v.GetType().Name}' value. ErrorType: ({e.Message})");
                    }
                }
                else
                {
                    ResetKeyframeValue(GetDefaultValue(v));
                    ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
                }
            });


            var setButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "Set");
            setButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 60f, 35f, 130f, 65f);
            setButton.onClick.AddListener(OnClickSetValue);
            CreateTooltip(setButton.gameObject, "Set the 'Edit Value' to selected Keyframe(s).", 250f, 40f);

            var getCurrentValueButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "Get Current Value");
            getCurrentValueButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, 40f, 25f, 70f);
            getCurrentValueButton.onClick.AddListener(() =>
            {
                if (_timeline._selectedKeyframes.Count > 0)
                    ResetKeyframeValue(_timeline._selectedKeyframes[0].Value.parent.GetValue());
            });
            //CreateTooltip(getCurrentValueButton.gameObject, "Get the current value of the first selected Keyframe.", 500f, 50f);

            var getButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "Get Keyframe Value");
            getButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, 8.5f, 25f, 38.5f);
            getButton.onClick.AddListener(() =>
            {
                if (_timeline._selectedKeyframes.Count > 0)
                    ResetKeyframeValue(_timeline._selectedKeyframes[0].Value.value);
            });
            //CreateTooltip(getButton.gameObject, "Get the value of the first selected Keyframe.", 500f, 50f);

            var defaultButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "Get Default Value");
            defaultButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, -25f, 25f, 5f);
            defaultButton.onClick.AddListener(() =>
            {
                if (_timeline._selectedKeyframes.Count > 0)
                    ResetKeyframeValue(GetDefaultValue(_timeline._selectedKeyframes[0].Value.value));
            });

            var addButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "+");
            addButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 100f, 0f, 140f, 30f);
            addButton.onClick.AddListener(OnClickAddValue);
            CreateTooltip(addButton.gameObject, "Adds the 'Edit Value' to selected Keyframe(s)", 250f, 40f);

            var substractButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "-");
            substractButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 50f, 0f, 90f, 30f);
            substractButton.onClick.AddListener(OnClickSubstractValue);
            CreateTooltip(substractButton.gameObject, "Substract the 'Edit Value' from the selected Keyframe(s)", 250f, 40f);

            #endregion

            #region Randomize

            var randomizePanel = UIUtility.CreatePanel("ShalltyUtilsButton", keyframePanel.transform);
            randomizePanel.transform.SetRect(0f, 0f, 1f, 0.4f, 10f, 10f, -10f, -10f);
            UIUtility.AddOutlineToObject(randomizePanel.transform);

            var randomizeButton = UIUtility.CreateButton("ShalltyUtilsButton", keyframePanel.transform, "Randomize selected keyframes");
            randomizeButton.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, -95f, 150f, -65f);
            randomizeButton.onClick.AddListener(OnClickRandomize);
            CreateTooltip(randomizeButton.gameObject, "Randomize the Value or Time of all selected Keyframes by randomly adding or substracting a number between 0 and the Randomness value.", 600f, 100f);

            var nameFieldRandom = UIUtility.CreateText("ShalltyUtilsButton", keyframePanel.transform, "Randomness:");
            nameFieldRandom.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -150f, -130f, 250f, -110f);

            inputFieldRandom = UIUtility.CreateInputField("ShalltyUtilsButton", nameFieldRandom.transform, "0.001");
            inputFieldRandom.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -200f, -40f, -100f, -10f);
            inputFieldRandom.caretWidth = 3;

            toggleValueRandom  = UIUtility.CreateToggle("ShalltyUtilsButton", keyframePanel.transform, "Randomize Value");
            toggleValueRandom.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 0f, -130f, 120f, -110f);

            toggleTimeRandom = UIUtility.CreateToggle("ShalltyUtilsButton", keyframePanel.transform, "Randomize Time");
            toggleTimeRandom.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, 0f, -160f, 120f, -140f);

            #endregion

            #region Curve Presets

            Transform oldPresets = _timeline._ui.transform.Find("Keyframe Window/Main Container/Curve Fields/Fields/Presets");
            oldPresets.GetComponent<LayoutElement>().preferredHeight = 100f;
            for (int i = 0; i < oldPresets.childCount; i++)
                oldPresets.GetChild(i).gameObject.SetActive(false);

            curvePresetScroll = UIUtility.CreateScrollView("ShalltyUtilsButton", oldPresets);
            curvePresetScroll.transform.SetRect(0f, 0f, 1f, 0.90f);
            curvePresetScroll.content.GetOrAddComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            curvePresetScroll.vertical = false;
            GameObject.Destroy(curvePresetScroll.verticalScrollbar.gameObject);
            curvePresetScroll.horizontalScrollbarSpacing = -6f;
            curvePresetScroll.verticalScrollbarSpacing = -18f;
            curvePresetScroll.inertia = false;
            curvePresetScroll.movementType = ScrollRect.MovementType.Clamped;
            curvePresetScroll.scrollSensitivity = -30f;
            UIUtility.AddOutlineToObject(curvePresetScroll.transform);
            ((RectTransform)curvePresetScroll.horizontalScrollbar.transform).sizeDelta = new Vector2(0f, 15f);

            CreateCurvePresets();


            var curvePresetSaveParent = UIUtility.CreateNewUIObject("ShalltyUtilsButton", oldPresets.transform.parent);
            curvePresetSaveParent.GetOrAddComponent<LayoutElement>().preferredHeight = 35f;

            curvePresetSaveField = UIUtility.CreateInputField("ShalltyUtilsButton", curvePresetSaveParent.transform, "Curve name...");
            curvePresetSaveField.caretWidth = 3;
            curvePresetSaveField.contentType = InputField.ContentType.Alphanumeric;
            curvePresetSaveField.transform.SetRect(0f, 0f, 0f, 0f, 120f, 1.509187f, 280f, 26.50919f);

            var curvePresetSave = UIUtility.CreateButton("ShalltyUtilsButton", curvePresetSaveParent.transform, "Save");
            curvePresetSave.transform.SetRect(0f, 0f, 0f, 0f, 55f, 1.509187f, 115f, 26.50919f);
            curvePresetSave.onClick.AddListener(() => SaveCurvePreset(curvePresetSaveField.text));

            var curvePresetDelete = UIUtility.CreateButton("ShalltyUtilsButton", curvePresetSaveParent.transform, "Delete");
            curvePresetDelete.transform.SetRect(0f, 0f, 0f, 0f, 0f, 1.509187f, 60f, 26.50919f);
            curvePresetDelete.onClick.AddListener(() => DeleteCurvePreset(curvePresetSaveField.text));

            #endregion

            #endregion

        }

        #region Curve Presets

        public static void CreateCurvePresets()
        {
            foreach (Button button in curvePresetsButtons)
                GameObject.Destroy(button.gameObject);

            curvePresetsButtons.Clear();

            curvePresets = LoadCurvePresets();
            List<CurvePreset> curves = new List<CurvePreset>(curvePresets);

            foreach (CurvePreset preset in curves)
            {
                string file = preset.file;
                AnimationCurve curve = preset.curve;

                string fileName = Path.GetFileNameWithoutExtension(file);

                var curveButton = UIUtility.CreateButton("ShalltyUtilsButton", curvePresetScroll.content, "");
                curveButton.GetOrAddComponent<LayoutElement>().preferredWidth = 56f;
                curveButton.onClick.AddListener(() =>
                {
                    if (!GraphEditor.keyTimesMode)
                    {
                        if (_timeline._selectedKeyframes.Count > 0)
                            _timeline.ApplyKeyframeCurvePreset(curve);
                    }
                    else
                    {
                        GraphEditor.curveKeyTimes.keys = curve.keys;
                        _graphEditor.UpdateCurve();
                    }
                    
                    curvePresetSaveField.text = fileName;
                });
                CreateTooltip(curveButton.gameObject, fileName,fileName.Length * 12, 30f);
                UIUtility.AddOutlineToObject(curveButton.transform);
                curvePresetsButtons.Add(curveButton);

                Sprite curveSprite = Sprite.Create(preset.image, new Rect(0, 0, preset.image.width, preset.image.height), new Vector2(0.5f, 0.5f));
                var image = UIUtility.CreateNewUIObject("curvePresetImage", curveButton.transform);
                UIUtility.AddImageToObject(image, curveSprite);
                image.SetRect();               
            }
            curvePresetScroll.content.sizeDelta = new Vector2((curvePresetsButtons.Count * 56f) - 280f, 56f);


            TimelineFirstColor(false);
            TimelineSecondColor(false);
            TimelineTextColor(false);
            TimelineText2Color(false);
        }

        private static void OnClickSetValue()
        {
            if (_timeline._selectedKeyframes.Count == 0)
                return;

            object v = _timeline._selectedKeyframes[0].Value.value;
            if (!string.IsNullOrEmpty(currentValue))
            {
                try
                {
                    object deserializedObject = GetObjectFromString(currentValue, v);

                    List<object> oldValues = new List<object>();

                    foreach (KeyValuePair<float, Timeline.Keyframe> pair in _timeline._selectedKeyframes)
                    {
                        oldValues.Add(pair.Value.value);
                        pair.Value.value = deserializedObject;
                    }

                    if (undoRedoTimeline.Value)
                        Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.UseCurrentValueCommand(oldValues, new List<KeyValuePair<float, Timeline.Keyframe>>(_timeline._selectedKeyframes)));

                    _timeline.UpdateKeyframeValueText();
                }
                catch (Exception e)
                {
                    ResetKeyframeValue(GetDefaultValue(v));
                    ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
                    ShalltyUtils.Logger.LogError($"Please enter a valid '{v.GetType().Name}' value. ErrorType: ({e.Message})");
                }
            }
            else
            {
                ResetKeyframeValue(GetDefaultValue(v));
                ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
            }
        }

        private static void OnClickAddValue()
        {
            if (_timeline._selectedKeyframes.Count == 0)
                return;

            object v = _timeline._selectedKeyframes[0].Value.value;
            if (!string.IsNullOrEmpty(currentValue))
            {
                try
                {
                    object deserializedObject = GetObjectFromString(currentValue, v);

                    List<object> oldValues = new List<object>();

                    foreach (KeyValuePair<float, Timeline.Keyframe> pair in _timeline._selectedKeyframes)
                    {
                        oldValues.Add(pair.Value.value);

                        if (v is int)
                        {
                            pair.Value.value = (int)pair.Value.value + (int)deserializedObject;
                        }
                        else if (v is float)
                        {
                            pair.Value.value = (float)pair.Value.value + (float)deserializedObject;
                        }
                        else if (v is Vector2)
                        {
                            pair.Value.value = (Vector2)pair.Value.value + (Vector2)deserializedObject;
                        }
                        else if (v is Vector3)
                        {
                            pair.Value.value = (Vector3)pair.Value.value + (Vector3)deserializedObject;
                        }
                        else if (v is Vector4)
                        {
                            pair.Value.value = (Vector4)pair.Value.value + (Vector4)deserializedObject;
                        }
                        else if (v is Color)
                        {
                            pair.Value.value = (Color)pair.Value.value + (Color)deserializedObject;
                        }
                        else if (v is Quaternion)
                        {
                            pair.Value.value = (Quaternion)pair.Value.value * (Quaternion)deserializedObject;
                        }
                    }

                    if (undoRedoTimeline.Value)
                        Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.UseCurrentValueCommand(oldValues, new List<KeyValuePair<float, Timeline.Keyframe>>(_timeline._selectedKeyframes)));

                    _timeline.UpdateKeyframeValueText();
                }
                catch (Exception e)
                {
                    ResetKeyframeValue(GetDefaultValue(v));
                    ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
                    ShalltyUtils.Logger.LogError($"Please enter a valid '{v.GetType().Name}' value. ErrorType: ({e.Message})");
                }
            }
            else
            {
                ResetKeyframeValue(GetDefaultValue(v));
                ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
            }
        }

        private static void OnClickSubstractValue()
        {
            if (_timeline._selectedKeyframes.Count == 0)
                return;

            object v = _timeline._selectedKeyframes[0].Value.value;
            if (!string.IsNullOrEmpty(currentValue))
            {
                try
                {
                    object deserializedObject = GetObjectFromString(currentValue, v);

                    List<object> oldValues = new List<object>();

                    foreach (KeyValuePair<float, Timeline.Keyframe> pair in _timeline._selectedKeyframes)
                    {
                        oldValues.Add(pair.Value.value);

                        if (v is int)
                        {
                            pair.Value.value = (int)pair.Value.value - (int)deserializedObject;
                        }
                        else if (v is float)
                        {
                            pair.Value.value = (float)pair.Value.value - (float)deserializedObject;
                        }
                        else if (v is Vector2)
                        {
                            pair.Value.value = (Vector2)pair.Value.value - (Vector2)deserializedObject;
                        }
                        else if (v is Vector3)
                        {
                            pair.Value.value = (Vector3)pair.Value.value - (Vector3)deserializedObject;
                        }
                        else if (v is Vector4)
                        {
                            pair.Value.value = (Vector4)pair.Value.value - (Vector4)deserializedObject;
                        }
                        else if (v is Color)
                        {
                            pair.Value.value = (Color)pair.Value.value - (Color)deserializedObject;
                        }
                        else if (v is Quaternion)
                        {
                            pair.Value.value = (Quaternion)deserializedObject * (Quaternion)pair.Value.value;
                        }
                    }

                    if (undoRedoTimeline.Value)
                        Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.UseCurrentValueCommand(oldValues, new List<KeyValuePair<float, Timeline.Keyframe>>(_timeline._selectedKeyframes)));

                    _timeline.UpdateKeyframeValueText();
                }
                catch (Exception e)
                {
                    ResetKeyframeValue(GetDefaultValue(v));
                    ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
                    ShalltyUtils.Logger.LogError($"Please enter a valid '{v.GetType().Name}' value. ErrorType: ({e.Message})");
                }
            }
            else
            {
                ResetKeyframeValue(GetDefaultValue(v));
                ShalltyUtils.Logger.LogMessage($"Please enter a valid '{v.GetType().Name}' value.");
            }
        }

        private static Texture2D ConvertCurveToTexture(AnimationCurve curve, Color color, int width, int height, int thickness)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] colors = new Color[width * height];

            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (float)(width - 1);
                float y = curve.Evaluate(t) * (height - 1);

                int yIndex = (int)Mathf.Clamp(y, 0, height - 1);

                for (int i = -thickness / 2; i <= thickness / 2; i++)
                {
                    int yIndexOffset = Mathf.Clamp(yIndex + i, 0, height - 1);

                    for (int j = -thickness / 2; j <= thickness / 2; j++)
                    {
                        int xIndex = Mathf.Clamp(x + j, 0, width - 1);

                        colors[yIndexOffset * width + xIndex] = color;
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            return texture;
        }

        private static List<CurvePreset> LoadCurvePresets()
        {
            string folder = Path.Combine(_defaultDir, "Curve Presets");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            List<string> files = Directory.GetFiles(folder, "*.xml").OrderBy(file => new FileInfo(file).CreationTime).ToList();

            List<CurvePreset> curves = new List<CurvePreset>();

            AnimationCurve _linePreset = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationCurve _topPreset = new AnimationCurve(new UnityEngine.Keyframe(0f, 0f, 2f, 2f), new UnityEngine.Keyframe(1f, 1f, 0f, 0f));
            AnimationCurve _bottomPreset = new AnimationCurve(new UnityEngine.Keyframe(0f, 0f, 0f, 0f), new UnityEngine.Keyframe(1f, 1f, 2f, 2f));
            AnimationCurve _hermitePreset = new AnimationCurve(new UnityEngine.Keyframe(0f, 0f, 0f, 0f), new UnityEngine.Keyframe(1f, 1f, 0f, 0f));
            AnimationCurve _stairsPreset = new AnimationCurve(new UnityEngine.Keyframe(0f, 0f, 0f, 0f), new UnityEngine.Keyframe(1f, 1f, float.PositiveInfinity, 0f));

            curves.Add(new CurvePreset("Linear Curve", _linePreset, ConvertCurveToTexture(_linePreset, Color.cyan, 128, 128, 3)));
            curves.Add(new CurvePreset("easeTop Curve", _topPreset, ConvertCurveToTexture(_topPreset, Color.cyan, 128, 128, 3)));
            curves.Add(new CurvePreset("easeBottom Curve", _bottomPreset, ConvertCurveToTexture(_bottomPreset, Color.cyan, 128, 128, 3)));
            curves.Add(new CurvePreset("Hermite Curve", _hermitePreset, ConvertCurveToTexture(_hermitePreset, Color.cyan, 128, 128, 3)));
            curves.Add(new CurvePreset("Stairs Curve", _stairsPreset, ConvertCurveToTexture(_stairsPreset, Color.cyan, 128, 128, 3)));

            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    XmlDocument document = new XmlDocument();
                    try
                    {
                        document.Load(file);

                        List<UnityEngine.Keyframe> curveKeys = new List<UnityEngine.Keyframe>();

                        foreach (XmlNode curveKeyNode in document.FirstChild.ChildNodes)
                        {
                            if (curveKeyNode.Name == "curveKeyframe")
                            {
                                UnityEngine.Keyframe curveKey = new UnityEngine.Keyframe(
                                        XmlConvert.ToSingle(curveKeyNode.Attributes["time"].Value),
                                        XmlConvert.ToSingle(curveKeyNode.Attributes["value"].Value),
                                        XmlConvert.ToSingle(curveKeyNode.Attributes["inTangent"].Value),
                                        XmlConvert.ToSingle(curveKeyNode.Attributes["outTangent"].Value));
                                curveKeys.Add(curveKey);
                            }
                        }

                        AnimationCurve curve;
                        if (curveKeys.Count == 0)
                            curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                        else
                            curve = new AnimationCurve(curveKeys.ToArray());

                        curves.Add(new CurvePreset(file, curve, ConvertCurveToTexture(curve, Color.cyan, 128, 128, 3)));
                    }
                    catch (Exception e)
                    {
                        ShalltyUtils.Logger.LogError("Could not load data for Keyframe Curve, exception:" + e.Message);
                    }
                }
            }

            return curves;
        }

        private static void SaveCurvePreset(string curveSaveName)
        {
            if (curveSaveName.IsNullOrWhiteSpace())
            {
                ShalltyUtils.Logger.LogMessage("Add a valid name for the curve!");
                return;
            }

            string folder = Path.Combine(_defaultDir, "Curve Presets");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, $"{curveSaveName}.xml");

            using (XmlTextWriter localWriter = new XmlTextWriter(file, Encoding.UTF8))
            {
                AnimationCurve curve = new AnimationCurve(_graphEditor.mainCurve.keys);

                localWriter.WriteStartElement("root");

                foreach (UnityEngine.Keyframe curveKey in curve.keys)
                {
                    localWriter.WriteStartElement("curveKeyframe");
                    localWriter.WriteAttributeString("time", XmlConvert.ToString(curveKey.time));
                    localWriter.WriteAttributeString("value", XmlConvert.ToString(curveKey.value));
                    localWriter.WriteAttributeString("inTangent", XmlConvert.ToString(curveKey.inTangent));
                    localWriter.WriteAttributeString("outTangent", XmlConvert.ToString(curveKey.outTangent));
                    localWriter.WriteEndElement();
                }

                localWriter.WriteEndElement();
            }

            UI.CreateCurvePresets();
        }

        private static void DeleteCurvePreset(string curveSaveName)
        {
            string folder = Path.Combine(_defaultDir, "Curve Presets");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, $"{curveSaveName}.xml");

            if (File.Exists(file))
            {
                File.Delete(file);
                UI.CreateCurvePresets();
            }
        }

        #endregion


        #region Keyframe Values

        public static class ToStringUtility
        {
            public static string ObjectToString(Vector2 obj) => $"x: {obj.x:F3}, y: {obj.y:F3}";

            public static string ObjectToString(Vector3 obj) => $"x: {obj.x:F3}, y: {obj.y:F3}, z: {obj.z:F3}";

            public static string ObjectToString(Vector4 obj) => $"x: {obj.x:F3}, y: {obj.y:F3}, z: {obj.z:F3}, w: {obj.w:F}";

            public static string ObjectToString(Color obj) => $"r: {obj.r:F}, g: {obj.g:F}, b: {obj.b:F}, a: {obj.a:F}";

            public static string ObjectToString(Quaternion obj) => $"x: {obj.x:F3}, y: {obj.y:F3}, z: {obj.z:F3}, w: {obj.w:F}";

            public static Vector2 StringToVector2(string str)
            {
                var array = str.Split(',');
                return new Vector2(float.Parse(array[0].Split(':')[1]), float.Parse(array[1].Split(':')[1]));
            }

            public static Vector3 StringToVector3(string str)
            {
                var array = str.Split(',');
                return new Vector3(float.Parse(array[0].Split(':')[1]), float.Parse(array[1].Split(':')[1]), float.Parse(array[2].Split(':')[1]));
            }

            public static Vector4 StringToVector4(string str)
            {
                var array = str.Split(',');
                return new Vector4(float.Parse(array[0].Split(':')[1]), float.Parse(array[1].Split(':')[1]), float.Parse(array[2].Split(':')[1]), float.Parse(array[3].Split(':')[1]));
            }

            public static Color StringToColor(string str)
            {
                var array = str.Split(',');
                return new Color(float.Parse(array[0].Split(':')[1]), float.Parse(array[1].Split(':')[1]), float.Parse(array[2].Split(':')[1]), float.Parse(array[3].Split(':')[1]));
            }

            public static Quaternion StringToQuaternion(string str)
            {
                var array = str.Split(',');
                return new Quaternion(float.Parse(array[0].Split(':')[1]), float.Parse(array[1].Split(':')[1]), float.Parse(array[2].Split(':')[1]), float.Parse(array[3].Split(':')[1]));
            }
        }

        public static void ResetKeyframeValue(object v)
        {
            if (v == null)
            {
                currentValue = "";
                return;
            }

            switch (Type.GetTypeCode(v.GetType()))
            {
                case TypeCode.Boolean:
                    currentValue = ((bool)v).ToString();
                    break;

                case TypeCode.Int32:
                    currentValue = ((int)v).ToString();
                    break;

                case TypeCode.Single:
                    currentValue = ((float)v).ToString();
                    break;
                    /*
                case TypeCode.String:
                    currentValue = (string)v;
                    break;*/

                default:
                    if (v is Vector2)
                        currentValue = ToStringUtility.ObjectToString((Vector2)v);
                    else if (v is Vector3)
                        currentValue = ToStringUtility.ObjectToString((Vector3)v);
                    else if (v is Vector4)
                        currentValue = ToStringUtility.ObjectToString((Vector4)v);
                    else if (v is Quaternion)
                        currentValue = ToStringUtility.ObjectToString((Quaternion)v);
                    else if (v is Color)
                        currentValue = ToStringUtility.ObjectToString((Color)v);
                    break;
            }

            if (keyframeValueField != null)
            {
                keyframeValueField.text = currentValue;
                keyframeValueName.text = $"Edit Value ({v.GetType().Name})";
            }
        }

        private static object GetDefaultValue(object v)
        {
            
            Type valueType = v.GetType();

            if (valueType == typeof(bool))
            {
                return (object)false;
            }
            else if (valueType == typeof(int))
            {
                return (object)0;
            }
            else if (valueType == typeof(float))
            {
                return (object)0f;
            }/*
            else if (valueType == typeof(string))
            {
                return (object)string.Empty;
            }*/
            else if (valueType == typeof(Vector2))
            {
                return (object)Vector2.zero;
            }
            else if (valueType == typeof(Vector3))
            {
                return (object)Vector3.zero;
            }
            else if (valueType == typeof(Vector4))
            {
                return (object)Vector4.zero;
            }
            else if (valueType == typeof(Quaternion))
            {
                return (object)Quaternion.identity;
            }
            else if (valueType == typeof(Color))
            {
                return (object)Color.black;
            }
            else
                return null;
            
        }

        private static object GetObjectFromString(string currentValue, object v)
        {
            object deserializedObject = v;
            switch (Type.GetTypeCode(v.GetType()))
            {
                case TypeCode.Boolean:
                    deserializedObject = bool.Parse(currentValue);
                    break;

                case TypeCode.Int32:
                    deserializedObject = int.Parse(currentValue);
                    break;

                case TypeCode.Single:
                    deserializedObject = float.Parse(currentValue);
                    break;
                    /*
                case TypeCode.String:
                    deserializedObject = currentValue;
                    break;*/

                default:
                    if (v is Vector2)
                        deserializedObject = ToStringUtility.StringToVector2(currentValue);
                    else if (v is Vector3)
                        deserializedObject = ToStringUtility.StringToVector3(currentValue);
                    else if (v is Vector4)
                        deserializedObject = ToStringUtility.StringToVector4(currentValue);
                    else if (v is Color)
                        deserializedObject = ToStringUtility.StringToColor(currentValue);
                    else if (v is Quaternion)
                        deserializedObject = ToStringUtility.StringToQuaternion(currentValue);
                    break;
            }
            return deserializedObject;
        }

        private static void OnClickRandomize()
        {
            if (_timeline._selectedKeyframes.Count == 0) return;

            List<KeyValuePair<float, Timeline.Keyframe>> selectedKeyframes = new List<KeyValuePair<float, Timeline.Keyframe>>(_timeline._selectedKeyframes);
            List<KeyValuePair<float, Timeline.Keyframe>> toSelect = new List<KeyValuePair<float, Timeline.Keyframe>>();

            if (!float.TryParse(inputFieldRandom.text, out float randomnessValue))
            {
                inputFieldRandom.text = "0.001";
                return;
            }

            if (toggleTimeRandom.isOn)
            {
                List<float> oldTimes = new List<float>();
                List<float> newTimes = new List<float>();

                _timeline._selectedKeyframes.Clear();
                foreach (KeyValuePair<float, Timeline.Keyframe> pair in selectedKeyframes)
                {
                    float oldTime = pair.Key;
                    float newTime = (float)RandomizeObjectValue(oldTime, randomnessValue);
                    Interpolable interpolable = pair.Value.parent;
                    interpolable.keyframes.Remove(oldTime);

                    while (interpolable.keyframes.ContainsKey(newTime) || newTime < 0 || newTime > _timeline._duration)
                    {
                        newTime = (float)RandomizeObjectValue(oldTime, randomnessValue);
                    }

                    interpolable.keyframes.Add(newTime, pair.Value);
                    toSelect.Add(new KeyValuePair<float, Timeline.Keyframe>(newTime, pair.Value));

                    oldTimes.Add(oldTime);
                    newTimes.Add(newTime);
                }

                if (undoRedoTimeline.Value)
                    Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.DragAtCurrentTimeCommand(newTimes, oldTimes, toSelect));

                _timeline.SelectKeyframes(toSelect.ToArray());
            }

            

            if (toggleValueRandom.isOn)
            {
                List<object> oldValues = new List<object>();

                foreach (KeyValuePair<float, Timeline.Keyframe> pair in selectedKeyframes)
                {
                    oldValues.Add(pair.Value.value);
                    pair.Value.value = RandomizeObjectValue(pair.Value.value, randomnessValue);
                }

                if (undoRedoTimeline.Value)
                    Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.UseCurrentValueCommand(oldValues, new List<KeyValuePair<float, Timeline.Keyframe>>(_timeline._selectedKeyframes)));
            }

            _timeline.UpdateGrid();
            _timeline.UpdateKeyframeWindow();
        }

        private static object RandomizeObjectValue(object obj, float tolerance)
        {
            if (obj is Vector3)
            {
                Vector3 v = (Vector3)obj;
                float offsetX = Random.Range(-tolerance, tolerance);
                float offsetY = Random.Range(-tolerance, tolerance);
                float offsetZ = Random.Range(-tolerance, tolerance);
                obj = new Vector3(v.x + offsetX, v.y + offsetY, v.z + offsetZ);
            }
            else if (obj is Vector2)
            {
                Vector3 v = (Vector2)obj;
                float offsetX = Random.Range(-tolerance, tolerance);
                float offsetY = Random.Range(-tolerance, tolerance);
                obj = new Vector2(v.x + offsetX, v.y + offsetY);
            }
            else if (obj is Vector4)
            {
                Vector4 v = (Vector4)obj;
                float offsetX = Random.Range(-tolerance, tolerance);
                float offsetY = Random.Range(-tolerance, tolerance);
                float offsetZ = Random.Range(-tolerance, tolerance);
                float offsetW = Random.Range(-tolerance, tolerance);
                obj = new Vector4(v.x + offsetX, v.y + offsetY, v.z + offsetZ, v.w + offsetW);
            }
            else if (obj is Quaternion)
            {
                Quaternion q = (Quaternion)obj;
                float offsetAngleX = Random.Range(-tolerance, tolerance);
                float offsetAngleY = Random.Range(-tolerance, tolerance);
                float offsetAngleZ = Random.Range(-tolerance, tolerance);
                obj = Quaternion.Euler(q.eulerAngles + new Vector3(offsetAngleX, offsetAngleY, offsetAngleZ));
            }
            else if (obj is Color)
            {
                Color c = (Color)obj;
                float offsetR = Random.Range(-tolerance, tolerance);
                float offsetG = Random.Range(-tolerance, tolerance);
                float offsetB = Random.Range(-tolerance, tolerance);
                obj = new Color(c.r + offsetR, c.g + offsetG, c.b + offsetB);
            }
            else if (obj is float)
            {
                float f = (float)obj;
                float offsetF = Random.Range(-tolerance, tolerance);
                obj = f + offsetF;
            }
            else if (obj is int)
            {
                int i = (int)obj;
                int offsetI = Mathf.RoundToInt(Random.Range(-tolerance, tolerance));
                obj = i + offsetI;
            }
            else
            {
                ShalltyUtils.Logger.LogError("RandomizeObjectValue: Unknown type");
            }
            return obj;
        }

        #endregion


        public static void CreateTooltip(GameObject gameObject, string text, float sizeX = 130f, float sizeY = 60f, float delay = 1f) // Added optional delay parameter
        {
            PointerEnterHandler pointerEnter = gameObject.GetOrAddComponent<PointerEnterHandler>();

            pointerEnter.onPointerEnter = (e) =>
            {
                if (!displayTooltipsTimeline.Value) return;

                tooltipCoroutine = _self.StartCoroutine(ShowTooltipCoroutine(gameObject, text, sizeX, sizeY, delay));
            };

            pointerEnter.onPointerExit = (e) =>
            {
                if (!displayTooltipsTimeline.Value) return;

                // Stop timer if mouse leaves before delay finishes
                _self.StopCoroutine(tooltipCoroutine);

                // Optionally hide the tooltip immediately on exit (uncomment if desired)
                _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                ((RectTransform)_timeline._tooltip.transform.parent).sizeDelta = new Vector2(130f, 60f);
            };
        }

        private static IEnumerator ShowTooltipCoroutine(GameObject gameObject, string text, float sizeX, float sizeY, float delay)
        {
            yield return new WaitForSeconds(delay);

            _timeline._tooltip.transform.parent.gameObject.SetActive(true);
            _timeline._tooltip.text = text;
            ((RectTransform)_timeline._tooltip.transform.parent).sizeDelta = new Vector2(sizeX, sizeY);
        }

    }
}