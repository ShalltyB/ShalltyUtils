extern alias aliasTimeline;
using aliasTimeline.UILib;
using aliasTimeline.UILib.EventHandlers;
using aliasTimeline.UILib.ContextMenu;
using HarmonyLib;
using HSPE;
using Studio;
using System.Collections.Generic;
using System.Linq;
using ToolBox.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HSPE.PoseController;
using static ShalltyUtils.ShalltyUtils;
using Timeline;
using Vectrosity;
using System.Collections;
using System.Xml;
using System.IO;
using System;
using Random = UnityEngine.Random;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using System.Text;

namespace ShalltyUtils
{
    public class GuideObjectPicker
    {
        public static List<Vector3> lastGuideObjectPos = new List<Vector3>();
        public static List<Quaternion> lastGuideObjectRot = new List<Quaternion>();

        public static readonly Dictionary<int, Vector3> _oldRotValues = new Dictionary<int, Vector3>();
        public static readonly Dictionary<int, Vector3> _oldPosValues = new Dictionary<int, Vector3>();
        public static List<GuideCommand.EqualsInfo> _additionalRotationEqualsCommands = new List<GuideCommand.EqualsInfo>();

        private static ScrollRect goPickerScrollview;

        public static DragType _currentDragType = DragType.None;
        public static bool _lockDrag = false;

        public static bool ignoreChildren = false;
        public static bool moveTimeline = false;
        public static bool showNodes = true;

        public static List<PickerPage> goPickerPages = new List<PickerPage>();
        public static List<PickerButton> goPickerButtons = new List<PickerButton>();
        private static List<Button> goPickerCurrentButtons = new List<Button>();
        private static List<Button> goPickerCurrentPagesButtons = new List<Button>();

        public static bool goPickerSelecting;
        public static Vector2 goPickerSelectFirstPoint;
        public static RectTransform goPickerSelectionArea;
        private static Toggle goPickerToggle;
        private static Image goPickerSelectionImage;

        private static Texture nodeDefaultTexture;

        private static VectorLine goPickerNodesLine;
        private static VectorLine gridLine;
        private static ScrollRect goPickerButtonsScroll;
        private static float currentScale = 1f;
        private static Image editButtonsPanel;
        private static InputField editButtonsWidth;
        private static InputField editButtonsHeight;
        private static InputField editButtonsPosX;
        private static InputField editButtonsPosY;

        private static Dictionary<PickerButton, Vector2> dragButtonsOffset = new Dictionary<PickerButton, Vector2>();
        private static Dictionary<PickerButton, Vector2> copyButtonsOffset = new Dictionary<PickerButton, Vector2>();

        private static Dictionary<int, KeyValuePair<PickerButton, PickerPage>> nodesLineDictionary = new Dictionary<int, KeyValuePair<PickerButton, PickerPage>>();

        private static KeyCode selectionKey = KeyCode.Mouse0;
        private static Vector2 startPos;
        private static Vector2 endPos;
        private static Rect selectionRect;
        private static bool isSelecting;
        private static InputField editButtonsName;
        private static bool isHovering = false;

        public static bool showTooltip = false;
        public static string tooltipText = "Tooltip";

        public class PickerButton
        {
            // Saved vars
            public string originalTransform;
            public string buttonText;
            public Color buttonColor;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 offsetMin;
            public Vector2 offsetMax;

            // Temporary vars
            public GuideObject guideObject;
            public Button button;

            public PickerButton(string buttonText, Color buttonColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, GuideObject guideObject, string transformName = "$$null$$")
            {
                if (guideObject != null)
                {
                    ObjectCtrlInfo goOci = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, guideObject)).Select(pair => pair.Value).FirstOrDefault();
                    if (goOci == null || !(goOci is OCIFolder))
                    {
                        this.originalTransform = guideObject.transformTarget.name;
                    }
                    else if (goOci is OCIFolder ociFolder)
                    {
                        this.originalTransform = ociFolder.treeNodeObject.textName;
                    }
                }
                else
                {
                    this.originalTransform = "$$null$$";
                }

                if (transformName != "$$null$$")
                    this.originalTransform = transformName;

                this.buttonText = buttonText;
                this.buttonColor = buttonColor;
                this.anchorMin = anchorMin;
                this.anchorMax = anchorMax;
                this.offsetMin = offsetMin;
                this.offsetMax = offsetMax;
                this.guideObject = guideObject;
            }

            public PickerButton(PickerButton pickerButton)
            {
                this.originalTransform = pickerButton.originalTransform;
                this.buttonText = pickerButton.buttonText;
                this.buttonColor = pickerButton.buttonColor;
                this.anchorMin = pickerButton.anchorMin;
                this.anchorMax = pickerButton.anchorMax;
                this.offsetMin = pickerButton.offsetMin;
                this.offsetMax = pickerButton.offsetMax;
                this.guideObject = pickerButton.guideObject;
            }


            public void UpdateColor()
            {
                if (button == null) return;

                if ( guideObject == null || Singleton<GuideObjectManager>.Instance.selectObjects == null || Singleton<GuideObjectManager>.Instance.selectObjects.Length == 0)
                {
                    button.image.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.5f);
                    button.GetComponentInChildren<InputField>(true).textComponent.color = button.image.color.GetContrastingColor();
                    return;
                }

                button.image.color = Singleton<GuideObjectManager>.Instance.selectObjects.Contains(guideObject) ? buttonColor : new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.5f);
                button.GetComponentInChildren<InputField>(true).textComponent.color = button.image.color.GetContrastingColor();
            }
        }

        public class PickerPage
        {
            public bool isSelected;
            public bool showNodes;
            public string pageText;
            public Color pageColor;

            public List<PickerButton> pageButtons;
            public Button button;

           public PickerPage(string pageText, Color pageColor, List<PickerButton> pageButtons, bool showNodes)
            {
                this.isSelected = false;
                this.showNodes = showNodes;
                this.pageText = pageText;
                this.pageColor = pageColor;
                this.pageButtons = pageButtons;
            }

            public PickerPage(PickerPage pickerPage)
            {
                this.isSelected = false;
                this.showNodes = pickerPage.showNodes;
                this.pageText = pickerPage.pageText;
                this.pageColor = pickerPage.pageColor;
            }

            public void SelectPage()
            {
                if (goPickerPages == null) goPickerPages = new List<PickerPage>();

                foreach (PickerPage page in goPickerPages)
                {
                    if (page == null) continue;

                    page.isSelected = false;

                    if (button == null) continue;
                    page.button.image.color = page.isSelected ? new Color(page.pageColor.r, page.pageColor.g, page.pageColor.b, 0.2f) : page.pageColor;

                    foreach (Text text in page.button.GetComponentsInChildren<Text>(true))
                        text.color = page.button.image.color.GetContrastingColor();
                }

                if (pageButtons == null)
                    pageButtons = new List<PickerButton>();

                goPickerButtons = pageButtons;
                isSelected = true;

                if (button == null) return;

                button.image.color = isSelected ? new Color(pageColor.r, pageColor.g, pageColor.b, 0.2f) : pageColor;

                foreach (Text text in button.GetComponentsInChildren<Text>(true))
                    text.color = button.image.color.GetContrastingColor();

                _self.ExecuteDelayed2(() =>
                {
                    UpdateAllColors();
                });
            }
        }

        public static void Init()
        {
            // GuideObject Picker

            var goPickerGameObject = GameObject.Instantiate(_kkpeWindow._ui.transform.Find("BG/Top Container/Buttons/FK"), _kkpeWindow._ui.transform.Find("BG/Top Container/Buttons"));
            goPickerGameObject.name = "goPickerToggle";
            goPickerGameObject.GetComponentInChildren<Text>().text = "GuideObject Picker";
            goPickerToggle = goPickerGameObject.GetOrAddComponent<Toggle>();
            goPickerToggle.transform.SetRect(0f, 1f, 1f, 2.5f, 0f, 0f, 0f, 0f);


            var goPickerPanel = UIUtility.CreatePanel("goPickerPanel", _kkpeWindow._ui.transform.Find("BG/Controls"));
            goPickerPanel.transform.SetRect(0f, 0.33f, 0.75f, 1f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(goPickerPanel.transform, Color.black);
            goPickerPanel.gameObject.SetActive(false);


            goPickerScrollview = UIUtility.CreateScrollView("goPickerScrollview", goPickerPanel.transform);

            UIUtility.AddOutlineToObject(goPickerScrollview.transform);
            goPickerScrollview.transform.SetRect(0f, 0f, 1f, 1f, 5f, 5f, -5f, -5f);
            goPickerScrollview.inertia = false;
            goPickerScrollview.scrollSensitivity = 40f;
            goPickerScrollview.movementType = ScrollRect.MovementType.Clamped;
            goPickerScrollview.content.SetRect(0f, 0f, 0f, 0f);
            goPickerScrollview.content.sizeDelta = new Vector2(2000f, 2000f);
            goPickerScrollview.viewport.gameObject.AddComponent<PointerDownHandler>().onPointerDown = OnPickerMouseDown;
            goPickerScrollview.gameObject.AddComponent<PickerDragHandler>();
            goPickerScrollview.viewport.gameObject.AddComponent<ScrollHandler>().onScroll = e =>
            {
                float maxValue = 1f;
                float minValue = 0.15f;
                float modifierValue = 0.05f;

                if (e.scrollDelta.y != 0)
                {
                    // Zoom
                    float zoomFactor = 1f - (e.scrollDelta.y > 0 ? -modifierValue : modifierValue);
                    currentScale = Mathf.Clamp(currentScale * zoomFactor, minValue, maxValue);
                    goPickerScrollview.content.localScale = new Vector3(currentScale, currentScale, 1f);

                    RectTransform rectTransform = goPickerScrollview.content;

                    var _startPinchScreenPosition = (Vector2)Input.mousePosition;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, _startPinchScreenPosition, null, out var _startPinchCenterPosition);
                    Vector2 pivotPosition = new Vector3(rectTransform.pivot.x * rectTransform.rect.size.x, rectTransform.pivot.y * rectTransform.rect.size.y);
                    Vector2 posFromBottomLeft = pivotPosition + _startPinchCenterPosition;

                    
                    Vector2 pivot = new Vector2(posFromBottomLeft.x / rectTransform.rect.width, posFromBottomLeft.y / rectTransform.rect.height);

                    Vector2 size = rectTransform.rect.size;
                    Vector2 deltaPivot = rectTransform.pivot - pivot;
                    Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y) * rectTransform.localScale.x;
                    rectTransform.pivot = pivot;
                    rectTransform.localPosition -= deltaPosition;

                    

                    GenerateGrid();
                }

                e.Reset();
            };



            goPickerSelectionImage = UIUtility.CreatePanel("goPickerSelection", goPickerScrollview.content.transform);
            goPickerSelectionImage.color = new Color(0f, 1f, 1f, 0.45f);
            goPickerSelectionArea = (RectTransform)goPickerSelectionImage.transform;
            goPickerSelectionArea.SetRect(0f, 0f, 0f, 0f);

            /*

            // TOGGLE: IGNORE CHILDREN

            _kkpeWindow._optionsWindow.sizeDelta = new Vector2(200f, 300f);
            var goPickerIgnoreChildren = GameObject.Instantiate(_kkpeWindow._ui.transform.Find("BG/Controls/Buttons/Simple Options/Optimize IK Container"), _kkpeWindow._ui.transform.Find("Options Window/Options"));
            goPickerIgnoreChildren.name = "goPickerIgnoreChildren";
            goPickerIgnoreChildren.GetComponentInChildren<Text>().text = "Ignore Children";
            goPickerIgnoreChildren.GetOrAddComponent<LayoutElement>().preferredHeight = 30f;

            var goPickerIgnoreChildrenToggle = goPickerIgnoreChildren.transform.GetComponentInChildren<Toggle>();
            goPickerIgnoreChildrenToggle.isOn = false;
            goPickerIgnoreChildrenToggle.onValueChanged.AddListener((b) =>{ ignoreChildren = goPickerIgnoreChildrenToggle.isOn; });

            // TOGGLE: MOVE TIMELINE

            var goPickerMoveTimeline = GameObject.Instantiate(_kkpeWindow._ui.transform.Find("BG/Controls/Buttons/Simple Options/Optimize IK Container"), _kkpeWindow._ui.transform.Find("Options Window/Options"));
            goPickerMoveTimeline.name = "goPickerMoveTimeline";
            goPickerMoveTimeline.GetComponentInChildren<Text>().text = "Move Timeline";
            goPickerMoveTimeline.GetOrAddComponent<LayoutElement>().preferredHeight = 30f;

            var goPickerMoveTimelineToggle = goPickerMoveTimeline.transform.GetComponentInChildren<Toggle>();
            goPickerMoveTimelineToggle.isOn = false;
            goPickerMoveTimelineToggle.onValueChanged.AddListener((b) => { moveTimeline = goPickerMoveTimelineToggle.isOn; });

            */

            // TOGGLE: SHOW NODES
            
            var goPickerShowNodes = GameObject.Instantiate(_kkpeWindow._ui.transform.Find("BG/Controls/Buttons/Simple Options/Optimize IK Container"), _kkpeWindow._ui.transform.Find("Options Window/Options"));
            goPickerShowNodes.name = "goPickerShowNodes";
            goPickerShowNodes.GetComponentInChildren<Text>().text = "Show Nodes";
            goPickerShowNodes.GetOrAddComponent<LayoutElement>().preferredHeight = 30f;

            var goPickerShowNodesToggle = goPickerShowNodes.transform.GetComponentInChildren<Toggle>();
            goPickerShowNodesToggle.isOn = true;
            goPickerShowNodesToggle.onValueChanged.AddListener((b) => 
            { 
                if (!goPickerHideWithAxis.Value)
                    showNodes = Singleton<Studio.Studio>.Instance.workInfo.visibleAxis;
                else
                    showNodes = goPickerShowNodesToggle.isOn;

                if (showNodes)
                {
                    _self.ExecuteDelayed2(() =>
                    {
                        UpdateAllColors();
                    });
                }
            });


            var goPickerButtonsPanel = UIUtility.CreatePanel("goPickerButtonsPanel", _kkpeWindow._ui.transform.Find("BG/Controls/Other buttons"));
            goPickerButtonsPanel.transform.SetRect();
            UIUtility.AddOutlineToObject(goPickerButtonsPanel.transform, Color.black);
            goPickerButtonsPanel.gameObject.SetActive(false);

            goPickerButtonsScroll = UIUtility.CreateScrollView("goPickerButtonsScroll", goPickerButtonsPanel.transform);
            goPickerButtonsScroll.transform.SetRect();
            goPickerButtonsScroll.content.transform.SetRect();
            goPickerButtonsScroll.inertia = false;
            goPickerButtonsScroll.scrollSensitivity = 40f;
            goPickerButtonsScroll.movementType = ScrollRect.MovementType.Clamped;
            goPickerButtonsScroll.viewport.gameObject.AddComponent<PointerDownHandler>().onPointerDown = (eventData) =>
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_kkpeWindow._ui.transform, eventData.position, eventData.pressEventCamera, out Vector2 selectPoint))
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)goPickerScrollview.content.transform, eventData.position, eventData.pressEventCamera, out Vector2 mousePoint);
                        List<AContextMenuElement> elements = new List<AContextMenuElement>
                        {
                        new LeafElement()
                        {
                            text = "Add page",
                            onClick = p =>
                            {
                                PickerPage newPage = new PickerPage("New page", Color.white, new List<PickerButton>(), false);
                                goPickerPages.Add(newPage);
                                newPage.SelectPage();
                                UpdatePickerPages();
                            }
                        },
                        new LeafElement()
                        {
                            text = "Load page file",
                            onClick = p =>
                            {
                                LoadSinglePageFile();
                            }
                        },
                        new LeafElement()
                        {
                            text = "Cancel",
                            onClick = p =>
                            {
                                UIUtility.HideContextMenu();
                            }
                        }
                    };
                        UIUtility.ShowContextMenu(_kkpeWindow._ui, selectPoint, elements);
                    }
                    return;
                }

                if (UIUtility.IsContextMenuDisplayed())
                    UIUtility.HideContextMenu();
            };
            

            var goPickerButtonsScrollLayout = goPickerButtonsScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
            goPickerButtonsScrollLayout.padding = new RectOffset(2, 2, 2, 2);
            goPickerButtonsScrollLayout.childForceExpandHeight = false;

            goPickerToggle.onValueChanged.AddListener((b) =>
            {
                goPickerPanel.gameObject.SetActive(goPickerToggle.isOn);
                goPickerButtonsPanel.gameObject.SetActive(goPickerToggle.isOn);

                _kkpeWindow._fkBonesButtons.gameObject.SetActive(false);
                _kkpeWindow._ikBonesButtons.gameObject.SetActive(false);
                _kkpeWindow._currentModeIK = false;
                _kkpeWindow._controls.gameObject.SetActive(true);
                GenerateGrid();
            });

            #region Edit Buttons

            editButtonsPanel = UIUtility.CreatePanel("editButtonsPanel", _kkpeWindow._ui.transform.Find("BG"));
            editButtonsPanel.transform.SetRect(0f, 0f, 0f, 0f, 400f, 0f, 650f, 250f);
            UIUtility.AddOutlineToObject(editButtonsPanel.transform, Color.black);
            editButtonsPanel.gameObject.SetActive(false);

            var editButtonsDrag = UIUtility.CreatePanel("editButtonsDrag", editButtonsPanel.transform);
            editButtonsDrag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            editButtonsDrag.color = Color.gray;
            UIUtility.MakeObjectDraggable(editButtonsPanel.rectTransform, editButtonsPanel.rectTransform);

            var editButtonsTitle = UIUtility.CreateText("editButtonsTitle", editButtonsDrag.transform, "Edit Button(s)");
            editButtonsTitle.transform.SetRect();
            editButtonsTitle.alignment = TextAnchor.MiddleCenter;

            var editButtonsClose = UIUtility.CreateButton("CloseButton", editButtonsDrag.transform, "X");
            editButtonsClose.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            editButtonsClose.onClick.AddListener(() => { editButtonsPanel.gameObject.SetActive(false); });

            var editButtonsFieldsPanel = UIUtility.CreatePanel("editButtonsFieldsPanel", editButtonsPanel.transform);
            editButtonsFieldsPanel.transform.SetRect(0.05f, 0.05f, 0.95f, 0.9f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(editButtonsFieldsPanel.transform);
            var editButtonsFieldsPanelLayout = editButtonsFieldsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            editButtonsFieldsPanelLayout.spacing = 15f;
            editButtonsFieldsPanelLayout.childForceExpandHeight = false;
            editButtonsFieldsPanelLayout.padding = new RectOffset(20, 20, 20, 20);
            editButtonsFieldsPanelLayout.childAlignment = TextAnchor.MiddleCenter;



            // FIELD: CHANGE WIDTH

            var editButtonsWidthLabel = UIUtility.CreateText("editButtonsWidthLabel", editButtonsFieldsPanel.transform, "Width:");
            editButtonsWidthLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var editButtonsWidthLabelLayout = editButtonsWidthLabel.gameObject.AddComponent<LayoutElement>();
            editButtonsWidthLabelLayout.preferredHeight = 100f;

            editButtonsWidth = UIUtility.CreateInputField("editButtonsWidth", editButtonsWidthLabel.transform, "");
            editButtonsWidth.transform.SetRect(0.75f, 0.5f, 0.75f, 0.5f, -40f, -15f, 40f, 15f);
            editButtonsWidth.contentType = InputField.ContentType.DecimalNumber;
            editButtonsWidth.textComponent.alignment = TextAnchor.MiddleCenter;
            editButtonsWidth.text = "1";
            editButtonsWidth.caretWidth = 3;
            ((Text)editButtonsWidth.placeholder).alignment = TextAnchor.MiddleCenter;


            // FIELD: CHANGE HEIGTH

            var editButtonsHeightLabel = UIUtility.CreateText("editButtonsHeightLabel", editButtonsFieldsPanel.transform, "Height:");
            editButtonsHeightLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var editButtonsHeightLabelLayout = editButtonsHeightLabel.gameObject.AddComponent<LayoutElement>();
            editButtonsHeightLabelLayout.preferredHeight = 100f;

            editButtonsHeight = UIUtility.CreateInputField("editButtonsHeight", editButtonsHeightLabel.transform, "");
            editButtonsHeight.transform.SetRect(0.75f, 0.5f, 0.75f, 0.5f, -40f, -15f, 40f, 15f);
            editButtonsHeight.contentType = InputField.ContentType.DecimalNumber;
            editButtonsHeight.textComponent.alignment = TextAnchor.MiddleCenter;
            editButtonsHeight.text = "1";
            editButtonsHeight.caretWidth = 3;
            ((Text)editButtonsHeight.placeholder).alignment = TextAnchor.MiddleCenter;


            // FIELD: CHANGE POSITION X

            var editButtonsPosXLabel = UIUtility.CreateText("editButtonsPosXLabel", editButtonsFieldsPanel.transform, "X:");
            editButtonsPosXLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var editButtonsPosXLabelLayout = editButtonsPosXLabel.gameObject.AddComponent<LayoutElement>();
            editButtonsPosXLabelLayout.preferredHeight = 100f;

            editButtonsPosX = UIUtility.CreateInputField("editButtonsPosX", editButtonsPosXLabel.transform, "");
            editButtonsPosX.transform.SetRect(0.75f, 0.5f, 0.75f, 0.5f, -40f, -15f, 40f, 15f);
            editButtonsPosX.contentType = InputField.ContentType.DecimalNumber;
            editButtonsPosX.textComponent.alignment = TextAnchor.MiddleCenter;
            editButtonsPosX.text = "0";
            editButtonsPosX.caretWidth = 3;
            ((Text)editButtonsPosX.placeholder).alignment = TextAnchor.MiddleCenter;

            // FIELD: CHANGE POSITION Y

            var editButtonsPosYLabel = UIUtility.CreateText("editButtonsPosYLabel", editButtonsFieldsPanel.transform, "Y:");
            editButtonsPosYLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var editButtonsPosYLabelLayout = editButtonsPosYLabel.gameObject.AddComponent<LayoutElement>();
            editButtonsPosYLabelLayout.preferredHeight = 100f;

            editButtonsPosY = UIUtility.CreateInputField("editButtonsPosY", editButtonsPosYLabel.transform, "");
            editButtonsPosY.transform.SetRect(0.75f, 0.5f, 0.75f, 0.5f, -40f, -15f, 40f, 15f);
            editButtonsPosY.contentType = InputField.ContentType.DecimalNumber;
            editButtonsPosY.textComponent.alignment = TextAnchor.MiddleCenter;
            editButtonsPosY.text = "0";
            editButtonsPosY.caretWidth = 3;
            ((Text)editButtonsPosY.placeholder).alignment = TextAnchor.MiddleCenter;


            // FIELD: CHANGE NAME
            
            var editButtonsNameLabel = UIUtility.CreateText("editButtonsNameLabel", editButtonsFieldsPanel.transform, "Link:");
            editButtonsNameLabel.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -120f, -95f, 280f, -65f);
            var editButtonsNameLabelLayout = editButtonsNameLabel.gameObject.AddComponent<LayoutElement>();
            editButtonsNameLabelLayout.preferredHeight = 100f;

            editButtonsName = UIUtility.CreateInputField("editButtonsName", editButtonsNameLabel.transform, "");
            editButtonsName.transform.SetRect(0.55f, 0.5f, 0.75f, 0.5f, -40f, -15f, 40f, 15f);
            editButtonsName.contentType = InputField.ContentType.Standard;
            editButtonsName.textComponent.alignment = TextAnchor.MiddleCenter;
            editButtonsName.text = "";
            editButtonsName.caretWidth = 3;
            ((Text)editButtonsName.placeholder).alignment = TextAnchor.MiddleCenter;
            

            // BUTTON: APPLY CHANGES

            var editButtonsApply = UIUtility.CreateButton("editButtonsApply", editButtonsFieldsPanel.transform, "Apply to Selected");
            editButtonsApply.transform.SetRect(0.5f, 0.5f, 0.5f, 0.5f, -130f, -285f, 130f, -245f);
            var editButtonsApplyLayout = editButtonsApply.gameObject.AddComponent<LayoutElement>();
            editButtonsApplyLayout.preferredHeight = 120f;
            editButtonsApply.onClick.AddListener(() =>
            {

                List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
                if (selectedPickerButtons.Count > 0)
                {
                    int index = 0;
                    foreach (PickerButton picker in selectedPickerButtons)
                    {
                        if (picker.button == null) continue;

                        RectTransform rect = (RectTransform)picker.button.transform;

                        rect.sizeDelta = new Vector2(float.Parse(editButtonsWidth.text), float.Parse(editButtonsHeight.text));

                        if (selectedPickerButtons.Count == 1)
                        {
                            rect.anchoredPosition = new Vector2(float.Parse(editButtonsPosX.text), float.Parse(editButtonsPosY.text));

                            GuideObject guideObject = picker.guideObject;
                            if (guideObject != null && editButtonsName.text.IsNullOrWhiteSpace())
                            {
                                ObjectCtrlInfo goOci = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, guideObject)).Select(pair => pair.Value).FirstOrDefault();
                                if (goOci == null || !(goOci is OCIFolder))
                                {
                                    picker.originalTransform = guideObject.transformTarget.name;
                                }
                                else if (goOci is OCIFolder ociFolder)
                                {
                                    picker.originalTransform = ociFolder.treeNodeObject.textName;
                                }
                            }
                            else
                            {
                                picker.originalTransform = editButtonsName.text;
                            }
                        }

                        picker.anchorMin = rect.anchorMin;
                        picker.anchorMax = rect.anchorMax;
                        picker.offsetMin = rect.offsetMin;
                        picker.offsetMax = rect.offsetMax;



                        index++;
                    }
                }

            });


            #endregion


            gridLine = new VectorLine("guideObjectPickerGridLine", new List<Vector2> { Vector2.zero, Vector2.one }, 2f, LineType.Discrete, Joins.None)
            {
                active = false,
                color = new Color(1f, 1f, 1f, 0.5f)
            };

            goPickerNodesLine = new VectorLine("guideObjectPickerNodes", new List<Vector3> { Vector3.zero }, 20f, LineType.Points);
            goPickerNodesLine.color = Color.white;

            nodeDefaultTexture = ResourceUtils.GetEmbeddedResource("guideObjectPickerNode.png").LoadTexture();
            LoadNodesTexture();

            goPickerNodesLine.active = false;

            _self.StartCoroutine(AfterInit());
        }

        public static void LoadNodesTexture()
        {
            Texture nodeTex = nodeDefaultTexture;
            if (!goPickerNodeTexture.Value.IsNullOrEmpty() && File.Exists(goPickerNodeTexture.Value))
            {
                try
                {
                    nodeTex = File.ReadAllBytes(goPickerNodeTexture.Value)?.LoadTexture();
                }
                catch { }

                if (nodeTex == null)
                    nodeTex = nodeDefaultTexture;
            }

            goPickerNodesLine.texture = nodeTex;
        }



        private static IEnumerator AfterInit()
        {
            yield return null;

            goPickerScrollview.enabled = false;
            goPickerScrollview.horizontalScrollbar.gameObject.SetActive(false);
            goPickerScrollview.verticalScrollbar.gameObject.SetActive(false);

            yield return null;

            GenerateGrid();
            UpdatePickerPages();

            goPickerScrollview.normalizedPosition = new Vector2(Mathf.Clamp(goPickerScrollview.normalizedPosition.x, 0.0f, 1.0f), Mathf.Clamp(goPickerScrollview.normalizedPosition.y, 0.0f, 1.0f));
        }

        // OnGUI
        public static void DrawSelectionRect()
        {
            if (showTooltip && goPickerEnableTooltip.Value)
            {
                // Calculate the tooltip position based on mouse position
                Vector2 mousePos = Event.current.mousePosition;

                GUIStyle style = IMGUIUtils.SolidBackgroundGuiSkin.box;
                var alignment = style.alignment;
                var wordWrap = style.wordWrap;
                var richText = style.richText;

                style.alignment = TextAnchor.MiddleCenter;
                style.wordWrap = true;
                style.richText = true;

                Vector2 textSize = style.CalcSize(new GUIContent(tooltipText));

                // Create a tooltip rect with a fixed padding
                const float padding = 10f;
                Rect tooltipRect = new Rect(mousePos.x + padding, mousePos.y + padding, textSize.x + padding * 2, textSize.y + padding * 2);

                // Draw the tooltip box with the centered text
                GUI.Box(tooltipRect, tooltipText, style);

                style.alignment = alignment;
                style.wordWrap = wordWrap;
                style.richText = richText;
            }


            if (isSelecting)
            {
                GUI.color = new Color(0f, 1f, 1f, 0.5f);
               
                GUI.Box(selectionRect, "", IMGUIUtils.SolidBackgroundGuiSkin.box);

                GUI.color = Color.white;
            }
        }

        private static void StartSelection()
        {
            Transform kkpeUI = _kkpeWindow._ui.transform.Find("BG");
            Transform timelineUI = _timeline._ui.transform.Find("Timeline Window");

            // Don't allow selection if the mouse is over the Timeline UI or the KKPE UI
            if (timelineUI != null && timelineUI.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)timelineUI, Input.mousePosition) || (kkpeUI != null && kkpeUI.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)kkpeUI, Input.mousePosition))) return;

            startPos = Input.mousePosition;
            isSelecting = true;
        }
        private static void UpdateSelection()
        {
            endPos = Input.mousePosition;
            selectionRect = new Rect(
                Mathf.Min(startPos.x, endPos.x),
                Mathf.Min(Screen.height - startPos.y, Screen.height - endPos.y),
                Mathf.Abs(startPos.x - endPos.x),
                Mathf.Abs(startPos.y - endPos.y)
            );
        }

        private static void EndSelection()
        {
            if (!isSelecting) return;

            isSelecting = false;

            List<GuideObject> toSelect = new List<GuideObject>();

            for (int i = 0; i < goPickerNodesLine.points3.Count; i++)
            {
                Vector3 point = goPickerNodesLine.points3[i];
                Vector3 screenPoint = Camera.main.WorldToScreenPoint(point);

                // Convert from pixels to points
                float dpi = Screen.dpi;
                float pixelsPerPoint = dpi / 96f; // Assuming 96 DPI as the standard reference
                Vector2 screenPointInPoints = new Vector2(screenPoint.x / pixelsPerPoint, screenPoint.y / pixelsPerPoint);

                // Invert Y-axis to match GUI space
                Vector2 invertedScreenPointInPoints = new Vector2(screenPointInPoints.x, Screen.height / pixelsPerPoint - screenPointInPoints.y);

                Vector2 finalScreenPointInPointsWithOffset = invertedScreenPointInPoints;

                if (selectionRect.Contains(finalScreenPointInPointsWithOffset))
                {
                    if (nodesLineDictionary.TryGetValue(i, out var pair))
                    {
                        toSelect.Add(pair.Key.guideObject);
                    }
                }
            }

            if (!Input.GetKey(KeyCode.LeftControl))
            {
                foreach (TreeNodeObject _node in Singleton<TreeNodeCtrl>.Instance.hashSelectNode)
                    _node.OnDeselect();
                Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Clear();

                foreach (GuideObject go in new HashSet<GuideObject>(Singleton<GuideObjectManager>.Instance.hashSelectObject))
                    Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);
            }

            if (toSelect.Count > 0)
            {
                Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                foreach (GuideObject go in toSelect)
                {
                    bool hasParent = go.parentGuide != null;

                    TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? go : go.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                    if (node == null) return;

                    if (hasParent)
                    {
                        if (!Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(go))
                            Singleton<GuideObjectManager>.Instance.AddSelectMultiple(go);
                        else
                            Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);
                    }
                    else
                    {
                        Singleton<TreeNodeCtrl>.Instance.AddSelectNode(node, true);
                    }
                }


                GuideObject lastGuideObject = toSelect.Last();

                Singleton<GuideObjectManager>.Instance.StopSelectObject();
                lastGuideObject.isActive = true;
                _self.ExecuteDelayed2(() => { lastGuideObject.SetLayer(lastGuideObject.gameObject, LayerMask.NameToLayer("Studio/Select"));  });


            }

            foreach (KeyValuePair<Transform, GuideObject> pair in Singleton<GuideObjectManager>.Instance.dicGuideObject)
            {
                if (pair.Value.guideSelect != null)
                {
                    if (!Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(pair.Value.guideSelect.guideObject))
                        pair.Value.guideSelect.colorNow = pair.Value.guideSelect.colorNormal;
                    else
                        pair.Value.guideSelect.colorNow = new Color(0f, 1f, 1f, 0.5f);
                }
            }

            UpdateAllColors();
        }

       

        public static void Update()
        {
            if (goPickerNodesLine != null)
            {
                nodesLineDictionary.Clear();
                goPickerNodesLine.points3.Clear();

                if (!goPickerPages.IsNullOrEmpty() && showNodes)
                {
                    for (int i = 0; i < goPickerPages.Count; i++)
                    {
                        PickerPage page = goPickerPages[i];
                        if (page.pageButtons != null && page.showNodes)
                        {
                            for (int j = 0; j < page.pageButtons.Count; j++)
                            {
                                PickerButton button = page.pageButtons[j];
                                if (button.guideObject != null)
                                {
                                    goPickerNodesLine.points3.Add(button.guideObject.transformTarget.position);
                                    nodesLineDictionary.Add(nodesLineDictionary.Count, new KeyValuePair<PickerButton, PickerPage>(button, page));
                                }
                            }
                        }
                    }

                    if (goPickerNodesLine.points3.Count != 0)
                    {
                        goPickerNodesLine.active = true;
                        goPickerNodesLine.Draw();

                        int index = 0;
                        if (goPickerNodesLine.Selected(Input.mousePosition, out index))
                        {
                            if (index >= nodesLineDictionary.Count) return;
                            PickerPage page = nodesLineDictionary[index].Value;
                            PickerButton picker = nodesLineDictionary[index].Key;

                            if (picker == null || picker.guideObject == null) return;

                            isHovering = true;

                            if (goPickerEnableTooltip.Value)
                            {
                                showTooltip = true;
                                string pickerText = picker.buttonText.IsNullOrEmpty() ? picker.originalTransform : picker.buttonText;
                                string pageText = "";

                                if (goPickerShowPageNameTooltip.Value)
                                    pageText = $" - <color=#{ColorUtility.ToHtmlStringRGB(page.pageColor)}>[{page.pageText}]</color>";
                                tooltipText = $"<b><color=#{ColorUtility.ToHtmlStringRGB(picker.buttonColor)}>{pickerText}</color>{pageText}</b>";
                            }

                            if (Input.GetMouseButtonDown(0))
                            {
                                bool hasParent = picker.guideObject.parentGuide != null;
                                Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                                TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? picker.guideObject : picker.guideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                                if (node == null) return;

                                if (hasParent)
                                {
                                    ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(node);
                                    if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count != 1 || !Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                        Singleton<TreeNodeCtrl>.Instance.SelectSingle(node, false);

                                    Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                    Singleton<GuideObjectManager>.Instance.selectObject = picker.guideObject;
                                    picker.guideObject.isActive = true;
                                    _self.ExecuteDelayed2(() =>
                                    {
                                        picker.guideObject.SetLayer(picker.guideObject.gameObject, LayerMask.NameToLayer("Studio/Select"));
                                    });

                                }
                                else
                                {
                                    if (!picker.guideObject.isActive)
                                    {

                                        Singleton<TreeNodeCtrl>.Instance.SetSelectNode(node);
                                        if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                        {
                                            Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                            picker.guideObject.SetActive(true);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (isHovering)
                            {
                                isHovering = false;
                                showTooltip = false;
                            }
                        }

                        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(selectionKey))
                            StartSelection();
                    }

                }


                if (goPickerNodesLine.points3.Count == 0)
                {
                    goPickerNodesLine.active = false;
                }
            }



            if (isSelecting)
            {
                UpdateSelection();

                if (Input.GetKeyUp(selectionKey))
                    EndSelection();
            }
        }

        public static void UpdateEditButtonsWindow()
        {
            List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
            if (selectedPickerButtons.Count > 0)
            {
                PickerButton picker = selectedPickerButtons[0];
                if (picker.button == null) return;

                editButtonsHeight.text = ((RectTransform)picker.button.transform).sizeDelta.y.ToString();
                editButtonsWidth.text  = ((RectTransform)picker.button.transform).sizeDelta.x.ToString();
                editButtonsPosX.text   = ((RectTransform)picker.button.transform).anchoredPosition.x.ToString();
                editButtonsPosY.text = ((RectTransform)picker.button.transform).anchoredPosition.y.ToString();
                editButtonsName.text = picker.originalTransform;
            }
        }

        private static void OnPickerMouseDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_kkpeWindow._ui.transform, eventData.position, eventData.pressEventCamera, out Vector2 selectPoint))
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)goPickerScrollview.content.transform, eventData.position, eventData.pressEventCamera, out Vector2 mousePoint);

                    mousePoint -= goPickerScrollview.content.rect.position;

                    List <AContextMenuElement> elements = new List<AContextMenuElement>
                    {
                        new LeafElement()
                        {
                            text = "Add buttons",
                            onClick = p =>
                            {
                                if (Singleton<GuideObjectManager>.Instance.hashSelectObject == null || Singleton<GuideObjectManager>.Instance.hashSelectObject.Count == 0)
                                {
                                    ShalltyUtils.Logger.LogMessage("First select one or more GuideObjects!");
                                    return;
                                }

                                GuideObject[] selectedGuideObjects = Singleton<GuideObjectManager>.Instance.hashSelectObject.ToArray();

                                for (int i = 0; i < selectedGuideObjects.Length; i++)
                                {
                                    GuideObject guideObject = selectedGuideObjects[i];

                                    string pickerName = guideObject.transformTarget.name;

                                    TreeNodeObject node = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, guideObject)).Select(pair => pair.Key).FirstOrDefault();
                                    if (node != null)
                                        pickerName = node.textName;

                                    Vector2 rectPosition = new Vector2(mousePoint.x, mousePoint.y);

                                    // Calculate anchorMin, anchorMax, offsetMin, and offsetMax
                                    Vector2 anchorMin = new Vector2(0f, 0f);
                                    Vector2 anchorMax = new Vector2(0f, 0f);
                                    Vector2 offsetMin = Vector2.zero;
                                    Vector2 offsetMax = Vector2.zero;

                                    float gridDoubeCellSize = goPickerGridSize.Value * 2f;

                                    offsetMin.x = Mathf.Floor(rectPosition.x / gridDoubeCellSize) * gridDoubeCellSize;
                                    offsetMin.y = Mathf.Floor(rectPosition.y / goPickerGridSize.Value) * goPickerGridSize.Value;
                                    offsetMax.x = (Mathf.Ceil(rectPosition.x / gridDoubeCellSize) * gridDoubeCellSize);
                                    offsetMax.y = Mathf.Ceil(rectPosition.y / goPickerGridSize.Value) * goPickerGridSize.Value;

                                    while (goPickerButtons.Any(b => b.anchorMin == anchorMin && b.anchorMax == anchorMax && b.offsetMin == offsetMin && b.offsetMax == offsetMax))
                                    {
                                        // Increment rectPosition to move to the next grid cell
                                        rectPosition.y -= goPickerGridSize.Value;
    
                                        // Recalculate offsetMin and offsetMax based on the updated rectPosition
                                        offsetMin.x = Mathf.Floor(rectPosition.x / gridDoubeCellSize) * gridDoubeCellSize;
                                        offsetMin.y = Mathf.Floor(rectPosition.y / goPickerGridSize.Value) * goPickerGridSize.Value;
                                        offsetMax.x = (Mathf.Ceil(rectPosition.x / gridDoubeCellSize) * gridDoubeCellSize);
                                        offsetMax.y = Mathf.Ceil(rectPosition.y / goPickerGridSize.Value) * goPickerGridSize.Value;
                                    }
                                    
                                    // Create the PickerButton and add it to the list
                                    goPickerButtons.Add(new PickerButton(pickerName, Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f), anchorMin, anchorMax, offsetMin, offsetMax, guideObject));
                                }

                                // Update the UI with the newly created buttons
                                UpdatePickerButtons();
                            }
                        },
                        new LeafElement()
                        {
                            text = "Paste",
                            onClick = p =>
                            {
                                if (copyButtonsOffset.Count == 0) return;

                                Dictionary<PickerButton, Vector2> pastedButtons = new Dictionary<PickerButton, Vector2>();

                                foreach (KeyValuePair<PickerButton, Vector2> pair in copyButtonsOffset)
                                {
                                    PickerButton button = new PickerButton(pair.Key);
                                    Vector2 offset = pair.Value;

                                    goPickerButtons.Add(button);
                                    pastedButtons.Add(button, offset);
                                }

                                UpdatePickerButtons();


                                _self.ExecuteDelayed2(() =>
                                {
                                    foreach (KeyValuePair<PickerButton, Vector2> pair in pastedButtons)
                                    {
                                        PickerButton button = pair.Key;
                                        Vector2 offset = pair.Value;

                                        if (button == null || button.button == null) continue;

                                        RectTransform rect = ((RectTransform)button.button.gameObject.transform);

                                        rect.anchoredPosition = mousePoint + offset;

                                        button.anchorMin = rect.anchorMin;
                                        button.anchorMax = rect.anchorMax;
                                        button.offsetMin = rect.offsetMin;
                                        button.offsetMax = rect.offsetMax;
                                    }

                                });
                                
                            }
                        },
                        new LeafElement()
                        {
                            text = "Cancel",
                            onClick = p =>
                            {
                                UIUtility.HideContextMenu();
                            }
                        }
                    };
                    UIUtility.ShowContextMenu(_kkpeWindow._ui, selectPoint, elements);
                }
                return;
            }

            if (UIUtility.IsContextMenuDisplayed())
                UIUtility.HideContextMenu();
        }

        private static void GenerateGrid()
        {
            if (gridLine == null)
                return;

            RectTransform rect = goPickerScrollview.content.transform as RectTransform;

            float _curveGridCellSizePercent = goPickerGridSize.Value;

            Vector2 curveGridCellSize = new Vector2(_curveGridCellSizePercent, _curveGridCellSizePercent);
            List<Vector2> points = new List<Vector2>();

            float sizeMultiplier = goPickerGridScale.Value; 
            if (sizeMultiplier == 0) sizeMultiplier = 0.1f;

            float width =  rect.rect.width * sizeMultiplier;
            float height =  rect.rect.height * sizeMultiplier;

            int numColumns = Mathf.FloorToInt(width / curveGridCellSize.x);
            int numRows = Mathf.FloorToInt(height / curveGridCellSize.y);

            float xOffset = rect.rect.x * -1;
            float yOffset = rect.rect.y * -1;

            // Generate vertical grid lines
            for (int i = 0; i <= numColumns; i++)
            {
                float x = i * curveGridCellSize.x;
                points.Add(new Vector2(x - xOffset, -yOffset));
                points.Add(new Vector2(x - xOffset, height - yOffset));
            }

            // Generate horizontal grid lines
            for (int i = 0; i <= numRows; i++)
            {
                float y = i * curveGridCellSize.y;
                points.Add(new Vector2(-xOffset, y - yOffset));
                points.Add(new Vector2(width - xOffset, y - yOffset));
            }

            gridLine.points2 = points;

            if (gridLine.points2.Count > 0)
            {
                Vector3 lossyScale = Vector3.one;

                gridLine.active = true;
                gridLine.SetCanvas(_kkpeWindow._ui);
                gridLine.SetMask(goPickerScrollview.viewport.gameObject);
                gridLine.rectTransform.localScale = new Vector3(gridLine.rectTransform.localScale.x * (lossyScale.x / gridLine.rectTransform.lossyScale.x), gridLine.rectTransform.localScale.y * (lossyScale.y / gridLine.rectTransform.lossyScale.y), gridLine.rectTransform.localScale.z * (lossyScale.z / gridLine.rectTransform.lossyScale.z));
                gridLine.rectTransform.position = Vector3.zero;
                gridLine.drawTransform = goPickerScrollview.content.transform;
                gridLine.drawDepth = 0;
                gridLine.Draw();
            }
        }

        private static void AddPickerButton(bool update = true)
        {
            GuideObject guideObject = Singleton<GuideObjectManager>.Instance.selectObject;
            if (guideObject == null)
            {
                ShalltyUtils.Logger.LogMessage("First select an GuideObject!");
                return;
            }

            string pickerName = guideObject.transformTarget.name;


            TreeNodeObject node = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, guideObject)).Select(pair => pair.Key).FirstOrDefault();
            if (node != null)
                pickerName = node.textName;
            

            goPickerButtons.Add(new PickerButton(pickerName, Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f), goPickerSelectionArea.anchorMin, goPickerSelectionArea.anchorMax, goPickerSelectionArea.offsetMin, goPickerSelectionArea.offsetMax, guideObject));
            
            if (update)
                UpdatePickerButtons();
        }

        private static void UpdatePickerButtons()
        {
            foreach (Button button in goPickerCurrentButtons)
                GameObject.Destroy(button.gameObject);

            goPickerCurrentButtons.Clear();

            if (goPickerButtons == null || goPickerButtons.Count == 0 || goPickerPages == null || goPickerPages.Count == 0) return;

            foreach (PickerButton picker in goPickerButtons)
            {
                var button = UIUtility.CreateButton("goPickerButton_" + picker.buttonText, goPickerScrollview.content, picker.buttonText);
                button.transform.SetRect(picker.anchorMin, picker.anchorMax, picker.offsetMin, picker.offsetMax);
                button.image.color = picker.buttonColor;
                var buttonText = button.transform.GetComponentInChildren<Text>(true);

                buttonText.text = "[UNLINKED]";
                buttonText.color = Color.red;
                buttonText.rectTransform.SetRect(0f, 0.8f, 1f, 1.3f, 0f, 0f, 0f, 0f);
                buttonText.enabled = picker.guideObject == null;


                var field = UIUtility.CreateInputField("goPickerInputField_" + picker.buttonText, button.transform, "");
                field.transform.SetRect();
                field.textComponent.color = picker.buttonColor.GetContrastingColor();
                field.textComponent.alignment = TextAnchor.MiddleCenter;
                field.text = picker.buttonText;
                field.lineType = InputField.LineType.MultiLineNewline;
                field.readOnly = false;
                field.onEndEdit.AddListener((s) => {
                    picker.buttonText = s.IsNullOrEmpty() ? picker.guideObject.transformTarget.name : s;
                    field.readOnly = true;
                    field.enabled = false;
                } );
                field.GetComponent<Image>().enabled = false;
                field.ForceLabelUpdate();
                field.enabled = false;


                button.gameObject.GetOrAddComponent<PointerDownHandler>().onPointerDown = (e) => 
                {
                    if (Input.GetKey(KeyCode.LeftAlt))
                        return;

                    switch (e.button)
                    {
                        case PointerEventData.InputButton.Left:

                            if (picker.guideObject == null)
                            {
                                ShalltyUtils.Logger.LogMessage("GuideObjectPicker: This button isn't linked to anything!");
                                return;
                            }

                            bool hasParent = picker.guideObject.parentGuide != null;
                            Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                            TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? picker.guideObject : picker.guideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                            if (node == null) return;


                            if (hasParent)
                            {
                                ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(node);
                                if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count != 1 || !Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                    Singleton<TreeNodeCtrl>.Instance.SelectSingle(node, false);

                                Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                Singleton<GuideObjectManager>.Instance.selectObject = picker.guideObject;
                                picker.guideObject.isActive = true;
                                _self.ExecuteDelayed2(() =>
                                {
                                    picker.guideObject.SetLayer(picker.guideObject.gameObject, LayerMask.NameToLayer("Studio/Select"));
                                });

                            }
                            else
                            {
                                if (!picker.guideObject.isActive)
                                {

                                    Singleton<TreeNodeCtrl>.Instance.SetSelectNode(node);
                                    if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                    {
                                        Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                        picker.guideObject.SetActive(true);
                                    }
                                }
                            }

                            /*
                            bool hasParent = picker.guideObject.parentGuide != null;
                            Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                            TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? picker.guideObject : picker.guideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                            if (node == null) return;

                            if (hasParent)
                            {
                                ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(node);
                                if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count != 1 || !Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                    Singleton<TreeNodeCtrl>.Instance.SelectSingle(node, false);

                                Singleton<GuideObjectManager>.Instance.selectObject = picker.guideObject;
                            }
                            else
                            {
                                Singleton<TreeNodeCtrl>.Instance.SetSelectNode(node);
                            }*/

                            break;

                        case PointerEventData.InputButton.Right:

                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_kkpeWindow._ui.transform, e.position, e.pressEventCamera, out Vector2 localPoint))
                            {
                                
                                List<AContextMenuElement> elements = new List<AContextMenuElement>
                                {
                                    new LeafElement()
                                    {
                                        text = "Copy",
                                        onClick = p =>
                                        {
                                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)goPickerScrollview.content.transform), e.pressPosition, e.pressEventCamera, out Vector2 copyPoint))
                                            {
                                                copyPoint -= goPickerScrollview.content.rect.position;

                                                copyButtonsOffset.Clear();

                                                List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
                                                if (selectedPickerButtons.Count == 0)
                                                {
                                                    if (picker == null || picker.button == null) return;

                                                    RectTransform rect = ((RectTransform)button.gameObject.transform);
                                                    PickerButton newButton = new PickerButton(picker);
                                                    newButton.guideObject = null;
                                                    copyButtonsOffset.Add(newButton, rect.anchoredPosition - copyPoint);
                                                }
                                                else
                                                {
                                                    foreach (PickerButton selectedPicker in selectedPickerButtons)
                                                    {
                                                        if (selectedPicker == null || selectedPicker.button == null) continue;

                                                        RectTransform rect = ((RectTransform)selectedPicker.button.gameObject.transform);
                                                        PickerButton newButton = new PickerButton(selectedPicker);
                                                        newButton.guideObject = null;
                                                        copyButtonsOffset.Add(newButton, rect.anchoredPosition - copyPoint);
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Edit",
                                        onClick = p =>
                                        {
                                            editButtonsPanel.gameObject.SetActive(true);
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Rename",
                                        onClick = p =>
                                        {
                                            field.readOnly = false;
                                            field.enabled = true;
                                            field.ActivateInputField();
                                            field.Select();
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Color",
                                        onClick = p =>
                                        {
                                            Studio.Studio.Instance.colorPalette.visible = false;
                                            Studio.Studio.Instance.colorPalette.Setup("GoPicker Button - Color", picker.buttonColor, (col) =>
                                            {
                                                List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
                                                if (selectedPickerButtons.Count == 0)
                                                {
                                                    picker.buttonColor = col;
                                                }
                                                foreach (PickerButton selectedPicker in selectedPickerButtons)
                                                {
                                                    selectedPicker.buttonColor = col;
                                                }
                                                UpdateAllColors();
                                            }, false);
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Link to selected",
                                        onClick = p =>
                                        {
                                            GuideObject guideObject = Singleton<GuideObjectManager>.Instance.selectObject;
                                            if (guideObject == null)
                                            {
                                                ShalltyUtils.Logger.LogMessage("First select an GuideObject!");
                                                return;
                                            }

                                            picker.guideObject = guideObject;
                                            ShalltyUtils.Logger.LogMessage("Linked GuideObject updated.");

                                            ObjectCtrlInfo goOci = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject,  guideObject)).Select(pair => pair.Value).FirstOrDefault();
                                            if (goOci == null || !(goOci is OCIFolder))
                                            {
                                                picker.originalTransform = guideObject.transformTarget.name;
                                            }
                                            else if (goOci is OCIFolder ociFolder)
                                            {
                                                picker.originalTransform = ociFolder.treeNodeObject.textName;
                                            }
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Delete",
                                        onClick = p =>
                                        {
                                            UIUtility.DisplayConfirmationDialog(result =>
                                            {
                                                if (result)
                                                {
                                                    List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
                                                    if (selectedPickerButtons.Count == 0)
                                                        goPickerButtons.Remove(picker);
                                                    foreach (PickerButton selectedPicker in selectedPickerButtons)
                                                        goPickerButtons.Remove(selectedPicker);

                                                    UpdatePickerButtons();
                                                }

                                            }, "Are you sure you want to delete the selected button(s)?");
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Cancel",
                                        onClick = p =>
                                        {
                                           UIUtility.HideContextMenu();
                                        }
                                    }
                                };
                                UIUtility.ShowContextMenu(_kkpeWindow._ui, localPoint, elements);
                            }

                            break;
                    }
                };

                var dragHandler = button.gameObject.GetOrAddComponent<DragHandler>();
                dragHandler.onBeginDrag = (e) =>
                {
                    if (!Input.GetKey(KeyCode.LeftAlt))
                        return;

                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)goPickerScrollview.content.transform), e.position, e.pressEventCamera, out localPoint))
                    {
                        dragButtonsOffset.Clear();

                        List<PickerButton> selectedPickerButtons = GetSelectedPickerButtons();
                        if (selectedPickerButtons.Count == 0)
                        {
                            if (picker == null || picker.button == null) return;

                            RectTransform rect = ((RectTransform)button.gameObject.transform);
                            dragButtonsOffset.Add(picker, rect.anchoredPosition - localPoint);
                        }
                        else
                        {
                            foreach (PickerButton selectedPicker in selectedPickerButtons)
                            {
                                if (selectedPicker == null || selectedPicker.button == null) continue;

                                RectTransform rect = ((RectTransform)selectedPicker.button.gameObject.transform);
                                dragButtonsOffset.Add(selectedPicker, rect.anchoredPosition - localPoint);
                            }
                        }
                        
                    }
                    e.Reset();
                };
                dragHandler.onDrag = (e) =>
                {
                    if (e.button == PointerEventData.InputButton.Middle)
                    {
                        goPickerScrollview.content.localPosition = goPickerScrollview.content.localPosition + new Vector3(e.delta.x * 2, e.delta.y * 2);
                        GenerateGrid();
                    }

                    if (dragButtonsOffset.Count == 0) return;

                    if (!dragButtonsOffset.ContainsKey(picker)) return;

                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)goPickerScrollview.content.transform), e.position, e.pressEventCamera, out localPoint))
                    {
                        foreach (KeyValuePair<PickerButton, Vector2> pair in dragButtonsOffset)
                        {
                            PickerButton selectedPicker = pair.Key;
                            Vector2 offset = pair.Value;

                            if (selectedPicker == null || selectedPicker.button == null) continue;

                            RectTransform rect = ((RectTransform)selectedPicker.button.gameObject.transform);

                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                localPoint.x = Mathf.Floor(localPoint.x / goPickerGridSize.Value) * goPickerGridSize.Value;
                                localPoint.y = (Mathf.Ceil(localPoint.y / goPickerGridSize.Value) * goPickerGridSize.Value) - goPickerGridSize.Value / 2;
                            }

                            rect.anchoredPosition = localPoint + offset;

                            selectedPicker.anchorMin = rect.anchorMin;
                            selectedPicker.anchorMax = rect.anchorMax;
                            selectedPicker.offsetMin = rect.offsetMin;
                            selectedPicker.offsetMax = rect.offsetMax;
                        }
                    }
                    e.Reset();
                };
                dragHandler.onEndDrag = (e) =>
                {
                    if (dragButtonsOffset.Count == 0) return;
                        dragButtonsOffset.Clear();
                };


                UIUtility.AddOutlineToObject(button.transform);
                picker.button = button;
                goPickerCurrentButtons.Add(button);
            }

        }

        public static List<PickerButton> GetSelectedPickerButtons()
        {
            if (Singleton<GuideObjectManager>.Instance.hashSelectObject == null || Singleton<GuideObjectManager>.Instance.hashSelectObject.Count == 0 || goPickerButtons == null || goPickerButtons.Count == 0)
                return new List<PickerButton>();

            return goPickerButtons.Where(b => b != null && Singleton<GuideObjectManager>.Instance.hashSelectObject.Any(go => ReferenceEquals(go, b.guideObject))).ToList();
        }

        public static void UpdateAllColors(bool updateCurrentButtons = true)
        {
            if (updateCurrentButtons)
            {
                foreach (PickerButton picker in goPickerButtons)
                    picker.UpdateColor();
            }

            if (goPickerNodesLine != null && nodesLineDictionary != null && showNodes)
            {
                goPickerNodesLine.SetWidth(goPickerNodeSize.Value);

                GuideObject[] selectedObjects = Singleton<GuideObjectManager>.Instance.selectObjects;
                foreach (var kvp in nodesLineDictionary)
                {
                    int index = kvp.Key;
                    PickerButton button = kvp.Value.Key;
                    Color buttonColor = button.buttonColor;
                    if (button.guideObject == null || selectedObjects == null || selectedObjects.Length == 0)
                    {
                        goPickerNodesLine.SetColor(new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.5f), index);
                    }
                    else
                    {
                        goPickerNodesLine.SetColor(selectedObjects.Contains(button.guideObject) ?  buttonColor : new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.5f), index);
                    }
                }
            }
        }

        public static void UpdatePickerPages()
        {
            foreach (Button button in goPickerCurrentPagesButtons)
                GameObject.Destroy(button.gameObject);

            goPickerCurrentPagesButtons.Clear();

            if (goPickerPages.Count == 0)
            {
                PickerPage newPage = new PickerPage("New page", Color.white, new List<PickerButton>(), false);
                goPickerPages.Add(newPage);
            }

            if (!goPickerPages.Any(p => p.isSelected == true))
                goPickerPages.First().SelectPage();
            

            foreach (PickerPage page in goPickerPages)
            {
                var button = UIUtility.CreateButton("goPickerPage_" + page.pageText, goPickerButtonsScroll.content, page.pageText);
                button.transform.SetRect();
                button.image.color = page.isSelected ? new Color(page.pageColor.r, page.pageColor.g, page.pageColor.b, 0.2f) : page.pageColor;
                button.GetOrAddComponent<LayoutElement>().preferredHeight = 50f;

                var toggleText = button.transform.GetComponentInChildren<Text>(true);
                toggleText.transform.SetRect(0.70f);
                toggleText.alignByGeometry = false;
                toggleText.fontSize = 20;
                toggleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                toggleText.verticalOverflow = VerticalWrapMode.Overflow;
                toggleText.color = page.pageColor.GetContrastingColor();
                toggleText.text = page.showNodes ? "◈" : "◇";

                toggleText.gameObject.GetOrAddComponent<PointerDownHandler>().onPointerDown = (e) =>
                {
                    page.showNodes = !page.showNodes;
                    toggleText.text = page.showNodes ? "◈" : "◇";
                    _self.ExecuteDelayed2(() => { UpdateAllColors(false); });
                };

                var field = UIUtility.CreateInputField("goPickerInputField_" + page.pageText, button.transform, "");
                field.transform.SetRect(0f, 0f, 0.8f, 1f);
                field.textComponent.color = page.pageColor.GetContrastingColor();
                field.textComponent.alignment = TextAnchor.MiddleCenter;
                field.text = page.pageText;
                field.lineType = InputField.LineType.MultiLineNewline;
                field.readOnly = false;
                field.onEndEdit.AddListener((s) => {
                    page.pageText = s.IsNullOrEmpty() ? "New page" : s;
                    field.readOnly = true;
                    field.enabled = false;
                });
                field.GetComponent<Image>().enabled = false;
                field.ForceLabelUpdate();
                field.enabled = false;


                button.gameObject.GetOrAddComponent<PointerDownHandler>().onPointerDown = (e) =>
                {
                    switch (e.button)
                    {
                        case PointerEventData.InputButton.Left:

                            page.SelectPage();
                            UpdatePickerButtons();

                        break;

                        case PointerEventData.InputButton.Right:

                            page.SelectPage();
                            UpdatePickerButtons();

                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_kkpeWindow._ui.transform, e.position, e.pressEventCamera, out Vector2 localPoint))
                            {

                                List<AContextMenuElement> elements = new List<AContextMenuElement>
                                {
                                    new LeafElement()
                                    {
                                        text = "Rename",
                                        onClick = p =>
                                        {
                                            field.readOnly = false;
                                            field.enabled = true;
                                            field.ActivateInputField();
                                            field.Select();
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Color",
                                        onClick = p =>
                                        {
                                            Studio.Studio.Instance.colorPalette.visible = false;
                                            Studio.Studio.Instance.colorPalette.Setup("GoPicker Page - Color", page.pageColor, (col) =>
                                            {
                                                page.pageColor = col;
                                                button.image.color = page.isSelected ? new Color(page.pageColor.r, page.pageColor.g, page.pageColor.b, 0.2f) : page.pageColor;
                                                field.textComponent.color = col.GetContrastingColor();
                                                toggleText.color = col.GetContrastingColor();

                                            }, false);
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Save page file",
                                        onClick = p =>
                                        {
                                            SaveSinglePageFile(page);
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Try linking unlinked buttons",
                                        onClick = p =>
                                        {
                                            LinkUnlinkedButtons(page);
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Delete",
                                        onClick = p =>
                                        {
                                            UIUtility.DisplayConfirmationDialog(result =>
                                            {
                                                if (result)
                                                {
                                                    goPickerPages.Remove(page);
                                                    //goPickerPages.First().SelectPage();
                                                    UpdatePickerPages();
                                                }

                                            }, "Are you sure you want to delete this page?");
                                        }
                                    },
                                    new LeafElement()
                                    {
                                        text = "Cancel",
                                        onClick = p =>
                                        {
                                           UIUtility.HideContextMenu();
                                        }
                                    }
                                };
                                UIUtility.ShowContextMenu(_kkpeWindow._ui, localPoint, elements);
                            }

                            break;
                    }
                };

                UIUtility.AddOutlineToObject(button.transform);
                page.button = button;
                goPickerCurrentPagesButtons.Add(button);
            }

            goPickerButtonsScroll.content.sizeDelta = new Vector2(0f, Mathf.Clamp(-250f + (goPickerCurrentPagesButtons.Count * 50f), 0f, float.MaxValue));
            UpdatePickerButtons();
        }

        public static void TransformGuideObject(GuideObject guideObject, Vector3 newPosition, bool ignoreChildren = false, bool moveTimeline = true)
        {
            if (moveTimeline && _timeline._interpolables.Count > 0)
            {
                Interpolable interpolable = _timeline._interpolables.Where(pair => pair.Value != null && pair.Value.id == "guideObjectPos" && ReferenceEquals(pair.Value.parameter, guideObject)).Select(pair => pair.Value).FirstOrDefault();

                if (interpolable != null)
                {
                    foreach (var pair in interpolable.keyframes)
                    {
                        if (pair.Value == null) return;
                        Vector3 value = (Vector3)pair.Value.value;
                        pair.Value.value = value + (newPosition - guideObject.changeAmount.pos);
                    }
                }
            }

            Vector3 moveDifference = guideObject.changeAmount.pos - newPosition;
            guideObject.changeAmount.pos = newPosition;

            if (ignoreChildren)
            {

                bool hasParent = guideObject.parentGuide != null;
                Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? guideObject : guideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                if (node == null) return;

                foreach (TreeNodeObject child in node.child)
                {
                    ObjectCtrlInfo oci;
                    if (Singleton<Studio.Studio>.Instance.dicInfo.TryGetValue(child, out oci))
                    {
                        oci.guideObject.changeAmount.pos += moveDifference;

                        if (moveTimeline && _timeline._interpolables.Count > 0)
                        {
                          Interpolable interpolable = _timeline._interpolables.Where(pair => pair.Value != null && pair.Value.id == "guideObjectPos" && ReferenceEquals(pair.Value.parameter, oci.guideObject)).Select(pair => pair.Value).FirstOrDefault();
                           
                            if (interpolable != null)
                            {
                                foreach (var pair in interpolable.keyframes)
                                {
                                    if (pair.Value == null) return;
                                    Vector3 value = (Vector3)pair.Value.value;
                                    pair.Value.value = value + moveDifference;
                                }
                            }

                        }
                    }
                    
                }
            }
        }

        #region LOAD/SAVE PAGES

        public static void ClearSceneData()
        {
            goPickerPages.Clear();
            goPickerButtons.Clear();

            foreach (Button button in goPickerCurrentPagesButtons)
                GameObject.Destroy(button.gameObject);

            foreach (Button button in goPickerCurrentButtons)
                GameObject.Destroy(button.gameObject);

            UpdatePickerPages();
        }

        public static string SaveSceneData()
        {
            if (!_kkpeWindow) return string.Empty;

            List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();


            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(stringWriter))
                {
                    writer.WriteStartElement("root");

                    foreach (PickerPage page in goPickerPages)
                        SaveSinglePage(page, writer, dic);
                   
                    writer.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        public static void LoadSceneData(PluginData pluginData, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (!_kkpeWindow) return;

            if (pluginData == null)
            {
                ClearSceneData();
                return;
            }

            _self.ExecuteDelayed2(() =>
            {
                goPickerPages.Clear();
                goPickerButtons.Clear();

                foreach (Button button in goPickerCurrentPagesButtons)
                    GameObject.Destroy(button.gameObject);

                foreach (Button button in goPickerCurrentButtons)
                    GameObject.Destroy(button.gameObject);

                string data = (string)pluginData.data["guideObjectPickerData"];
                if (data.IsNullOrEmpty()) return;

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(data);

                XmlNode node = doc.FirstChild;
                if (node == null) return;

                List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();
    
                foreach (XmlNode childNode in node.ChildNodes)
                    LoadSinglePage(childNode, dic);


                UpdatePickerPages();

                _self.ExecuteDelayed2(() =>
                {
                    UpdateAllColors();
                });

            }, 20);
        }

        public static void ImportSceneData(PluginData pluginData, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (!_kkpeWindow || pluginData == null) return;

            _self.ExecuteDelayed2(() =>
            {
                string data = (string)pluginData.data["guideObjectPickerData"];
                if (data.IsNullOrEmpty()) return;

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(data);

                XmlNode node = doc.FirstChild;
                if (node == null) return;

                List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(loadedItems).ToList();

                try
                {
                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        if (childNode.Name == "page")
                        {
                            string pageText = childNode.Attributes["pageText"].Value;
                            Color pageColor = childNode.ReadColor("pageColor");
                            bool showNodes = childNode.Attributes["showNodes"] == null || childNode.ReadBool("showNodes");

                            List<PickerButton> allPageButtons = new List<PickerButton>();

                            foreach (XmlNode pageButtons in childNode.ChildNodes)
                            {
                                if (pageButtons.Name == "pageButtons")
                                {
                                    foreach (XmlNode button in pageButtons.ChildNodes)
                                    {
                                        if (button.Name == "button")
                                        {
                                            string buttonText = button.Attributes["buttonText"].Value;
                                            Color buttonColor = button.ReadColor("buttonColor");
                                            Vector2 anchorMin = button.ReadVector2("anchorMin");
                                            Vector2 anchorMax = button.ReadVector2("anchorMax");
                                            Vector2 offsetMin = button.ReadVector2("offsetMin");
                                            Vector2 offsetMax = button.ReadVector2("offsetMax");

                                            GuideObject guideObject = null;
                                            int objectIndex = button.ReadInt("objectIndex");
                                            if (objectIndex != -1)
                                            {
                                                if (objectIndex < dic.Count)
                                                {
                                                    ObjectCtrlInfo oci = dic[objectIndex].Value;

                                                    Transform t = oci.guideObject.transformTarget.Find(button.Attributes["guideObjectPath"].Value);
                                                    if (t != null)
                                                        _self._allGuideObjects.TryGetValue(t, out guideObject);
                                                }
                                            }

                                            allPageButtons.Add(new PickerButton(buttonText, buttonColor, anchorMin, anchorMax, offsetMin, offsetMax, guideObject));
                                        }
                                    }
                                }
                            }

                            PickerPage page = new PickerPage(pageText, pageColor, allPageButtons, showNodes);
                            goPickerPages.Add(page);
                        }
                    }
                }
                catch (Exception e)
                {
                    ShalltyUtils.Logger.LogError("Couldn't load GuideObjectPicker data: " + e); ;
                }

                UpdatePickerPages();
            }, 20);
        }

        public static void SaveSinglePage(PickerPage page, XmlTextWriter mainWriter, List<KeyValuePair<int, ObjectCtrlInfo>> dic)
        {
            if (page == null || page.pageButtons == null || page.pageButtons.Count == 0) return;

            using (StringWriter stream = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(stream))
                {
                    try
                    {
                        writer.WriteStartElement("page");

                        writer.WriteAttributeString("pageText", page.pageText);
                        writer.WriteValue("pageColor", page.pageColor);
                        writer.WriteValue("showNodes", page.showNodes);

                        writer.WriteStartElement("pageButtons");
                        foreach (PickerButton button in page.pageButtons)
                        {
                            writer.WriteStartElement("button");

                            writer.WriteAttributeString("buttonText", button.buttonText);
                            writer.WriteValue("buttonColor", button.buttonColor);
                            writer.WriteValue("anchorMin", button.anchorMin);
                            writer.WriteValue("anchorMax", button.anchorMax);
                            writer.WriteValue("offsetMin", button.offsetMin);
                            writer.WriteValue("offsetMax", button.offsetMax);
                            writer.WriteAttributeString("originalTransform", button.originalTransform);


                            if (dic != null)
                            {

                                int objectIndex = -1;
                                string guideObjectPath = "";


                                if (button.guideObject != null && button.guideObject.transformTarget != null)
                                {
                                    GuideObject guideObject = button.guideObject.parentGuide ?? button.guideObject;
                                    if (guideObject == null) return;

                                    objectIndex = dic.FindIndex(pair => ReferenceEquals(pair.Value.guideObject, guideObject));
                                    if (objectIndex != -1)
                                    {
                                        if (dic[objectIndex].Value != null)
                                        {
                                            if (dic[objectIndex].Value.guideObject != null && dic[objectIndex].Value.guideObject.transformTarget != null)
                                            {
                                                Transform self = button.guideObject.transformTarget;
                                                Transform root = dic[objectIndex].Value.guideObject.transformTarget;

                                                if (self != root)
                                                {
                                                    Transform self2 = self;
                                                    StringBuilder path = new StringBuilder(self2.name);
                                                    self2 = self2.parent;
                                                    while (self2 != root)
                                                    {
                                                        path.Insert(0, "/");
                                                        path.Insert(0, self2.name);
                                                        self2 = self2.parent;
                                                    }
                                                    guideObjectPath = path.ToString();
                                                }
                                            }
                                        }
                                    }
                                }

                                writer.WriteValue("objectIndex", objectIndex);
                                writer.WriteAttributeString("guideObjectPath", guideObjectPath);
                            }
                            

                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();

                        writer.WriteEndElement();
                    }
                    catch (Exception e)
                    {
                        ShalltyUtils.Logger.LogError($"GuideObjectPicker: There was a problem saving the page: {page.pageText}, error: {e}");
                        return; 
                    }
                }
                mainWriter.WriteRaw(stream.ToString());
            }
        }

        public static void SaveSinglePageDic(PickerPage page, XmlTextWriter mainWriter, Dictionary<int, ObjectCtrlInfo> dic)
        {
            if (page == null || page.pageButtons == null || page.pageButtons.Count == 0) return;

            using (StringWriter stream = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(stream))
                {
                    try
                    {
                        writer.WriteStartElement("page");

                        writer.WriteAttributeString("pageText", page.pageText);
                        writer.WriteValue("pageColor", page.pageColor);
                        writer.WriteValue("showNodes", page.showNodes);

                        writer.WriteStartElement("pageButtons");
                        foreach (PickerButton button in page.pageButtons)
                        {
                            writer.WriteStartElement("button");

                            writer.WriteAttributeString("buttonText", button.buttonText);
                            writer.WriteValue("buttonColor", button.buttonColor);
                            writer.WriteValue("anchorMin", button.anchorMin);
                            writer.WriteValue("anchorMax", button.anchorMax);
                            writer.WriteValue("offsetMin", button.offsetMin);
                            writer.WriteValue("offsetMax", button.offsetMax);
                            writer.WriteAttributeString("originalTransform", button.originalTransform);

                            if (dic != null)
                            {
                                int objectIndex = -1;
                                string guideObjectPath = "";

                                if (button.guideObject != null && button.guideObject.transformTarget != null)
                                {
                                    GuideObject guideObject = button.guideObject.parentGuide ?? button.guideObject;
                                    if (guideObject == null) return;

                                    objectIndex = guideObject.dicKey;
                                    if (objectIndex != -1)
                                    {
                                        if (dic[objectIndex] != null)
                                        {
                                            if (dic[objectIndex].guideObject != null && dic[objectIndex].guideObject.transformTarget != null)
                                            {
                                                Transform self = button.guideObject.transformTarget;
                                                Transform root = dic[objectIndex].guideObject.transformTarget;

                                                if (self != root)
                                                {
                                                    Transform self2 = self;
                                                    StringBuilder path = new StringBuilder(self2.name);
                                                    self2 = self2.parent;
                                                    while (self2 != root)
                                                    {
                                                        path.Insert(0, "/");
                                                        path.Insert(0, self2.name);
                                                        self2 = self2.parent;
                                                    }
                                                    guideObjectPath = path.ToString();
                                                }
                                            }
                                        }
                                    }
                                }

                                writer.WriteValue("objectIndex", objectIndex);
                                writer.WriteAttributeString("guideObjectPath", guideObjectPath);
                            }


                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();

                        writer.WriteEndElement();
                    }
                    catch (Exception e)
                    {
                        ShalltyUtils.Logger.LogError($"GuideObjectPicker: There was a problem saving the page: {page.pageText}, error: {e}");
                        return;
                    }
                }
                mainWriter.WriteRaw(stream.ToString());
            }
        }

        public static void LoadSinglePage(XmlNode childNode, List<KeyValuePair<int, ObjectCtrlInfo>> dic)
        {
            try
            {
                if (childNode.Name == "page")
                {
                    string pageText = childNode.Attributes["pageText"].Value;
                    Color pageColor = childNode.ReadColor("pageColor");
                    bool showNodes = childNode.Attributes["showNodes"] == null || childNode.ReadBool("showNodes");

                    List<PickerButton> allPageButtons = new List<PickerButton>();

                    foreach (XmlNode pageButtons in childNode.ChildNodes)
                    {
                        if (pageButtons.Name == "pageButtons")
                        {
                            foreach (XmlNode button in pageButtons.ChildNodes)
                            {
                                if (button.Name == "button")
                                {
                                    string buttonText = button.Attributes["buttonText"].Value;
                                    Color buttonColor = button.ReadColor("buttonColor");
                                    Vector2 anchorMin = button.ReadVector2("anchorMin");
                                    Vector2 anchorMax = button.ReadVector2("anchorMax");
                                    Vector2 offsetMin = button.ReadVector2("offsetMin");
                                    Vector2 offsetMax = button.ReadVector2("offsetMax");
                                    string originalTransform = button.Attributes["originalTransform"] == null ? "$$null$$" : button.Attributes["originalTransform"].Value;

                                    GuideObject guideObject = null;
                                    if (dic != null)
                                    {
                                        int objectIndex = button.ReadInt("objectIndex");
                                        if (objectIndex != -1)
                                        {
                                            if (objectIndex < dic.Count)
                                            {
                                                ObjectCtrlInfo oci = dic[objectIndex].Value;

                                                Transform t = oci.guideObject.transformTarget.Find(button.Attributes["guideObjectPath"].Value);
                                                if (t != null)
                                                {
                                                    _self._allGuideObjects.TryGetValue(t, out guideObject);

                                                    if (guideObject != null && originalTransform == "$$null$$")
                                                    {
                                                        if (oci is OCIFolder folder)
                                                            originalTransform = folder.treeNodeObject.textName;
                                                        else
                                                            originalTransform = t.name;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    allPageButtons.Add(new PickerButton(buttonText, buttonColor, anchorMin, anchorMax, offsetMin, offsetMax, guideObject, originalTransform));
                                }
                            }
                        }
                    }

                    PickerPage page = new PickerPage(pageText, pageColor, allPageButtons, showNodes);
                    goPickerPages.Add(page);
                }
            }
            catch (Exception e)
            {
                ShalltyUtils.Logger.LogError("GuideObjectPicker: Couldn't load page data: " + e); ;
            }
        }
        public static void LoadSinglePageDic(XmlNode childNode, Dictionary<int, ObjectCtrlInfo> dic)
        {
            try
            {
                if (childNode.Name == "page")
                {
                    string pageText = childNode.Attributes["pageText"].Value;
                    Color pageColor = childNode.ReadColor("pageColor");
                    bool showNodes = childNode.Attributes["showNodes"] == null || childNode.ReadBool("showNodes");

                    List<PickerButton> allPageButtons = new List<PickerButton>();

                    foreach (XmlNode pageButtons in childNode.ChildNodes)
                    {
                        if (pageButtons.Name == "pageButtons")
                        {
                            foreach (XmlNode button in pageButtons.ChildNodes)
                            {
                                if (button.Name == "button")
                                {
                                    string buttonText = button.Attributes["buttonText"].Value;
                                    Color buttonColor = button.ReadColor("buttonColor");
                                    Vector2 anchorMin = button.ReadVector2("anchorMin");
                                    Vector2 anchorMax = button.ReadVector2("anchorMax");
                                    Vector2 offsetMin = button.ReadVector2("offsetMin");
                                    Vector2 offsetMax = button.ReadVector2("offsetMax");
                                    string originalTransform = button.Attributes["originalTransform"] == null ? "$$null$$" : button.Attributes["originalTransform"].Value;

                                    GuideObject guideObject = null;
                                    if (dic != null)
                                    {
                                        int objectIndex = button.ReadInt("objectIndex");
                                        if (objectIndex != -1)
                                        {
                                            if (dic.ContainsKey(objectIndex))
                                            {
                                                ObjectCtrlInfo oci = dic[objectIndex];

                                                Transform t = oci.guideObject.transformTarget.Find(button.Attributes["guideObjectPath"].Value);
                                                if (t != null)
                                                {
                                                    _self._allGuideObjects.TryGetValue(t, out guideObject);

                                                    if (guideObject != null && originalTransform == "$$null$$")
                                                    {
                                                        if (oci is OCIFolder folder)
                                                            originalTransform = folder.treeNodeObject.textName;
                                                        else
                                                            originalTransform = t.name;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    allPageButtons.Add(new PickerButton(buttonText, buttonColor, anchorMin, anchorMax, offsetMin, offsetMax, guideObject, originalTransform));
                                }
                            }
                        }
                    }

                    PickerPage page = new PickerPage(pageText, pageColor, allPageButtons, showNodes);
                    goPickerPages.Add(page);
                }
            }
            catch (Exception e)
            {
                ShalltyUtils.Logger.LogError("GuideObjectPicker: Couldn't load page data: " + e); ;
            }
        }

        private static void SaveSinglePageFile(PickerPage page)
        {
            if (page == null) return;

            string dir = _defaultDir;
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("SAVE SINGLE PAGE", dir, "XML files (*.xml)|*.xml", "xml", SingleFileFlags);
            if (file == null) return;

            using (XmlTextWriter writer = new XmlTextWriter(file[0], Encoding.UTF8))
            {
                writer.WriteStartElement("root");

                SaveSinglePage(page, writer, null);

                writer.WriteEndElement();
            }
        }

        private static void LoadSinglePageFile()
        {
            string dir = _defaultDir;
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_ALLOWMULTISELECT |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] files = OpenFileDialog.ShowDialog("LOAD PAGE FILE(s)", dir, "XML files (*.xml)|*.xml", "xml", SingleFileFlags);
            if (files == null) return;

            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    XmlDocument document = new XmlDocument();
                    try
                    {
                        document.Load(file);

                        XmlNode node = document.FirstChild;
                        if (node == null) return;

                        foreach (XmlNode childNode in node.ChildNodes)
                            LoadSinglePage(childNode, null);

                    }
                    catch (Exception e)
                    {
                        string fileName = Path.GetFileName(file);
                        ShalltyUtils.Logger.LogError($"GuideObjectPicker: Could not load data of the file: {fileName} \n " +
                        $"GuideObjectPicker: Could not load data of the file: {fileName}, error: {e}");
                    }
                    
                }
            }

            UpdatePickerPages();         
        }

        #endregion
        
        public static void LinkUnlinkedButtons(PickerPage page)
        {
            if (page == null) return;

            if (page.pageButtons.Count == 0)
            {
                ShalltyUtils.Logger.LogMessage("This page doesn't have any buttons!");
                return;
            }

            List<ObjectCtrlInfo> selectedOcis = new List<ObjectCtrlInfo>(selectedObjects);

            if (selectedOcis.Count == 0)
            {
                ShalltyUtils.Logger.LogMessage("First select at least one object in the Workspace!");
                return;
            }

            Dictionary<string, GuideObject> filteredList = new Dictionary<string, GuideObject>();


            foreach (ObjectCtrlInfo obj in selectedOcis)
            {
                if (obj == null) continue;

                if (!(obj is OCIFolder))
                    filteredList[obj.guideObject.transformTarget.name] = obj.guideObject;

                if (obj is OCIItem item)
                {
                    foreach (OCIChar.BoneInfo bone in item.listBones)
                        filteredList[bone.guideObject.transformTarget.name] = bone.guideObject;
                }
                else if (obj is OCIChar chara)
                {
                    foreach (OCIChar.BoneInfo bone in chara.listBones)
                        filteredList[bone.guideObject.transformTarget.name] = bone.guideObject;

                    foreach (OCIChar.IKInfo bone in chara.listIKTarget)
                        filteredList[bone.guideObject.transformTarget.name] = bone.guideObject;
                }
                else if (obj is OCIFolder folder)
                {
                    filteredList[folder.treeNodeObject.textName] = folder.guideObject;
                }
            }


            foreach (PickerButton button in page.pageButtons)
            {
           
                if (button.guideObject != null || button.originalTransform == "$$null$$") continue;

                if (filteredList.Any(pair => pair.Key == button.originalTransform))
                    button.guideObject = filteredList.Where(pair => pair.Key == button.originalTransform).Select(pair => pair.Value).FirstOrDefault();
            }

        }


        #region MOVE/ROTATE Buttons DRAGGING

        public static void StartDrag(DragType dragType)
        {
            if (_lockDrag)
                return;
            _currentDragType = dragType;
        }

        public static void StopDrag()
        {
            if (_lockDrag)
                return;

            GuideCommand.EqualsInfo[] moveCommands = new GuideCommand.EqualsInfo[_oldPosValues.Count];
            int i = 0;
            if (_currentDragType == DragType.Position || _currentDragType == DragType.Both)
            {
                foreach (KeyValuePair<int, Vector3> kvp in _oldPosValues)
                {
                    if (kvp.Key == kkpeGuideObjectDictKey) continue;

                    moveCommands[i] = new GuideCommand.EqualsInfo()
                    {
                        dicKey = kvp.Key,
                        oldValue = kvp.Value,
                        newValue = Studio.Studio.Instance.dicChangeAmount[kvp.Key].pos
                    };
                    ++i;
                }
            }
            GuideCommand.EqualsInfo[] rotateCommands = new GuideCommand.EqualsInfo[_oldRotValues.Count + _additionalRotationEqualsCommands.Count];
            i = 0;
            if (_currentDragType == DragType.Rotation || _currentDragType == DragType.Both)
            {
                foreach (KeyValuePair<int, Vector3> kvp in _oldRotValues)
                {
                    if (kvp.Key == kkpeGuideObjectDictKey) continue;

                    rotateCommands[i] = new GuideCommand.EqualsInfo()
                    {
                        dicKey = kvp.Key,
                        oldValue = kvp.Value,
                        newValue = Studio.Studio.Instance.dicChangeAmount[kvp.Key].rot
                    };
                    ++i;
                }
            }
            foreach (GuideCommand.EqualsInfo info in _additionalRotationEqualsCommands)
            {
                rotateCommands[i] = info;
                ++i;
            }
            UndoRedoManager.Instance.Push(new HSPE.Commands.MoveRotateEqualsCommand(moveCommands, rotateCommands));
            _currentDragType = DragType.None;
            _oldPosValues.Clear();
            _oldRotValues.Clear();
            _additionalRotationEqualsCommands.Clear();
        }

        #endregion

        public class PickerDragHandler : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {

            public void OnInitializePotentialDrag(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(goPickerScrollview.content, eventData.position, eventData.pressEventCamera, out localPoint))
                    {
                        localPoint -= goPickerScrollview.content.rect.position;
                        goPickerSelectFirstPoint = localPoint;
                    }

                    goPickerSelecting = false;
                    eventData.Reset();
                }
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    goPickerSelectionImage.color = new Color(0f, 1f, 1f, 0.45f);
                    goPickerSelecting = true;
                    goPickerSelectionArea.SetAsLastSibling();
                    goPickerSelectionArea.gameObject.SetActive(true);
                    eventData.Reset();
                }
            }

            public void OnDrag(PointerEventData eventData)
            {

                if (eventData.button == PointerEventData.InputButton.Middle)
                {
                    goPickerScrollview.content.localPosition = goPickerScrollview.content.localPosition + new Vector3(eventData.delta.x * 2, eventData.delta.y * 2);
                    //goPickerScrollview.normalizedPosition = new Vector2(Mathf.Clamp(goPickerScrollview.normalizedPosition.x, 0.0f, 1.0f), Mathf.Clamp(goPickerScrollview.normalizedPosition.y, 0.0f, 1.0f));
                    GenerateGrid();
                }

                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    if (goPickerSelecting == false)
                        return;

                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)goPickerScrollview.content.transform, eventData.position, eventData.pressEventCamera, out localPoint))
                    {
                        localPoint -= goPickerScrollview.content.rect.position;

                        Vector2 min = new Vector2(Mathf.Min(goPickerSelectFirstPoint.x, localPoint.x), Mathf.Min(goPickerSelectFirstPoint.y, localPoint.y));
                        Vector2 max = new Vector2(Mathf.Max(goPickerSelectFirstPoint.x, localPoint.x), Mathf.Max(goPickerSelectFirstPoint.y, localPoint.y));

                        goPickerSelectionArea.offsetMin = min;
                        goPickerSelectionArea.offsetMax = max;
                    } 
                }
                eventData.Reset();
            }


            public void OnEndDrag(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    Vector2 localPoint;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)goPickerScrollview.content.transform, eventData.position, eventData.pressEventCamera, out localPoint))
                        return;

                    localPoint -= goPickerScrollview.content.rect.position;

                    goPickerSelectionArea.gameObject.SetActive(false);

                    if (goPickerSelecting)
                    {
                        List<GuideObject> toSelect = new List<GuideObject>();

                        foreach (PickerButton picker in goPickerButtons)
                        {
                            if (goPickerSelectionArea.Overlaps((RectTransform)picker.button.transform, true))
                            {
                                if (picker.guideObject != null)
                                    toSelect.Add(picker.guideObject);
                            }
                        }

                        if (!Input.GetKey(KeyCode.LeftControl))
                        {
                            foreach (TreeNodeObject _node in Singleton<TreeNodeCtrl>.Instance.hashSelectNode)
                                _node.OnDeselect();
                            Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Clear();

                            foreach (GuideObject go in new HashSet<GuideObject>(Singleton<GuideObjectManager>.Instance.hashSelectObject))
                                Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);
                        }

                        if (toSelect.Count > 0)
                        {
                            Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                            foreach (GuideObject go in toSelect)
                            {
                                bool hasParent = go.parentGuide != null;

                                TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? go : go.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                                if (node == null) return;

                                if (hasParent)
                                {
                                    if (!Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(go))
                                        Singleton<GuideObjectManager>.Instance.AddSelectMultiple(go);
                                    else
                                        Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);
                                }
                                else
                                {
                                    Singleton<TreeNodeCtrl>.Instance.AddSelectNode(node, true);
                                }
                            }

                            GuideObject lastGuideObject = toSelect.Last();

                            Singleton<GuideObjectManager>.Instance.StopSelectObject();
                            lastGuideObject.isActive = true;
                            _self.ExecuteDelayed2(() => { lastGuideObject.SetLayer(lastGuideObject.gameObject, LayerMask.NameToLayer("Studio/Select")); });
                        }

                        foreach (KeyValuePair<Transform, GuideObject> pair in Singleton<GuideObjectManager>.Instance.dicGuideObject)
                        {
                            if (pair.Value.guideSelect != null)
                            {
                                if (!Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(pair.Value.guideSelect.guideObject))
                                    pair.Value.guideSelect.colorNow = pair.Value.guideSelect.colorNormal;
                                else
                                    pair.Value.guideSelect.colorNow = new Color(0f, 1f, 1f, 0.5f);
                            }
                        }

                        UpdateAllColors();
                    }

                    goPickerSelecting = false;
                    eventData.Reset();
                }
            }
        }

        public static class Hooks
        {
            [HarmonyPostfix, HarmonyPatch(typeof(MainWindow), nameof(MainWindow.SpawnGUI))]
            static void SpawnGUIPostfix()
            {
                _kkpeWindow = Singleton<MainWindow>.Instance;
                if (_kkpeWindow == null)
                {
                    ShalltyUtils.Logger.LogError("KKPE isn't instantiated!");
                    return;
                }

                Init();
            }

            [HarmonyPostfix, HarmonyPatch(typeof(MainWindow), nameof(MainWindow.OnTargetChange))]
            static void OnTargetChangePostFix(MainWindow __instance, PoseController last)
            {
                if (__instance._poseTarget == null && goPickerToggle.isOn)
                {
                    //__instance._nothingText.gameObject.SetActive(false);
                    __instance._controls.gameObject.SetActive(true);
                }
            }

            [HarmonyPostfix, HarmonyPatch(typeof(StudioScene), nameof(StudioScene.OnClickAxis))]
            static void OnClickAxisPostfix(StudioScene __instance)
            {
                if (goPickerHideWithAxis.Value)
                    showNodes = Singleton<Studio.Studio>.Instance.workInfo.visibleAxis;
            }

            [HarmonyPrefix, HarmonyPatch(typeof(Studio.CameraControl), nameof(Studio.CameraControl.InputMouseProc))]
            static bool InputMouseProcPostfix()
            {
                if (isSelecting)
                    return false;
                else
                    return true;
            }

            [HarmonyPostfix, HarmonyPatch(typeof(MainWindow), nameof(MainWindow.GUILogic))]
            static void GUILogicPostfix(MainWindow __instance)
            {
                if (!__instance._ui.gameObject.activeSelf || !goPickerToggle.isOn) return;

                bool shouldInteract = Singleton<GuideObjectManager>.Instance.selectObjects != null && Singleton<GuideObjectManager>.Instance.selectObjects.Length > 0;

                if (shouldInteract)
                {
                    if (__instance._xMove || __instance._yMove || __instance._zMove || __instance._xRot || __instance._yRot || __instance._zRot)
                    {
                        __instance._delta += (new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (Input.GetKey(KeyCode.LeftShift) ? 4f : 1f) / (Input.GetKey(KeyCode.LeftControl) ? 6f : 1f)) / 10f;

                        if (_currentDragType == DragType.None)
                            StartDrag(__instance._xMove || __instance._yMove || __instance._zMove ? DragType.Position : DragType.Rotation);

                        if (Singleton<GuideObjectManager>.Instance.selectObjects.Length > 0 && Singleton<GuideObjectManager>.Instance.selectObjects.Length == lastGuideObjectPos.Count && Singleton<GuideObjectManager>.Instance.selectObjects.Length == lastGuideObjectRot.Count)
                        {
                            for (int i = 0; i < Singleton<GuideObjectManager>.Instance.selectObjects.Length; i++)
                            {
                                GuideObject guideObject = Singleton<GuideObjectManager>.Instance.selectObjects[i];
                                bool changePosition = false;
                                bool changeRotation = false;
                                Vector3 newPosition = lastGuideObjectPos[i];
                                Quaternion newRotation = lastGuideObjectRot[i];

                                if (__instance._xMove)
                                {
                                    newPosition.x += __instance._delta.y * __instance._intensityValue;
                                    changePosition = true;
                                }
                                if (__instance._yMove)
                                {
                                    newPosition.y += __instance._delta.y * __instance._intensityValue;
                                    changePosition = true;
                                }
                                if (__instance._zMove)
                                {
                                    newPosition.z += __instance._delta.y * __instance._intensityValue;
                                    changePosition = true;
                                }
                                if (__instance._xRot)
                                {
                                    newRotation *= Quaternion.AngleAxis(__instance._delta.x * 20f * __instance._intensityValue, Vector3.right);
                                    changeRotation = true;
                                }
                                if (__instance._yRot)
                                {
                                    newRotation *= Quaternion.AngleAxis(__instance._delta.x * 20f * __instance._intensityValue, Vector3.up);
                                    changeRotation = true;
                                }
                                if (__instance._zRot)
                                {
                                    newRotation *= Quaternion.AngleAxis(__instance._delta.x * 20f * __instance._intensityValue, Vector3.forward);
                                    changeRotation = true;
                                }

                                if (changePosition && guideObject.enablePos)
                                {
                                    if (_oldPosValues.ContainsKey(guideObject.dicKey) == false)
                                        _oldPosValues.Add(guideObject.dicKey,  guideObject.changeAmount.pos);

                                    TransformGuideObject(guideObject, __instance._positionOperationWorld ? guideObject.transformTarget.parent.InverseTransformPoint(newPosition) : newPosition, ignoreChildren, moveTimeline);
                                
                                }
                                if (changeRotation && guideObject.enableRot)
                                {
                                    if (_oldRotValues.ContainsKey(guideObject.dicKey) == false)
                                        _oldRotValues.Add(guideObject.dicKey, guideObject.changeAmount.rot);

                                    guideObject.changeAmount.rot = newRotation.eulerAngles;
                                }
                            }
                        }
                    }
                    else
                    {
                        __instance._delta = Vector2.zero;
                        if (_currentDragType != DragType.None)
                            StopDrag();


                        lastGuideObjectPos.Clear();
                        lastGuideObjectRot.Clear();

                        if (Singleton<GuideObjectManager>.Instance.selectObjects.Length > 0)
                        {
                            lastGuideObjectPos.AddRange(Singleton<GuideObjectManager>.Instance.selectObjects.Select(go => go.changeAmount.pos));
                            lastGuideObjectRot.AddRange(Singleton<GuideObjectManager>.Instance.selectObjects.Select(go => Quaternion.Euler(go.changeAmount.rot)));
                        }


                    }
                }

               

                for (int i = 0; i < __instance._positionButtons.Length; i++)
                    __instance._positionButtons[i].interactable = shouldInteract;

                for (int i = 0; i < __instance._rotationButtons.Length; i++)
                    __instance._rotationButtons[i].interactable = shouldInteract;

            }

        }


    }

    public static class RectTransformExtensions
    {

        public static bool Overlaps(this RectTransform a, RectTransform b)
        {
            return a.WorldRect().Overlaps(b.WorldRect());
        }
        public static bool Overlaps(this RectTransform a, RectTransform b, bool allowInverse)
        {
            return a.WorldRect().Overlaps(b.WorldRect(), allowInverse);
        }

        public static Rect WorldRect(this RectTransform rectTransform)
        {
            Vector2 sizeDelta = rectTransform.sizeDelta;
            Vector2 pivot = rectTransform.pivot;

            float rectTransformWidth = sizeDelta.x * rectTransform.lossyScale.x;
            float rectTransformHeight = sizeDelta.y * rectTransform.lossyScale.y;

            //With this it works even if the pivot is not at the center
            Vector3 position = rectTransform.TransformPoint(rectTransform.rect.center);
            float x = position.x - rectTransformWidth * 0.5f;
            float y = position.y - rectTransformHeight * 0.5f;

            return new Rect(x, y, rectTransformWidth, rectTransformHeight);
        }

    }
}