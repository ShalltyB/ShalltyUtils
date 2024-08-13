extern alias aliasTimeline;
using aliasTimeline.UILib;
using aliasTimeline.UILib.EventHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;
using Vectrosity;
using static aliasTimeline::Timeline.Timeline;
using System.Collections;
using aliasTimeline::Timeline;
using Studio;
using Illusion.Extensions;
using System.IO;
using System.Text;
using System.Xml;

namespace ShalltyUtils
{
    public class GraphEditor : MonoBehaviour
    {
        public float _curveGridCellSizePercent = 0.05f;
        public float gridSize = 100f;
        public float gridWidth = 1f;
        public float curveWidth = 2f;
        public Color curveColor = Color.cyan;

        public ScrollRect gridWindow;
        public RectTransform _gridWindow;

        private GameObject _curveContainer;
       
        public RawImage _gridImage;
        public RectTransform gridRect;

        public List<VectorLine> allCurveLines = new List<VectorLine>();
        public VectorLine keysLine;

        public VectorLine gridLine;
        public VectorLine cursorLine;
        public VectorLine curveLine;
        public VectorLine handlesLine;
        public int curveResolution = 50;

        private MovableWindow windowMaximize;

        private float windowSize = 500f;
        public float graphZoomX = 270f;
        public float graphZoomY = 270f;
        private bool isZooming = false;

        public Vector3 _scale = Vector3.zero;

        public static bool keyTimesMode = false;
        private int _selectedKeyframeCurvePointIndex;

        private readonly List <CurveKeyframeDisplay> _displayedCurveKeyframes = new List<CurveKeyframeDisplay>();
        private readonly List<KeyValuePair<CurveKeyframeDisplay, CurveKeyframeDisplay>> _displayedCurveKeyframesHandles = new List<KeyValuePair<CurveKeyframeDisplay, CurveKeyframeDisplay>>();

        public static AnimationCurve curveKeyTimes = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve mainCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private Image startPanel;
        private Image endPanel;

        public static List<KeyValuePair<float, Keyframe>> originalKeyframes = new List<KeyValuePair<float, Keyframe>>();
        private static Dictionary<Interpolable, List<KeyValuePair<float, Keyframe>>> keyTimesKeyframesByParent = new Dictionary<Interpolable, List<KeyValuePair<float, Keyframe>>>();
        private static List<Vector3> originalKeyframesValues = new List<Vector3>();
        private static float keyTimesMinValue = 0f;
        private static float keyTimesMaxValue = 0f;

        public Image DragPanel { get; private set; }


        private IEnumerator SmoothZoom(Vector2 endZoom)
        {
            isZooming = true;
            float startTime = Time.unscaledTime;
            Vector2 startZoom = new Vector2(graphZoomX, graphZoomY);
            float zoomDuration = 0.1f;


            while (Time.unscaledTime - startTime < zoomDuration)
            {
                float t = (Time.unscaledTime - startTime) / zoomDuration;
                Vector2 newZoom = Vector2.Lerp(startZoom, endZoom, t);
                graphZoomX = newZoom.x;
                graphZoomY = newZoom.y;
                gridRect.sizeDelta = newZoom;
                _timeline.UpdateCurve();
                yield return null;
            }

            graphZoomX = endZoom.x;
            graphZoomY = endZoom.y;
            gridRect.sizeDelta = endZoom;


            //_timeline.UpdateCurve();

            GenerateCursor();
            GenerateGrid();
            GenerateHandlesLines();
            DrawLines();

            isZooming = false;
        }

        public void CreateGridWindow()
    {
            var _keyframeWindow = Instantiate(_timeline._keyframeWindow, _timeline._ui.transform);
            _keyframeWindow.SetActive(false);

            _curveContainer = _keyframeWindow.transform.Find("Main Container/Curve Fields/Curve/Grid/Spline").gameObject;
            Destroy(_curveContainer.GetComponent<RawImage>());
            GameObject _cursor2 = _keyframeWindow.transform.Find("Main Container/Curve Fields/Curve/Grid/Cursor").gameObject;
            Destroy(_cursor2);

            _timeline._ui.transform.Find("Keyframe Window/Main Container/Curve Fields/Curve/Grid").gameObject.SetActive(false);
            _timeline._ui.transform.Find("Keyframe Window/Main Container/Curve Fields/Header").gameObject.SetActive(false);
            Transform curveContainer = _timeline._ui.transform.Find("Keyframe Window/Main Container/Curve Fields/Curve");


            Image EditorMainPanel = UIUtility.CreatePanel("GraphWindow", curveContainer);
            EditorMainPanel.transform.SetRect(0f, 0f, 1f, 1.075f, 0f, 0f, 0f, 0f);
            UIUtility.AddOutlineToObject(EditorMainPanel.transform, Color.black);

            _gridWindow = (RectTransform)EditorMainPanel.transform;

            gridWindow = UIUtility.CreateScrollView("GridScrollView", EditorMainPanel.transform);
            UIUtility.AddOutlineToObject(gridWindow.transform, Color.black);
            gridWindow.transform.SetRect(0.01f, 0.02f, 0.99f, 0.96f);
            gridWindow.inertia = false;
            gridWindow.scrollSensitivity = 0f;
            gridWindow.movementType = ScrollRect.MovementType.Unrestricted;
            gridWindow.onValueChanged.AddListener((Vector2 scrollPosition) =>
            {
                GenerateCursor();
                GenerateGrid();
                GenerateHandlesLines();
                DrawLines();
            });

            

            gridWindow.gameObject.AddComponent<PointerDownHandler>().onPointerDown = OnCurveMouseDown;

            PointerDownHandler oldComponent = _curveContainer.GetComponent<PointerDownHandler>();
            if (oldComponent != null) DestroyImmediate(oldComponent);

            RectTransform curveRect = (RectTransform)_keyframeWindow.transform.Find("Main Container/Curve Fields/Curve");
            curveRect.SetParent(gridWindow.content.transform);
            curveRect.SetRect(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            curveRect.sizeDelta = Vector2.one;

            gridRect = (RectTransform)curveRect.Find("Grid");
            gridRect.SetRect(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            gridRect.sizeDelta = new Vector2(graphZoomX, graphZoomY);
            gridRect.pivot = Vector2.zero;
            Destroy(gridRect.GetComponent<RawImage>());


            gridWindow.content.SetRect(0f, 0f, 0f, 0f, 5f, 5f, 5f, 5f);
            gridWindow.content.pivot = Vector2.zero;

            startPanel = UIUtility.CreatePanel("StartLimitPanel", gridWindow.content);
            startPanel.color = new Color(0f, 0f, 0f, 0.85f);
            ((RectTransform)startPanel.transform).pivot = new Vector2(1f, 0.5f);

            endPanel = UIUtility.CreatePanel("EndLimitPanel", gridWindow.content);
            endPanel.color = new Color(0f, 0f, 0f, 0.85f);
            ((RectTransform)endPanel.transform).pivot = new Vector2(0f, 0.5f);

            gridWindow.gameObject.AddComponent<ScrollHandler>().onScroll = e =>
            {
                if (Input.GetKey(KeyCode.LeftControl) && windowMaximize.enabled)
                {
                    if (e.scrollDelta.y < 0)
                    {
                        windowSize += 20f;
                        if (windowSize > 1000f)
                            windowSize = 1000f;
                    }
                    else
                    {
                        windowSize -= 20f;
                        if (windowSize < 0f)
                            windowSize = 0f;
                    }
                    _gridWindow.sizeDelta = new Vector2(windowSize, windowSize);
                }
                else if (Input.GetKey(KeyCode.LeftAlt))
                {

                    float sizeMultiplier = 10f;
                    float maxSize = 1000f;
                    float minSize = 10f;

                    if (e.scrollDelta.y < 0)
                    {
                        gridSize += sizeMultiplier;
                        if (gridSize > maxSize)
                            gridSize = maxSize;
                    }
                    else
                    {
                        gridSize -= sizeMultiplier;
                        if (gridSize < minSize)
                            gridSize = minSize;
                    }

                }
                else
                {

                    if (e.scrollDelta.y != 0)
                    {
                        if (isZooming) return;

                        float zoomMultiplier = 20f;
                        float maxZoom = 10000f;
                        float minZoom = 20f;
                        float maxMultiplier = 1000f;

                        float normalizedZoomLevelX = Mathf.InverseLerp(minZoom, maxZoom, graphZoomX);
                        float targetZoomMultiplierX = Mathf.Lerp(zoomMultiplier, maxMultiplier, normalizedZoomLevelX);
                        float targetZoomX = Mathf.Clamp(graphZoomX + e.scrollDelta.y * targetZoomMultiplierX, minZoom, maxZoom);

                        float normalizedZoomLevelY = Mathf.InverseLerp(minZoom, maxZoom, graphZoomY);
                        float targetZoomMultiplierY = Mathf.Lerp(zoomMultiplier, maxMultiplier, normalizedZoomLevelY);
                       
                        float targetZoomY = Mathf.Clamp(graphZoomY + e.scrollDelta.y * targetZoomMultiplierY, minZoom, maxZoom);


                        Vector2 endZoom = Vector2.zero;
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            endZoom = new Vector2(targetZoomX, graphZoomY);
                        }
                        else
                        {
                            endZoom = new Vector2(targetZoomX, targetZoomY);
                        }

                        
                        StartCoroutine(SmoothZoom(endZoom));
                    }

                }

                _timeline.UpdateCurve();
                e.Reset();
            };

            DragPanel = UIUtility.CreatePanel("Draggable", EditorMainPanel.transform);
            DragPanel.transform.SetRect(0f, 1f, 1f, 1f, 0f, -20f);
            DragPanel.color = Color.gray;
            windowMaximize = UIUtility.MakeObjectDraggable(DragPanel.rectTransform, EditorMainPanel.rectTransform);
            windowMaximize.enabled = false;

            var nametext = UIUtility.CreateText("Title", DragPanel.transform, "Graph Editor");
            nametext.transform.SetRect();
            nametext.alignment = TextAnchor.MiddleCenter;

            var close = UIUtility.CreateButton("MaximizeButton", DragPanel.transform, "❐");
            close.transform.SetRect(1f, 0f, 1f, 1f, -20f, 1f, -1f, -1f);
            close.onClick.AddListener(() => 
            {
                EditorMainPanel.transform.SetRect(0f, 0f, 1f, 1.075f, 0f, 0f, 0f, 0f);
                windowMaximize.enabled = !windowMaximize.enabled;
                if (windowMaximize.enabled)
                    _gridWindow.sizeDelta = new Vector2(windowSize, windowSize);
            });
            UI.CreateTooltip(close.gameObject, "Window mode");

            var resetView = UIUtility.CreateButton("ResetViewButton", DragPanel.transform, "↺");
            resetView.transform.SetRect(0.92f, 0f, 0.92f, 1f, -20f, 1f, -1f, -1f);
            resetView.onClick.AddListener(() =>
            {
                gridWindow.content.SetRect(0f, 0f, 0f, 0f, 5f, 5f, 5f ,5f);
                gridWindow.content.pivot = Vector2.zero;
                graphZoomX = 270f;
                graphZoomY = 270f;
                gridSize = 100f;
                gridRect.sizeDelta = new Vector2(graphZoomX, graphZoomY);
                _timeline.UpdateCurve();
                isZooming = false;
            });
            UI.CreateTooltip(resetView.gameObject, "Reset view");

            var keysTimesMode = UIUtility.CreateToggle("keysTimesMode", DragPanel.transform, "");
            keysTimesMode.transform.SetRect(0.10f, 0f, 0.10f, 1f, -20f, 1f, -1f, -1f);
            keysTimesMode.isOn = false;
            keysTimesMode.onValueChanged.AddListener((v) =>
            {
                ToggleKeyTimesMode();
                keysTimesMode.isOn = keyTimesMode; 
            });
            UI.CreateTooltip(keysTimesMode.gameObject, "Turn on to enter the 'Edit Keyframes Times' mode, where you can edit times of all selected keyframes with a curve in the Graph Editor.", 800f);



            gridLine = new VectorLine("graphGridLine", new List<Vector2> { Vector2.zero, Vector2.one }, gridWidth, LineType.Discrete, Joins.None);
            gridLine.active = false;
            gridLine.color = Color.black;

            cursorLine = new VectorLine("graphGursorLine", new List<Vector2> { Vector2.zero, Vector2.one }, 3f, LineType.Discrete, Joins.None);
            cursorLine.active = false;
            cursorLine.color = Color.black;

            handlesLine = new VectorLine("graphHandlesLine", new List<Vector2> { Vector2.zero, Vector2.one }, 1f, LineType.Discrete, Joins.None);
            handlesLine.active = false;
            handlesLine.color = Color.red;

            curveLine = new VectorLine("graphCurveLine", new List<Vector2> { Vector2.zero, Vector2.one }, curveWidth, LineType.Continuous, Joins.Fill);
            curveLine.active = false;
            curveLine.color = curveColor;
            allCurveLines.Add(curveLine);

            GameObject.Destroy(_keyframeWindow);
        }

        private void OnCurveMouseDown(PointerEventData eventData)
        {
            if (_timeline._selectedKeyframes.Count == 0) return;

            if (eventData.button == PointerEventData.InputButton.Middle && Input.GetKey(KeyCode.LeftControl) == false && RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_curveContainer.transform), eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                float time = localPoint.x / ((RectTransform)_curveContainer.transform).rect.width;
                float value = localPoint.y / ((RectTransform)_curveContainer.transform).rect.height;
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    float mod = time % _curveGridCellSizePercent;
                    if (mod / _curveGridCellSizePercent > 0.5f)
                        time += _curveGridCellSizePercent - mod;
                    else
                        time -= mod;
                    mod = value % _curveGridCellSizePercent;
                    if (mod / _curveGridCellSizePercent > 0.5f)
                        value += _curveGridCellSizePercent - mod;
                    else
                        value -= mod;
                }

                UnityEngine.Keyframe curveKey = new UnityEngine.Keyframe(time, value);
                if (curveKey.time < 0 || curveKey.time > 1)
                    return;

                curveKey.inTangent = Mathf.Tan(45f * Mathf.Deg2Rad);
                curveKey.outTangent = Mathf.Tan(45f * Mathf.Deg2Rad);

                if (!keyTimesMode)
                {
                    mainCurve = _timeline._selectedKeyframes[0].Value.curve;
                    _selectedKeyframeCurvePointIndex = mainCurve.AddKey(curveKey);
                    _timeline._selectedKeyframeCurvePointIndex = _selectedKeyframeCurvePointIndex;
                    _timeline.SaveKeyframeCurve();
                }
                else
                {
                    mainCurve = curveKeyTimes;
                    _selectedKeyframeCurvePointIndex = mainCurve.AddKey(curveKey);
                }
                _timeline.UpdateCurve();
            }
        }

        public void CurveToKeyframes(AnimationCurve mainCurve)
        {
            if (mainCurve == null)
                return;

            if (mainCurve.keys.Length == _timeline._selectedInterpolables.Count)
            {
                SortedList<float, Keyframe> interpolableKeyframes = _timeline._selectedInterpolables[0].keyframes;

                for (int i = 0; i < mainCurve.keys.Length; i++)
                {
                    Keyframe timelineKey = interpolableKeyframes.Values[i];
                    UnityEngine.Keyframe k = mainCurve.keys[i];

                    //Update value
                    Vector3 oldValue = (Vector3)timelineKey.value;
                    timelineKey.value = new Vector3(k.value, oldValue.y, oldValue.z);
                    
                    //Update time
                    interpolableKeyframes.RemoveAt(i);
                    interpolableKeyframes.Add(k.time, timelineKey);

                    //Update tangents
                    UnityEngine.Keyframe inKey = interpolableKeyframes.Values[i].curve[0];
                    interpolableKeyframes.Values[i].curve.RemoveKey(0);
                    inKey.inTangent = k.inTangent;
                    inKey.outTangent = k.outTangent;
                    interpolableKeyframes.Values[i].curve.AddKey(inKey);

                    if (i > 0 && i - 1 < interpolableKeyframes.Values.Count)
                    {
                        UnityEngine.Keyframe outKey = interpolableKeyframes.Values[i - 1].curve[1];
                        interpolableKeyframes.Values[i - 1].curve.RemoveKey(1);
                        outKey.outTangent = k.outTangent;
                        outKey.inTangent = k.inTangent;
                        interpolableKeyframes.Values[i - 1].curve.AddKey(outKey);
                    }
                }
            }
        }

        public List<Vector2> GeneratePoints(AnimationCurve curve, bool isFirst = false, bool isLast = false)
        {
            int pointCount = curveResolution;
            RectTransform rectTransform = (RectTransform)_curveContainer.transform;
            List<Vector2> points = new List<Vector2>();

            if (curve == null || curve.length < 2) return points;

            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;

            for (int i = 0; i < curve.length - 1; i++)
            {
                UnityEngine.Keyframe startKeyframe = curve[i];
                UnityEngine.Keyframe endKeyframe = curve[i + 1];

                AnimationCurve segmentCurve = new AnimationCurve(startKeyframe, endKeyframe);
                segmentCurve.preWrapMode = WrapMode.Once;
                segmentCurve.postWrapMode = WrapMode.Once;

                int startIndex = i == 0 ? 0 : 1;
                for (int j = startIndex; j < pointCount; j++)
                {
                    
                    float t = Mathf.Lerp(startKeyframe.time, endKeyframe.time, j / (float)(pointCount - 1));
                    float eval = segmentCurve.Evaluate(t);

                    Vector2 point = new Vector2(t * width, eval * height);
                    points.Add(point);
                }
            }

            RectTransform contentRect = (RectTransform)gridWindow.content.transform;

            if (isFirst)
            {
                Vector2 startPoint = new Vector2((contentRect.position.x * -1) - (10000 * width), points.FirstOrDefault().y);
                points.Insert(0, startPoint);
            }
            if (isLast)
            {
                Vector2 endPoint = new Vector2((contentRect.position.x * -1) + (10000 * width), points.LastOrDefault().y);
                points.Add(endPoint);
            }

            return points;
        }

        public void GenerateGraphCurves(AnimationCurve mainCurve)
        {
            foreach (VectorLine curve in allCurveLines)
                curve.points2.Clear();

            if (mainCurve != null)
            {
                int linesCount = Mathf.CeilToInt(((mainCurve.length * curveResolution)) / 10000f);

                if (linesCount > 0)
                {
                    List<AnimationCurve> curves = SplitAnimationCurve(mainCurve, linesCount);

                    /// CREATE NEW LINES
                    if (allCurveLines.Count < linesCount)
                    {
                        int linesToGo = linesCount - allCurveLines.Count;

                        for (int i = 0; i < linesToGo; i++)
                        {
                            VectorLine newLine = new VectorLine($"graphCurveLine ({allCurveLines.Count + 1})", new List<Vector2> { Vector2.zero, Vector2.one }, curveWidth, LineType.Continuous, Joins.Fill);
                            newLine.active = false;
                            newLine.color = keyTimesMode ? Color.green : Color.cyan;
                            allCurveLines.Add(newLine);
                        }
                    }

                    /// GENERATE LINES POINTS
                    for (int i = 0; i < curves.Count; i++)
                    {
                        bool isFirst = i == 0;
                        bool isLast = i == curves.Count - 1;

                        VectorLine newCurveLine = allCurveLines[i];
                        newCurveLine.points2 = GeneratePoints(curves[i], isFirst, isLast);
                        newCurveLine.active = true;
                    }
                }
            }

            // REMOVE EMPTY LINES
            for (int j = 0; j < allCurveLines.Count; j++)
            {
                VectorLine newCurveLine = allCurveLines[j];
                if (newCurveLine.points2.Count == 0)
                {
                    allCurveLines.Remove(newCurveLine);
                    VectorLine.Destroy(ref newCurveLine);
                }
            }
        }

        public List<AnimationCurve> SplitAnimationCurve(AnimationCurve inputCurve, int splitCount)
        {
            List<AnimationCurve> splitCurves = new List<AnimationCurve>();

            int totalKeys = inputCurve.length;
            int keysPerSplit = Mathf.CeilToInt((float)totalKeys / splitCount);

            for (int i = 0; i < splitCount; i++)
            {
                AnimationCurve splitCurve = new AnimationCurve();

                int startIndex = i * keysPerSplit;
                int endIndex = Mathf.Min((i + 1) * keysPerSplit, totalKeys);

                if (i < splitCount - 1)
                    endIndex++;

                for (int j = startIndex; j < endIndex; j++)
                {
                    UnityEngine.Keyframe keyframe = inputCurve.keys[j];
                    splitCurve.AddKey(keyframe);
                }

                splitCurves.Add(splitCurve);
            }

            return splitCurves;
        }

        private void DrawLines()
        {
            Vector3 lossyScale = Vector3.one;

            gridLine.SetCanvas(_timeline._ui);
            gridLine.SetMask(gridWindow.viewport.gameObject);
            gridLine.rectTransform.localScale = new Vector3(gridLine.rectTransform.localScale.x * (lossyScale.x / gridLine.rectTransform.lossyScale.x), gridLine.rectTransform.localScale.y * (lossyScale.y / gridLine.rectTransform.lossyScale.y), gridLine.rectTransform.localScale.z * (lossyScale.z / gridLine.rectTransform.lossyScale.z));
            gridLine.rectTransform.position = Vector3.zero;
            gridLine.drawTransform = _curveContainer.transform;
            gridLine.Draw();

            cursorLine.SetCanvas(_timeline._ui);
            cursorLine.SetMask(gridWindow.viewport.gameObject);
            cursorLine.rectTransform.localScale = new Vector3(cursorLine.rectTransform.localScale.x * (lossyScale.x / cursorLine.rectTransform.lossyScale.x), cursorLine.rectTransform.localScale.y * (lossyScale.y / cursorLine.rectTransform.lossyScale.y), cursorLine.rectTransform.localScale.z * (lossyScale.z / cursorLine.rectTransform.lossyScale.z));
            cursorLine.rectTransform.position = Vector3.zero;
            cursorLine.drawTransform = _curveContainer.transform;
            cursorLine.Draw();

            handlesLine.SetCanvas(_timeline._ui);
            handlesLine.SetMask(gridWindow.viewport.gameObject);
            handlesLine.rectTransform.localScale = new Vector3(handlesLine.rectTransform.localScale.x * (lossyScale.x / handlesLine.rectTransform.lossyScale.x), handlesLine.rectTransform.localScale.y * (lossyScale.y / handlesLine.rectTransform.lossyScale.y), handlesLine.rectTransform.localScale.z * (lossyScale.z / handlesLine.rectTransform.lossyScale.z));
            handlesLine.rectTransform.position = Vector3.zero;
            handlesLine.drawTransform = _curveContainer.transform;
            handlesLine.Draw();


            for (int i = 0; i < allCurveLines.Count; i++)
            {
                VectorLine line = allCurveLines[i];
                line.SetCanvas(_timeline._ui);
                line.SetMask(gridWindow.viewport.gameObject);
                line.rectTransform.localScale = new Vector3(line.rectTransform.localScale.x * (lossyScale.x / line.rectTransform.lossyScale.x), line.rectTransform.localScale.y * (lossyScale.y / line.rectTransform.lossyScale.y), line.rectTransform.localScale.z * (lossyScale.z / line.rectTransform.lossyScale.z));
                line.rectTransform.position = Vector3.zero;
                line.drawTransform = _curveContainer.transform;
                line.Draw();

                // Depth
                gridLine.drawDepth = 0;
                cursorLine.drawDepth = 1;
                line.drawDepth = i + 2;
            }

            handlesLine.drawDepth = allCurveLines.Count + 2;
        }

        private void GenerateGrid()
        {
            if (gridLine == null)
                return;

            gridLine.points2 = GetGridPoints();
            gridLine.active = true;
        }

        public void GenerateCursor()
        {
            if (cursorLine == null)
                return;


            List<Vector2> points = new List<Vector2>();

            float width = ((RectTransform)_curveContainer.transform).rect.width;
            float height = ((RectTransform)_curveContainer.transform).rect.height;

            RectTransform contentRect = gridWindow.content;
            
            Vector2 originLeft = new Vector2((contentRect.position.x * -1) - (10000 * width), 0);
            Vector2 originRight = new Vector2((contentRect.position.x * -1) + (10000 * width), 0);
            Vector2 originUp = new Vector2(0, (contentRect.position.y * -1) - (10000 * height));
            Vector2 originDown = new Vector2(0, (contentRect.position.y * -1) + (10000 * height));

            points.Add(originLeft);
            points.Add(originRight);
            points.Add(originUp);
            points.Add(originDown);

            cursorLine.points2 = points;

            UpdateCursorDuration();

            cursorLine.SetColor(Color.black);
            cursorLine.SetWidth(3f);

            UpdateCursorTime();

            cursorLine.active = true;

            ///

            startPanel.color = new Color(0f, 0f, 0f, 0.85f);
            startPanel.transform.SetRect(0f, 0f, 1f, 1f, (contentRect.position.x * -1) - (10000 * width), (contentRect.position.y * -1) - (10000 * height), 0f, (contentRect.position.y * -1) + (10000 * height));

            endPanel.color = new Color(0f, 0f, 0f, 0.85f);
            endPanel.transform.SetRect(0f, 0f, 1f, 1f, width, (contentRect.position.y * -1) - (10000 * height), (contentRect.position.x * -1) + (10000 * width), (contentRect.position.y * -1) + (10000 * height));
        }

        public void GenerateHandlesLines()
        {
            if (handlesLine == null || _displayedCurveKeyframesHandles.Count == 0)
                return;

            List<Vector2> points = new List<Vector2>();

            foreach (var handles in _displayedCurveKeyframesHandles)
            {
                if (handles.Key.gameObject.activeSelf && handles.Value.gameObject.activeSelf)
                {
                    if (_selectedKeyframeCurvePointIndex < 0 || _selectedKeyframeCurvePointIndex >= _displayedCurveKeyframes.Count)
                        continue;

                    CurveKeyframeDisplay key = _displayedCurveKeyframes[_selectedKeyframeCurvePointIndex];
                    Vector2 keyPos = ((RectTransform)key.gameObject.transform).anchoredPosition;

                    Vector2 inHandlePos = ((RectTransform)handles.Key.gameObject.transform).anchoredPosition;
                    Vector2 outHandlePos = ((RectTransform)handles.Value.gameObject.transform).anchoredPosition;

                    points.Add(keyPos);
                    points.Add(inHandlePos);

                    points.Add(keyPos);
                    points.Add(outHandlePos);
                } 
            }

            handlesLine.points2 = points;
            handlesLine.active = points.Count > 3;
            handlesLine.Draw();
        }

        public void UpdateCursorDuration()
        {
            if (cursorLine == null)
                return;

            float width = ((RectTransform)_curveContainer.transform).rect.width;
            float height = ((RectTransform)_curveContainer.transform).rect.height;

            Vector2 timeUp = new Vector2(1f * width, (gridWindow.content.position.y * -1) - (10000 * height));
            Vector2 timeDown = new Vector2(1f * width, (gridWindow.content.position.y * -1) + (10000 * height));

            cursorLine.points2.Add(timeUp);
            cursorLine.points2.Add(timeDown);
        }

        public void UpdateCursorTime(bool draw = false)
        {
            if (cursorLine == null || !_timeline._keyframeWindow.activeSelf)
                return;

            float width = ((RectTransform)_curveContainer.transform).rect.width;
            float height = ((RectTransform)_curveContainer.transform).rect.height;

            float normalizedTime = 0f;

            if (!keyTimesMode)
            {
                if (_timeline._selectedKeyframes.Count != 1) return;
                
                KeyValuePair<float, Keyframe> selectedKeyframe = _timeline._selectedKeyframes[0];

                if (_timeline._playbackTime < selectedKeyframe.Key) return;
                
                KeyValuePair<float, Keyframe> after = selectedKeyframe.Value.parent.keyframes.FirstOrDefault(k => k.Key > selectedKeyframe.Key);
                if (after.Value != null && _timeline._playbackTime <= after.Key)
                {
                    normalizedTime = (_timeline._playbackTime - selectedKeyframe.Key) / (after.Key - selectedKeyframe.Key);
                }
                else
                    return;
            }
            else
            {
                if (_timeline._selectedKeyframes.Count == 0) return;
                List<KeyValuePair<float, Keyframe>> keyframes = _timeline._selectedKeyframes.OrderBy(pair => pair.Key).ToList();

                float keyMinTime = keyframes.FirstOrDefault().Key;
                float keyMaxTime = keyframes.LastOrDefault().Key;

                if (_timeline._playbackTime < keyMinTime || _timeline._playbackTime > keyMaxTime) return;

                normalizedTime = (_timeline._playbackTime - keyMinTime) / (keyMaxTime - keyMinTime);
            }


            Vector2 currentTimeUp = new Vector2(normalizedTime * width, (gridWindow.content.position.y * -1) - (10000 * height));
            Vector2 currentTimeDown = new Vector2(normalizedTime * width, (gridWindow.content.position.y * -1) + (10000 * height));

            if (cursorLine.points2.Count == 8)
            {
                cursorLine.points2[6] = currentTimeUp;
                cursorLine.points2[7] = currentTimeDown;
            }
            else if (cursorLine.points2.Count < 8)
            {
                cursorLine.points2.Add(currentTimeUp);
                cursorLine.points2.Add(currentTimeDown);
            }

            cursorLine.SetColor(Color.red, 3);
            cursorLine.SetWidth(2f, 3);

            if (draw)
            {
                Vector3 lossyScale = Vector3.one;
                cursorLine.SetCanvas(_timeline._ui);
                cursorLine.SetMask(gridWindow.viewport.gameObject);
                cursorLine.rectTransform.localScale = new Vector3(cursorLine.rectTransform.localScale.x * (lossyScale.x / cursorLine.rectTransform.lossyScale.x), cursorLine.rectTransform.localScale.y * (lossyScale.y / cursorLine.rectTransform.lossyScale.y), cursorLine.rectTransform.localScale.z * (lossyScale.z / cursorLine.rectTransform.lossyScale.z));
                cursorLine.rectTransform.position = Vector3.zero;
                cursorLine.drawTransform = _curveContainer.transform;
                cursorLine.drawDepth = 1;
                cursorLine.Draw();

            }
           
        }

        private List<Vector2> GetGridPoints()
        {
            
            _curveGridCellSizePercent = gridSize / 2000f;

            Vector2 curveGridCellSize = new Vector2(graphZoomX * _curveGridCellSizePercent, graphZoomY * _curveGridCellSizePercent);
            List<Vector2> points = new List<Vector2>();

            float width = graphZoomX * gridSize;
            float height = graphZoomY * gridSize;

            int numColumns = Mathf.FloorToInt(width / curveGridCellSize.x);
            int numRows = Mathf.FloorToInt(height / curveGridCellSize.y);

            RectTransform contentRect = gridWindow.content;

            float xOffset = ((width - contentRect.rect.width) * 0.5f);
            float yOffset = ((height - contentRect.rect.height) * 0.5f);

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
       
            return points;
        }


        public void UpdateCurve()
        {
            GenerateCursor();

            if (!keyTimesMode)
            {
                if (_timeline._selectedKeyframes.Count == 0)
                {
                    GenerateGraphCurves(null);

                    // There are no keyframes selected, clear the graph.

                    if (_displayedCurveKeyframes.Count > 0)
                    {
                        foreach (var display in _displayedCurveKeyframes)
                            Destroy(display.gameObject);
                        _displayedCurveKeyframes.Clear();
                    }
                    if (_displayedCurveKeyframesHandles.Count > 0)
                    {
                        foreach (var pair in _displayedCurveKeyframesHandles)
                        {
                            Destroy(pair.Key.gameObject);
                            Destroy(pair.Value.gameObject);
                        }
                        _displayedCurveKeyframesHandles.Clear();
                    }
                    if (handlesLine != null)
                    {
                        handlesLine.points2.Clear();
                        handlesLine.active = false;
                    }
                    return;
                }

                mainCurve = _timeline._selectedKeyframes[0].Value.curve;


                foreach (KeyValuePair<float, Keyframe> pair in _timeline._selectedKeyframes)
                {
                    if (CompareCurves(mainCurve, pair.Value.curve) == false)
                    {
                        mainCurve = new AnimationCurve();
                        break;
                    }
                }
            }
            else
            {
                mainCurve = curveKeyTimes;

                //ApplyCurveToKeyframesValues(mainCurve, originalKeyframes, keyTimesKeyframesByParent, originalKeyframesValues);
                ApplyCurveToKeyframesTimes(mainCurve, originalKeyframes, keyTimesKeyframesByParent, keyTimesMinValue, keyTimesMaxValue);
            }

            GenerateGrid();
            GenerateGraphCurves(mainCurve);
            GenerateHandlesLines();
            DrawLines();

            AnimationCurve curve = mainCurve;
            int length = curve.length;
            float Multi = 1f;
            int displayIndex = 0;
            int handleIndex = 0;
            for (int i = 0; i < length; ++i)
            {
                UnityEngine.Keyframe curveKeyframe = curve[i];
                CurveKeyframeDisplay display;
                if (displayIndex < _displayedCurveKeyframes.Count)
                    display = _displayedCurveKeyframes[displayIndex];
                else
                {
                    display = new CurveKeyframeDisplay();
                    display.gameObject = Instantiate(_timeline._curveKeyframePrefab);
                    display.gameObject.hideFlags = HideFlags.None;
                    display.image = display.gameObject.transform.Find("RawImage").GetComponent<RawImage>();

                    display.image.enabled = true;

                    display.gameObject.transform.SetParent(gridRect);
                    display.gameObject.transform.localScale = Vector3.one;
                    display.gameObject.transform.localPosition = Vector3.zero;

                    display.pointerDownHandler = display.gameObject.AddComponent<PointerDownHandler>();
                    display.scrollHandler = display.gameObject.AddComponent<ScrollHandler>();
                    display.dragHandler = display.gameObject.AddComponent<DragHandler>();
                    display.pointerEnterHandler = display.gameObject.AddComponent<PointerEnterHandler>();

                    _displayedCurveKeyframes.Add(display);
                }

                int i1 = i;
                display.pointerDownHandler.onPointerDown = (e) =>
                {
                    if (e.button == PointerEventData.InputButton.Left)
                    {
                        _selectedKeyframeCurvePointIndex = i1;
                        _timeline._selectedKeyframeCurvePointIndex = i1;
                        _timeline.UpdateCurve();
                    }
                    if (e.button == PointerEventData.InputButton.Right)
                    {
                        float time =  curveKeyframe.time * ((RectTransform)_curveContainer.transform).rect.width;
                        float value = curveKeyframe.value * ((RectTransform)_curveContainer.transform).rect.height;
                            
                        float x = display.gameObject.transform.localPosition.x * -1;
                        float y = display.gameObject.transform.localPosition.y * -1;
                        gridWindow.content.localPosition = new Vector2(x + (((RectTransform)gridWindow.transform).rect.width / 2), y - (((RectTransform)gridWindow.transform).rect.height / 2));

                        _timeline.UpdateCurve();
                    }
                    if (i1 == 0 || i1 == curve.length - 1)
                        return;
                    if (e.button == PointerEventData.InputButton.Middle && Input.GetKey(KeyCode.LeftControl))
                    {
                        mainCurve.RemoveKey(i1);
                        _timeline.UpdateCurve();
                    }
                };
                display.scrollHandler.onScroll = (e) =>
                {
                    UnityEngine.Keyframe k = curve[i1];
                    float offset = e.scrollDelta.y > 0 ? Mathf.PI / 180f : -Mathf.PI / 180f;
                    mainCurve.RemoveKey(i1);

                    if (Input.GetKey(KeyCode.LeftControl))
                        k.inTangent = Mathf.Tan(Mathf.Atan(k.inTangent) + offset);
                    else if (Input.GetKey(KeyCode.LeftAlt))
                        k.outTangent = Mathf.Tan(Mathf.Atan(k.outTangent) + offset);
                    else
                    {
                        k.inTangent = Mathf.Tan(Mathf.Atan(k.inTangent) + offset);
                        k.outTangent = Mathf.Tan(Mathf.Atan(k.outTangent) + offset);
                    }
                    
                    mainCurve.AddKey(k);
                    _timeline.UpdateCurve();
                };
                display.dragHandler.onDrag = (e) =>
                {
                    i1 = _selectedKeyframeCurvePointIndex;

                    Vector2 localPoint;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_curveContainer.transform), e.position, e.pressEventCamera, out localPoint))
                    {
                        localPoint.x = Mathf.Clamp(localPoint.x, 0f, ((RectTransform)_curveContainer.transform).rect.width);
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            Vector2 curveGridCellSize = new Vector2(((RectTransform)_curveContainer.transform).rect.width * _curveGridCellSizePercent, ((RectTransform)_curveContainer.transform).rect.height * _curveGridCellSizePercent);
                            float mod = localPoint.x % curveGridCellSize.x;
                            if (mod / curveGridCellSize.x > 0.5f)
                                localPoint.x += curveGridCellSize.x - mod;
                            else
                                localPoint.x -= mod;
                            mod = localPoint.y % curveGridCellSize.y;
                            if (mod / curveGridCellSize.y > 0.5f)
                                localPoint.y += curveGridCellSize.y - mod;
                            else
                                localPoint.y -= mod;
                        }
                        if (i1 != 0 && i1 != curve.length - 1)
                            ((RectTransform)display.gameObject.transform).anchoredPosition = localPoint;
                        else
                            ((RectTransform)display.gameObject.transform).anchoredPosition = new Vector2(((RectTransform)display.gameObject.transform).anchoredPosition.x, localPoint.y);

                        float time  = (localPoint.x * Multi) / ((RectTransform)_curveContainer.transform).rect.width;
                        float value = (localPoint.y * Multi) / ((RectTransform)_curveContainer.transform).rect.height;


                        AnimationCurve originalCurve = new AnimationCurve(mainCurve.keys);

                        if (curve.keys.Any(k => k.time == time && curve.keys.ToList().IndexOf(k) != i1))
                        {
                            display.image.color = Color.red;

                            UnityEngine.Keyframe curveKey = originalCurve[i1];
                            originalCurve.RemoveKey(i1);

                            curveKey.value = value;
                            float add = 0.00001f;
                            while (curve.keys.Any(k => k.time == time))
                            {
                                time += add;
                            }

                            if (i1 != 0 && i1 != curve.length - 1)
                                curveKey.time = time;

                            originalCurve.AddKey(curveKey);

                            GenerateGraphCurves(originalCurve);
                            DrawLines();
                        }
                        else
                        {
                            display.image.color = i1 == _selectedKeyframeCurvePointIndex ? Color.green : (Color)new Color32(44, 153, 160, 255);
                           /*
                            UnityEngine.Keyframe curveKey = originalCurve[i1];
                            originalCurve.RemoveKey(i1);

                            curveKey.value = value;

                            if (i1 != 0 && i1 != curve.length - 1)
                                curveKey.time = time;

                            originalCurve.AddKey(curveKey);

                            GenerateGraphCurves(originalCurve);
                            DrawLines();
                           */
                            if (time >= 0 && time <= 1)
                            {
                                if (curve.keys.Any(k => k.time == time && curve.keys.ToList().IndexOf(k) != i1) == false)
                                {
                                    UnityEngine.Keyframe curveKey = curve[i1];
                                    if (i1 != 0 && i1 != curve.length - 1)
                                        curveKey.time = time;

                                    curveKey.value = value;

                                    mainCurve.RemoveKey(i1);
                                    _selectedKeyframeCurvePointIndex = mainCurve.AddKey(curveKey);
                                    _timeline._selectedKeyframeCurvePointIndex = _selectedKeyframeCurvePointIndex;
                                }
                            }
                            _timeline.SaveKeyframeCurve();
                            _timeline.UpdateCurve();
                        }
                    }
                };
                display.dragHandler.onEndDrag = (e) =>
                {
                    i1 = _selectedKeyframeCurvePointIndex;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_curveContainer.transform), e.position, e.pressEventCamera, out Vector2 localPoint))
                    {
                        localPoint.x = Mathf.Clamp(localPoint.x, 0f, ((RectTransform)_curveContainer.transform).rect.width);
                        float time = (localPoint.x * Multi) / ((RectTransform)_curveContainer.transform).rect.width;
                        float value = (localPoint.y * Multi) / ((RectTransform)_curveContainer.transform).rect.height;
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            float mod = time % _curveGridCellSizePercent;
                            if (mod / _curveGridCellSizePercent > 0.5f)
                                time += _curveGridCellSizePercent - mod;
                            else
                                time -= mod;
                            mod = value % _curveGridCellSizePercent;
                            if (mod / _curveGridCellSizePercent > 0.5f)
                                value += _curveGridCellSizePercent - mod;
                            else
                                value -= mod;
                        }
                        if (time >= 0 && time <= 1)
                        {
                            if (curve.keys.Any(k => k.time == time && curve.keys.ToList().IndexOf(k) != i1) == false)
                            {
                                UnityEngine.Keyframe curveKey = curve[i1];

                                if (i1 != 0 && i1 != curve.length - 1)
                                    curveKey.time = time;
                               
                                curveKey.value = value;

                                mainCurve.RemoveKey(i1);
                                _selectedKeyframeCurvePointIndex = mainCurve.AddKey(curveKey);
                                _timeline._selectedKeyframeCurvePointIndex = _selectedKeyframeCurvePointIndex;
                            }
                        }
                        _timeline.SaveKeyframeCurve();
                        _timeline.UpdateCurve();
                    }
                };
                display.pointerEnterHandler.onPointerEnter = (e) =>
                {
                    _timeline._tooltip.transform.parent.gameObject.SetActive(true);
                    UnityEngine.Keyframe k = curve[i1];
                    _timeline._tooltip.text = $"T: {k.time:0.000}, V: {k.value:0.###}\nIn: {Mathf.Atan(k.inTangent) * Mathf.Rad2Deg:0.#}, Out:{Mathf.Atan(k.outTangent) * Mathf.Rad2Deg:0.#}";
                };
                display.pointerEnterHandler.onPointerExit = (e) => { _timeline._tooltip.transform.parent.gameObject.SetActive(false); };


                display.image.color = i == _selectedKeyframeCurvePointIndex ? Color.green : (Color)new Color32(44, 153, 160, 255);

                display.gameObject.SetActive(true);

                // Curve key position
                ((RectTransform)display.gameObject.transform).anchoredPosition = new Vector2((curveKeyframe.time * Multi) * ((RectTransform)_curveContainer.transform).rect.width, (curveKeyframe.value * Multi) * ((RectTransform)_curveContainer.transform).rect.height);


                if (i == _selectedKeyframeCurvePointIndex)
                {
                    CurveKeyframeDisplay inHandle;
                    CurveKeyframeDisplay outHandle;
                    if (handleIndex < _displayedCurveKeyframesHandles.Count)
                    {
                        inHandle = _displayedCurveKeyframesHandles[handleIndex].Key;
                        outHandle = _displayedCurveKeyframesHandles[handleIndex].Value;
                    }
                    else
                    {
                        inHandle = new CurveKeyframeDisplay();
                        inHandle.gameObject = Instantiate(_timeline._curveKeyframePrefab);
                        inHandle.gameObject.hideFlags = HideFlags.None;
                        inHandle.image = inHandle.gameObject.transform.Find("RawImage").GetComponent<RawImage>();

                        inHandle.image.enabled = true;
                        inHandle.image.color = Color.red;

                        inHandle.gameObject.transform.SetParent(gridRect);
                        inHandle.gameObject.transform.localScale = Vector3.one;
                        inHandle.gameObject.transform.localPosition = Vector3.zero;

                        inHandle.dragHandler = inHandle.gameObject.AddComponent<DragHandler>();

                        outHandle = new CurveKeyframeDisplay();
                        outHandle.gameObject = Instantiate(_timeline._curveKeyframePrefab);
                        outHandle.gameObject.hideFlags = HideFlags.None;
                        outHandle.image = outHandle.gameObject.transform.Find("RawImage").GetComponent<RawImage>();

                        outHandle.image.enabled = true;
                        outHandle.image.color = Color.red;

                        outHandle.gameObject.transform.SetParent(gridRect);
                        outHandle.gameObject.transform.localScale = Vector3.one;
                        outHandle.gameObject.transform.localPosition = Vector3.zero;

                        outHandle.dragHandler = outHandle.gameObject.AddComponent<DragHandler>();

                        _displayedCurveKeyframesHandles.Add(new KeyValuePair<CurveKeyframeDisplay, CurveKeyframeDisplay>(inHandle, outHandle));
                    }

                    inHandle.dragHandler.onDrag = (e) =>
                    {
                        Vector2 localPoint;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_curveContainer.transform), e.position, e.pressEventCamera, out localPoint))
                        {
                            Vector2 handlePos = localPoint - ((RectTransform)display.gameObject.transform).anchoredPosition;

                            float inTangent = handlePos.y / handlePos.x;

                            UnityEngine.Keyframe curveKey = curve[i1];

                            curveKey.inTangent = inTangent;

                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                curveKey.outTangent = inTangent;
                            }

                            mainCurve.RemoveKey(i1);
                            mainCurve.AddKey(curveKey);

                            _timeline.SaveKeyframeCurve();
                            _timeline.UpdateCurve();
                        }
                    };
                    outHandle.dragHandler.onDrag = (e) =>
                    {
                        Vector2 localPoint;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_curveContainer.transform), e.position, e.pressEventCamera, out localPoint))
                        {
                            Vector2 handlePos = localPoint - ((RectTransform)display.gameObject.transform).anchoredPosition;

                            float outTangent = handlePos.y / handlePos.x;

                            UnityEngine.Keyframe curveKey = curve[i1];

                            curveKey.outTangent = outTangent;

                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                curveKey.inTangent = outTangent;
                            }

                            mainCurve.RemoveKey(i1);
                            mainCurve.AddKey(curveKey);

                            _timeline.SaveKeyframeCurve();
                            _timeline.UpdateCurve();
                        }
                    };
 
                    Vector3 inHandlePos = ((RectTransform)display.gameObject.transform).anchoredPosition - new Vector2(1, curveKeyframe.inTangent).normalized * 50f;
                    Vector3 outHandlePos = ((RectTransform)display.gameObject.transform).anchoredPosition + new Vector2(1, curveKeyframe.outTangent).normalized * 50f;

                    inHandle.gameObject.SetActive(true);
                    outHandle.gameObject.SetActive(true);

                    ((RectTransform)inHandle.gameObject.transform).anchoredPosition = inHandlePos;
                    ((RectTransform)outHandle.gameObject.transform).anchoredPosition = outHandlePos;
                    GenerateHandlesLines();

                    ++handleIndex;
                }               
                
                ++displayIndex;
            }

            for (; displayIndex < _displayedCurveKeyframes.Count; ++displayIndex)
                _displayedCurveKeyframes[displayIndex].gameObject.SetActive(false);

            for (; handleIndex < _displayedCurveKeyframesHandles.Count; ++handleIndex)
            {
                _displayedCurveKeyframesHandles[handleIndex].Key.gameObject.SetActive(false);
                _displayedCurveKeyframesHandles[handleIndex].Value.gameObject.SetActive(false);
            }

            

            _timeline.UpdateCurvePointTime();
            _timeline.UpdateCurvePointValue();
            _timeline.UpdateCurvePointInTangent();
            _timeline.UpdateCurvePointOutTangent();
        }

        private bool CompareCurves(AnimationCurve x, AnimationCurve y)
        {
            if (x.length != y.length)
                return false;
            for (int i = 0; i < x.length; i++)
            {
                UnityEngine.Keyframe keyX = x.keys[i];
                UnityEngine.Keyframe keyY = y.keys[i];
                if (keyX.time != keyY.time ||
                    keyX.value != keyY.value ||
                    keyX.inTangent != keyY.inTangent ||
                    keyX.outTangent != keyY.outTangent)
                    return false;
            }
            return true;
        }

        public static void ToggleKeyTimesMode()
        {
            if (!keyTimesMode && _timeline._selectedKeyframes.Count < 3)
            {
                ShalltyUtils.Logger.LogMessage("You must select at least 3 keyframes or more!");
                return;
            }

            keyTimesMode = !keyTimesMode;

            var keyframePanel = _timeline._keyframeWindow.transform.Find("Main Container/ShalltyUtilsPanel");
            var keyframePanelButton = _timeline._keyframeWindow.transform.Find("Main Container/ShalltyUtilsButton");

            if (keyframePanel.gameObject.activeSelf)
            {
                keyframePanel.gameObject.SetActive(false);
                keyframePanelButton.GetComponentInChildren<Text>().text = keyframePanel.gameObject.activeSelf ? "◄ ShalltyUtils" : "► ShalltyUtils";
            }

            _timeline._keyframeWindow.transform.Find("Close")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Main Fields")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Header")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Curve Point Time")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Curve Point Value")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Curve Point InTangent")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Curve Point OutTangent")?.gameObject.SetActive(!keyTimesMode);
            _timeline._keyframeWindow.transform.Find("Main Container/Curve Fields/Fields/Buttons")?.gameObject.SetActive(!keyTimesMode);

            foreach (VectorLine line in _graphEditor.allCurveLines)
                line.color = keyTimesMode ? Color.green : Color.cyan;


            if (!keyTimesMode && undoRedoTimeline.Value)
            {
                List<float> oldTimes = originalKeyframes.Select(pair => pair.Key).ToList();
                List<float> newTimes = _timeline._selectedKeyframes.Select(pair => pair.Key).ToList();


                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.DragAtCurrentTimeCommand(newTimes, oldTimes, new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes)));
            }


            originalKeyframes.Clear();
            keyTimesKeyframesByParent.Clear();
            keyTimesMaxValue = 0f;
            keyTimesMinValue = 0f;

            if (keyTimesMode)
            {
                curveKeyTimes = AnimationCurve.Linear(0f, 0f, 1f, 1f);

                originalKeyframes = _timeline._selectedKeyframes.ToList();
                originalKeyframesValues = _timeline._selectedKeyframes.Where(pair => pair.Value.value is Vector3).Select(pair => (Vector3)pair.Value.value).ToList();

                _timeline._selectedKeyframes.Clear();

                List <KeyValuePair<float, Keyframe>> keyframes = originalKeyframes.OrderBy(pair => pair.Key).ToList();

                keyTimesKeyframesByParent = new Dictionary<Interpolable, List<KeyValuePair<float, Keyframe>>>();
               

                foreach (KeyValuePair<float, Keyframe> pair in keyframes)
                {
                    Interpolable interpolable = pair.Value.parent;
                    if (!keyTimesKeyframesByParent.ContainsKey(interpolable))
                        keyTimesKeyframesByParent[interpolable] = new List<KeyValuePair<float, Keyframe>>();

                    keyTimesKeyframesByParent[interpolable].Add(pair);
                }

                keyTimesMinValue = keyframes.FirstOrDefault().Key;
                keyTimesMaxValue = keyframes.LastOrDefault().Key;
            }

            _graphEditor.UpdateCurve();
        }

        private static void ApplyCurveToKeyframesTimes(AnimationCurve curve, List<KeyValuePair<float, Keyframe>> originalKeyframes, Dictionary<Interpolable, List<KeyValuePair<float, Keyframe>>> keyframesByParent, float minValue, float maxValue)
        {
            if (originalKeyframes.Count > 2)
            {
                List<KeyValuePair<float, Keyframe>> keyframesToSelect = new List<KeyValuePair<float, Keyframe>>();

                foreach (var kvp in keyframesByParent)
                {
                    Interpolable interpolable = kvp.Key;
                    List<KeyValuePair<float, Keyframe>> parentKeyframes = kvp.Value.OrderBy(pair => pair.Key).ToList();


                    float minTime = parentKeyframes.FirstOrDefault().Key;
                    float maxTime = parentKeyframes.LastOrDefault().Key;

                    foreach (var keyframe in parentKeyframes)
                    {
                        float normalizedValue = Mathf.InverseLerp(minValue, maxValue, (float)keyframe.Key);
                        float newTime = Mathf.LerpUnclamped(minValue, maxValue, curve.Evaluate(normalizedValue));
                        float currentTime = interpolable.keyframes.Where(key => key.Value == keyframe.Value).FirstOrDefault().Key;

                        interpolable.keyframes.Remove(currentTime);
                        while (interpolable.keyframes.ContainsKey(newTime))
                            newTime += 0.001f;

                        interpolable.keyframes.Add(newTime, keyframe.Value);
                        keyframesToSelect.Add(new KeyValuePair<float, Keyframe>(newTime, keyframe.Value));
                    }
                }

                _timeline._selectedKeyframes.Clear();
                _timeline._selectedKeyframes.AddRange(keyframesToSelect);
                _timeline.UpdateGrid();
            }
        }
    }
}