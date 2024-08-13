extern alias aliasTimeline;
using aliasTimeline::Timeline;
using aliasTimeline.UILib.EventHandlers;
using HarmonyLib;
using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vectrosity;
using static aliasTimeline::Timeline.Timeline;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;

namespace ShalltyUtils
{
    public class PerformanceMode
    {
        public static bool performanceMode = true;

        private static KeyframeDisplay display;

        private static float lastRightClickTime = 0;
        private static float lastLeftClickTime = 0;

        private static List<VectorLine> allKeyframesLines = new List<VectorLine>();
        private static List<VectorLine> allSelectedKeyframesLines = new List<VectorLine>();
        private static List<KeyValuePair<float, Keyframe>> allKeyframes = new List<KeyValuePair<float, Keyframe>>();
        private static HashSet<KeyValuePair<float, Keyframe>> allKeyframesSet = new HashSet<KeyValuePair<float, Keyframe>>();
        private static List<Vector2> allKeyframesPoints = new List<Vector2>();

        private static List<KeyValuePair<int, VectorLine>> allPointsDict = new List<KeyValuePair<int, VectorLine>>();
        private static List<KeyValuePair<int, VectorLine>> allSelectedPointsDict = new List<KeyValuePair<int, VectorLine>>();

        private static Dictionary<int, float> selectedKeyframesXOffset = new Dictionary<int, float>();

        private static bool keyframeHover = false;
        private static bool isDraggin = false;

        public static void TogglePerformanceMode()
        {
            performanceMode = !performanceMode;

            if (!performanceMode)
            {
                if (display != null && display.gameObject != null)
                {
                    GameObject.DestroyImmediate(display.gameObject);
                    display = null;
                }

                if (allKeyframesLines.Count > 0)
                {
                    VectorLine.Destroy(allKeyframesLines);
                    allKeyframesLines.Clear();
                }

                if (allSelectedKeyframesLines.Count > 0)
                {
                    VectorLine.Destroy(allSelectedKeyframesLines);
                    allSelectedKeyframesLines.Clear();
                }

                if (KeyframesGroups.allKeyframeGroups.Count > 0)
                {
                    foreach (var group in KeyframesGroups.allKeyframeGroups)
                    {
                        if (group.performanceModeLines.Count > 0)
                        {
                            VectorLine.Destroy(group.performanceModeLines);
                            group.performanceModeLines.Clear();
                        }
                    }
                }


                allKeyframes.Clear();
                allKeyframesSet.Clear();
                allKeyframesPoints.Clear();
                allPointsDict.Clear();
                allSelectedPointsDict.Clear();
                selectedKeyframesXOffset.Clear();

                keyframeHover = false;
                isDraggin = false;
            }
            
            foreach (KeyframeDisplay display in _timeline._displayedKeyframes)
            {
                if (display != null && display.gameObject != null)
                {
                    GameObject.DestroyImmediate(display.gameObject);
                }
            }



            _timeline._displayedKeyframes.Clear();
            

            _timeline.UpdateGrid();

            ShalltyUtils.Logger.LogMessage("Timeline - Performance mode: " + (performanceMode ? "ON" : "OFF" ));

            int totalKeyframes = 0;
            foreach (var kvp in _timeline._interpolables)
            {
                Interpolable interpolable = kvp.Value;

                totalKeyframes += interpolable.keyframes.Count;
            }

            ShalltyUtils.Logger.LogMessage($"Timeline - Keyframes count: {totalKeyframes}");
        }

        public static void UpdateKeyframes()
        {
            if (!performanceMode) return;

            if (allKeyframes.Count > 0 && allKeyframesLines.Count > 0 && _timeline._ui.gameObject.activeSelf)
            {
                Vector3 lossyScale = Vector3.one;

                for (int i = 0; i < allKeyframesLines.Count; i++)
                {
                    VectorLine line = allKeyframesLines[i];

                    if (line.points2.Count == 0 || line == null)
                        continue;

                    line.rectTransform.localScale = new Vector3(line.rectTransform.localScale.x * (lossyScale.x / line.rectTransform.lossyScale.x), line.rectTransform.localScale.y * (lossyScale.y / line.rectTransform.lossyScale.y), line.rectTransform.localScale.z * (lossyScale.z / line.rectTransform.lossyScale.z));
                    line.rectTransform.position = Vector3.zero;
                    line.drawTransform = _timeline._keyframesContainer;
                    line.drawDepth = 2;
                    line.Draw();

                    int pointIndex = 0;
                    if (line.Selected(Input.mousePosition, out pointIndex) && pointIndex >= 0 && pointIndex < line.points2.Count && !isDraggin)
                    {
                        int totalIndex = 0;
                        if (pointIndex >= 0 && pointIndex < line.points2.Count)
                        {
                            for (int j = 0; j < i; j++)
                            {
                                totalIndex += allKeyframesLines[j].points2.Count;
                            }
                            totalIndex += pointIndex;
                        }

                        if (display == null)
                        {
                            display = new KeyframeDisplay();

                            display.gameObject = GameObject.Instantiate(_timeline._keyframePrefab);
                            display.gameObject.hideFlags = HideFlags.None;
                            display.image = display.gameObject.transform.Find("RawImage").GetComponent<RawImage>();
                            display.image.enabled = false;

                            display.gameObject.transform.SetParent(_timeline._keyframesContainer);
                            display.gameObject.transform.localPosition = Vector3.zero;
                            display.gameObject.transform.localScale = Vector3.one;
                        }

                        Color color = _timeline._selectedKeyframes.Any(k => k.Value == allKeyframes[totalIndex].Value) ? Color.green : Color.red;
                        display.image.color = color;
                        //display.image.enabled = true;
                        display.gameObject.SetActive(true);

                        if (!_timeline._displayedKeyframes.Contains(display))
                            _timeline._displayedKeyframes.Add(display);

                        //((RectTransform)display.gameObject.transform).anchoredPosition = line.points2[pointIndex];
                        display.gameObject.transform.position = Input.mousePosition;
                        display.keyframe = allKeyframes[totalIndex].Value;
                        
                        PointerEnterHandler pointerEnter = display.gameObject.GetOrAddComponent<PointerEnterHandler>();
                        pointerEnter.onPointerEnter = (e) =>
                        {
                            if (isDraggin) return;

                            _timeline._tooltip.transform.parent.gameObject.SetActive(true);
                            KeyValuePair<float, Keyframe> matchingKeyframePair = display.keyframe.parent.keyframes.FirstOrDefault(k => k.Value == display.keyframe);
                            float t = matchingKeyframePair.Value != null ? matchingKeyframePair.Key : 0f;
                            _timeline._tooltip.text = $"T: {Mathf.FloorToInt(t / 60):00}:{t % 60:00.########}\nV: {display.keyframe.value}";
                        };
                        pointerEnter.onPointerExit = (e) =>
                        {
                            _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                        };

                        PointerDownHandler pointerDown = display.gameObject.GetOrAddComponent<PointerDownHandler>();
                        pointerDown.onPointerDown = (e) =>
                        {
                            if (Input.GetKey(KeyCode.LeftAlt))
                                return;
                            switch (e.button)
                            {
                                case PointerEventData.InputButton.Left:
                                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                                        _timeline.SelectAddKeyframes(display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe));
                                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                    {
                                        KeyValuePair<float, Keyframe> lastSelected = _timeline._selectedKeyframes.LastOrDefault(k => k.Value.parent == display.keyframe.parent);
                                        if (lastSelected.Value != null)
                                        {
                                            KeyValuePair<float, Keyframe> selectingNow = display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe);
                                            float minTime;
                                            float maxTime;
                                            if (lastSelected.Key < selectingNow.Key)
                                            {
                                                minTime = lastSelected.Key;
                                                maxTime = selectingNow.Key;
                                            }
                                            else
                                            {
                                                minTime = selectingNow.Key;
                                                maxTime = lastSelected.Key;
                                            }
                                            _timeline.SelectAddKeyframes(display.keyframe.parent.keyframes.Where(k => k.Key > minTime && k.Key < maxTime));
                                            _timeline.SelectAddKeyframes(selectingNow);
                                        }
                                        else
                                            _timeline.SelectAddKeyframes(display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe));
                                    }
                                    else
                                    {
                                        _timeline.SelectKeyframes(display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe));

                                        if (_timeline._selectedKeyframes.Count > 0)
                                        {
                                            if (Time.unscaledTime - lastLeftClickTime <= 0.2f)
                                            {
                                                List<KeyValuePair<float, Keyframe>> keyframesToSelect = _timeline._selectedKeyframes[0].Value.parent.keyframes.Where(k => Equals(k.Value.value, _timeline._selectedKeyframes[0].Value.value)).ToList();
                                                _timeline._selectedKeyframes.Clear();
                                                if (keyframesToSelect.Count != 0)
                                                    _timeline.SelectAddKeyframes(keyframesToSelect);
                                            }
                                        }
                                        lastLeftClickTime = Time.unscaledTime;
                                    }

                                    break;

                                case PointerEventData.InputButton.Right:
                                    _timeline.SeekPlaybackTime(display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe).Key);

                                    if (Time.unscaledTime - lastRightClickTime <= 0.2f)
                                    {
                                        List<Interpolable> currentlySelectedInterpolables = new List<Interpolable>(_timeline._selectedInterpolables);
                                        if (currentlySelectedInterpolables.Count == 0) return;

                                        List<KeyValuePair<float, Keyframe>> toSelect = new List<KeyValuePair<float, Keyframe>>();
                                        float currentTime = _timeline._playbackTime % _timeline._duration;

                                        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                                        {
                                            foreach (Interpolable selected in currentlySelectedInterpolables)
                                                toSelect.AddRange(selected.keyframes.Where(k => k.Key == currentTime));

                                            _timeline.SelectAddKeyframes(toSelect);
                                        }
                                        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                        {
                                            KeyValuePair<float, Keyframe> lastSelected = _timeline._selectedKeyframes.LastOrDefault(k => k.Value.parent == display.keyframe.parent);
                                            if (lastSelected.Value != null)
                                            {
                                                KeyValuePair<float, Keyframe> selectingNow = display.keyframe.parent.keyframes.First(k => k.Value == display.keyframe);
                                                float minTime;
                                                float maxTime;
                                                if (lastSelected.Key < selectingNow.Key)
                                                {
                                                    minTime = lastSelected.Key;
                                                    maxTime = selectingNow.Key;
                                                }
                                                else
                                                {
                                                    minTime = selectingNow.Key;
                                                    maxTime = lastSelected.Key;
                                                }

                                                foreach (Interpolable selected in currentlySelectedInterpolables)
                                                    toSelect.AddRange(selected.keyframes.Where(k => k.Key > minTime && k.Key < maxTime));
                                            }
                                            
                                            foreach (Interpolable selected in currentlySelectedInterpolables)
                                                toSelect.AddRange(selected.keyframes.Where(k => k.Key == currentTime));
                                            

                                            _timeline.SelectAddKeyframes(toSelect);

                                        }
                                        else
                                        {
                                            foreach (Interpolable selected in currentlySelectedInterpolables)
                                                toSelect.AddRange(selected.keyframes.Where(k => k.Key == currentTime));

                                            _timeline.SelectKeyframes(toSelect);
                                        }

                                       
                                    }

                                    lastRightClickTime = Time.unscaledTime;

                                break;

                                case PointerEventData.InputButton.Middle:
                                    if (Input.GetKey(KeyCode.LeftControl))
                                    {
                                        List<KeyValuePair<float, Keyframe>> toDelete = new List<KeyValuePair<float, Keyframe>>();
                                        if (Input.GetKey(KeyCode.LeftShift))
                                            toDelete.AddRange(_timeline._selectedKeyframes);
                                        KeyValuePair<float, Keyframe> kPair = display.keyframe.parent.keyframes.FirstOrDefault(k => k.Value == display.keyframe);
                                        if (kPair.Value != null)
                                            toDelete.Add(kPair);
                                        if (toDelete.Count != 0)
                                        {
                                            _timeline.DeleteKeyframes(toDelete);
                                            _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                                        }
                                    }
                                    break;
                            }
                        };

                        DragHandler dragHandler = display.gameObject.GetOrAddComponent<DragHandler>();
                        dragHandler.onBeginDrag = e =>
                        {
                            if (!Input.GetKey(KeyCode.LeftAlt) || (isDraggin))
                                return;
                            isDraggin = true;
                            Vector2 localPoint;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_timeline._keyframesContainer, e.position, e.pressEventCamera, out localPoint))
                            {
                                _timeline._selectedKeyframesXOffset.Clear();
                                selectedKeyframesXOffset.Clear();
                                foreach (KeyValuePair<float, Keyframe> selectedKeyframe in _timeline._selectedKeyframes)
                                {
                                    int keyIndex = allKeyframes.FindIndex(k => k.Value == selectedKeyframe.Value);
                                    KeyValuePair<int, VectorLine> newList = allPointsDict[keyIndex];

                                    VectorLine indexLine = newList.Value;
                                    int index = newList.Key;

                                    selectedKeyframesXOffset.Add(keyIndex, indexLine.points2[index].x - localPoint.x);
                                }
                                if (_timeline._selectedKeyframes.Any(pair => pair.Value == display.keyframe) && !_timeline._selectedKeyframesXOffset.ContainsKey(display))
                                    _timeline._selectedKeyframesXOffset.Add(display, ((RectTransform)display.gameObject.transform).anchoredPosition.x - localPoint.x);
                            }
                            if (selectedKeyframesXOffset.Count != 0)
                                isPlaying = false;
                            e.Reset();
                        };
                        dragHandler.onDrag = e =>
                        {
                            if (selectedKeyframesXOffset.Count == 0)
                            {
                                isDraggin = false;
                                return;
                            }
                                
                            Vector2 localPoint;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_timeline._keyframesContainer, e.position, e.pressEventCamera, out localPoint))
                            {
                                int displayIndex = allKeyframes.FindIndex(k => k.Value == display.keyframe);
                                float x = localPoint.x;
                                foreach (KeyValuePair<int, float> pair in selectedKeyframesXOffset)
                                {
                                    float localX = localPoint.x + pair.Value;
                                    if (localX < 0f)
                                        x = localPoint.x - localX;
                                }

                                if (Input.GetKey(KeyCode.LeftShift))
                                {
                                    float time = 10f * x / (_baseGridWidth * _timeline._zoomLevel);
                                    float beat = _timeline._blockLength / _timeline._divisions;
                                    float mod = time % beat;
                                    if (mod / beat > 0.5f)
                                        time += beat - mod;
                                    else
                                        time -= mod;
                                    x = (time * _baseGridWidth * _timeline._zoomLevel) / 10f - selectedKeyframesXOffset[displayIndex];
                                }

                                int sIndex = 0;
                                foreach (KeyValuePair<int, float> pair in selectedKeyframesXOffset)
                                {
                                    KeyValuePair<int, VectorLine> newList = allPointsDict[pair.Key];

                                    VectorLine indexLine = newList.Value;
                                    int index = newList.Key;

                                    indexLine.points2[index] = new Vector2(x + pair.Value, indexLine.points2[index].y);

                                    /// Selected keyframes
                                    ///
                                    if (allSelectedPointsDict.Count > 0)
                                    {
                                        KeyValuePair<int, VectorLine> selectedPair = allSelectedPointsDict[sIndex];

                                        selectedPair.Value.points2[selectedPair.Key] = new Vector2(x + pair.Value, selectedPair.Value.points2[selectedPair.Key].y);

                                        sIndex++;
                                    }
                                }

                                foreach (KeyValuePair<KeyframeDisplay, float> pair in _timeline._selectedKeyframesXOffset)
                                {
                                    RectTransform rt = ((RectTransform)pair.Key.gameObject.transform);
                                    rt.anchoredPosition = new Vector2(x + pair.Value, rt.anchoredPosition.y);
                                }
                            }
                            e.Reset();
                        };
                        dragHandler.onEndDrag = e =>
                        {

                            if (selectedKeyframesXOffset.Count == 0)
                            {
                                isDraggin = false;
                                return;
                            }

                            _timeline._selectedKeyframes.Clear();

                            List<KeyValuePair<float, Keyframe>> keyframesToSelect = new List<KeyValuePair<float, Keyframe>>();
                            List<Keyframe> movedKeyframes = new List<Keyframe>();
                            List<float> newTimes = new List<float>();
                            List<float> oldTimes = new List<float>();

                            foreach (KeyValuePair<int, float> pair in selectedKeyframesXOffset)
                            {
                                KeyValuePair<int, VectorLine> newList = allPointsDict[pair.Key];

                                VectorLine indexLine = newList.Value;
                                int index = newList.Key;
                                Keyframe keyframe = allKeyframes[pair.Key].Value;

                                float time = 10f * indexLine.points2[index].x / (_baseGridWidth * _timeline._zoomLevel);

                                oldTimes.Add(allKeyframes[pair.Key].Key);
                                movedKeyframes.Add(keyframe);
                                newTimes.Add(time);

                                int currentIndex = keyframe.parent.keyframes.IndexOfValue(keyframe);
                                if (currentIndex != -1)
                                {
                                    keyframe.parent.keyframes.RemoveAt(currentIndex);
                                    keyframe.parent.keyframes.Add(time, keyframe);
                                }

                                keyframesToSelect.Add(new KeyValuePair<float, Keyframe>(time, allKeyframes[pair.Key].Value));
                                
                                //_timeline.MoveKeyframe(allKeyframes[pair.Key].Value, time);

                                //int selectIndex = _timeline._selectedKeyframes.FindIndex(k => k.Value == allKeyframes[pair.Key].Value);
                                //_timeline._selectedKeyframes[selectIndex] = new KeyValuePair<float, Keyframe>(time, allKeyframes[pair.Key].Value);
                            } 

                            if (undoRedoTimeline.Value)
                                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.MoveMultipleKeyframeCommand(movedKeyframes, newTimes, oldTimes));

                            _timeline._selectedKeyframes.AddRange(keyframesToSelect);

                            e.Reset();
                            _timeline.UpdateKeyframeWindow(false);
                            _timeline._selectedKeyframesXOffset.Clear();
                            selectedKeyframesXOffset.Clear();

                            if (display != null)
                            {
                                display.gameObject.SetActive(false);
                                _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                                if (_timeline._displayedKeyframes.Contains(display))
                                    _timeline._displayedKeyframes.Remove(display);
                            }

                            //keyframeHover = false;
                            isDraggin = false;
                            _timeline.UpdateGrid();
                        };
                    }
                    else
                    {/*
                        if (keyframeHover)
                        {
                            if (Input.GetKey(KeyCode.LeftAlt) && isDraggin)
                            {
                                keyframeHover = false;
                                continue;
                            }

                            if (display != null)
                            {
                                //display.gameObject.SetActive(false);
                                _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                                if (_timeline._displayedKeyframes.Contains(display))
                                    _timeline._displayedKeyframes.Remove(display);
                            }
                            //_timeline.UpdateGrid();
                            keyframeHover = false;
                        }*/
                    }
                }

                keyframeHover = allKeyframesLines.Any(line => line.Selected(Input.mousePosition));

                // Draw keyframes groups lines.
                foreach (var group in KeyframesGroups.allKeyframeGroups)
                {
                    if (group != null)
                    {
                        for (int i = 0; i < group.performanceModeLines.Count; i++)
                        {
                            VectorLine line = group.performanceModeLines[i];
                            if (line == null) continue;

                            if (line.points2.Count == 0) continue;

                            line.rectTransform.localScale = new Vector3(line.rectTransform.localScale.x * (lossyScale.x / line.rectTransform.lossyScale.x), line.rectTransform.localScale.y * (lossyScale.y / line.rectTransform.lossyScale.y), line.rectTransform.localScale.z * (lossyScale.z / line.rectTransform.lossyScale.z));
                            line.rectTransform.position = Vector3.zero;
                            line.drawTransform = _timeline._keyframesContainer;

                            line.Draw();
                        }
                    }
                    else
                    {
                        VectorLine.Destroy(group.performanceModeLines);
                        group.performanceModeLines.Clear();
                    }
                }
                    
                

                // Draw selected keyframes lines.
                if (_timeline._selectedKeyframes.Count > 0 && allSelectedKeyframesLines.Count > 0)
                {
                    for (int i = 0; i < allSelectedKeyframesLines.Count; i++)
                    {
                        VectorLine line = allSelectedKeyframesLines[i];

                        if (line == null) continue;

                        if (line.points2.Count == 0)
                            continue;

                        line.rectTransform.localScale = new Vector3(line.rectTransform.localScale.x * (lossyScale.x / line.rectTransform.lossyScale.x), line.rectTransform.localScale.y * (lossyScale.y / line.rectTransform.lossyScale.y), line.rectTransform.localScale.z * (lossyScale.z / line.rectTransform.lossyScale.z));
                        line.rectTransform.position = Vector3.zero;
                        line.drawTransform = _timeline._keyframesContainer;
                        line.drawDepth = 999;

                        line.Draw();
                    }
                }
                else if (_timeline._selectedKeyframes.Count == 0 && allSelectedKeyframesLines.Count > 0)
                {
                    VectorLine.Destroy(allSelectedKeyframesLines);
                    allSelectedKeyframesLines.Clear();
                }


            }
            else if (allKeyframes.Count == 0)
            {
                if (allKeyframesLines.Count > 0)
                {
                    VectorLine.Destroy(allKeyframesLines);
                    allKeyframesLines.Clear();
                }

                if (allSelectedKeyframesLines.Count > 0)
                {
                    VectorLine.Destroy(allSelectedKeyframesLines);
                    allSelectedKeyframesLines.Clear();
                }

                if (KeyframesGroups.allKeyframeGroups.Count > 0)
                {
                    foreach (var group in KeyframesGroups.allKeyframeGroups)
                    {
                        if (group.performanceModeLines.Count > 0)
                        {
                            VectorLine.Destroy(group.performanceModeLines);
                            group.performanceModeLines.Clear();
                        }
                    }
                }
            }

            if (display != null && display.gameObject.activeSelf && !keyframeHover && !isDraggin)
            {
                display.gameObject.SetActive(false);
                _timeline._tooltip.transform.parent.gameObject.SetActive(false);

                if (_timeline._displayedKeyframes.Contains(display))
                    _timeline._displayedKeyframes.Remove(display);
            }
        }

        private static void GenerateKeyframesLines(List<VectorLine> lineList, List<Vector2> allPoints)
        {
            if (!performanceMode) return;

            int linesCount = Mathf.CeilToInt((allPoints.Count) / 8192f);

            foreach (VectorLine line in lineList)
                line.points2.Clear();

            if (linesCount > 0)
            {
                List<List<Vector2>> lines = SplitPointList(allPoints, linesCount);

                /// CREATE NEW LINES
                if (lineList.Count < linesCount)
                {
                    int linesToGo = linesCount - lineList.Count;

                    for (int i = 0; i < linesToGo; i++)
                    {
                        string name = lineList == allKeyframesLines ? "KeyframesLine" : "SelectedKeyframesLine";
                        Color color = lineList == allKeyframesLines ? Color.red : Color.green;
                        VectorLine newLine = new VectorLine($"{name} ({lineList.Count + 1})", new List<Vector2> { Vector2.zero, Vector2.one }, keyframesSize.Value, LineType.Points, Joins.None);
                        newLine.active = true;
                        newLine.SetCanvas(_timeline._horizontalScrollView.content.gameObject);
                        newLine.SetMask(_timeline._keyframesContainer.parent.gameObject);
                        newLine.color = color;
                        newLine.texture = keyframeTexture;
                        lineList.Add(newLine);
                    }
                }

                /// GENERATE LINES POINTS
                for (int i = 0; i < lines.Count; i++)
                {
                    VectorLine newLine = lineList[i];
                    newLine.points2 = lines[i];
                }
            }

            /// REMOVE EMPTY LINES
          

            List<VectorLine> linesToDelete = new List<VectorLine>();
            foreach (VectorLine line in lineList)
            {
                if (line.points2.Count == 0)
                    linesToDelete.Add(line);
            }

            foreach (VectorLine line in linesToDelete)
                lineList.Remove(line);

            VectorLine.Destroy(linesToDelete);

            /// FILL DICTIONARIES

            if (lineList == allKeyframesLines)
                allPointsDict.Clear();
            else
                allSelectedPointsDict.Clear();

            for (int i = 0; i < lineList.Count; i++)
            {
                for (int j = 0; j < lineList[i].points2.Count; j++)
                {
                    if (lineList == allKeyframesLines)
                        allPointsDict.Add(new KeyValuePair<int, VectorLine>(j, lineList[i]));
                    else
                        allSelectedPointsDict.Add(new KeyValuePair<int, VectorLine>(j, lineList[i]));
                }
            }
        }

        public static List<List<Vector2>> SplitPointList(List<Vector2> inputList, int splitCount)
        {
            List<List<Vector2>> splitPoints = new List<List<Vector2>>();

            int totalKeys = inputList.Count;
            int keysPerSplit = Mathf.CeilToInt((float)totalKeys / splitCount);

            for (int i = 0; i < splitCount; i++)
            {
                List<Vector2> splitList = new List<Vector2>();

                int startIndex = i * keysPerSplit;
                int endIndex = Mathf.Min((i + 1) * keysPerSplit, totalKeys);

                for (int j = startIndex; j < endIndex; j++)
                    splitList.Add(inputList[j]);

                splitPoints.Add(splitList);
            }

            return splitPoints;
        }

        public static class Hooks
        {
            [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.ShouldShowInterpolable))]
            static bool ShouldShowInterpolablePrefix(Interpolable interpolable, bool showAll, ref bool __result)
            {
                if (!performanceMode) return true;

                selectedObjects = KKAPI.Studio.StudioAPI.GetSelectedObjects();
                if (showAll == false && ((interpolable.oci != null && !(selectedObjects.Contains(interpolable.oci))) || !interpolable.ShouldShow()))
                {
                    __result = false;
                    return false;
                }

                if (!_timeline.IsFilterInterpolationMatch(interpolable))
                {
                    __result = false;
                    return false;
                }
            
                __result = true;
                return true;
            }

            [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UpdateKeyframeSelection))]
            static void UpdateKeyframeSelectionPostfix()
            {
                if (performanceMode)
                {
                   KeyframesGroups.UpdateGroupKeyframesLines();

                    if (_timeline._selectedKeyframes.Count == 0)
                        return;

                    List<Vector2> selectedPoints = new List<Vector2>(_timeline._selectedKeyframes.Count);
                    foreach (var pair in _timeline._selectedKeyframes)
                    {
                        if (!allKeyframesSet.Contains(pair))
                            continue;

                        InterpolableDisplay interpolableDisplay = _timeline._displayedInterpolables.Where(dsp => dsp.interpolable.obj == pair.Value.parent).FirstOrDefault();
                        if (interpolableDisplay == null) return;

                        selectedPoints.Add(new Vector2(_baseGridWidth * _timeline._zoomLevel * pair.Key / 10f, ((RectTransform)interpolableDisplay.gameObject.transform).anchoredPosition.y));
                    }

                    GenerateKeyframesLines(allSelectedKeyframesLines, selectedPoints);
                }
                else
                {
                    KeyframesGroups.UpdateGroupKeyframes();
                }
            }

            [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UpdateKeyframesTree))]
            static bool UpdateKeyframesTreePrefix(List<INode> nodes, bool showAll, ref int interpolableIndex, ref int keyframeIndex)
            {
                if (performanceMode)
                {
                    allKeyframesPoints.Clear();
                    allKeyframes.Clear();
                    allKeyframesSet.Clear();

                    NewUpdateKeyframesTree(nodes, showAll, ref interpolableIndex, ref keyframeIndex, ref allKeyframesPoints);
                    GenerateKeyframesLines(allKeyframesLines, allKeyframesPoints);

                    return false;
                }
                else
                    return true;
            }

            static void NewUpdateKeyframesTree(List<INode> nodes, bool showAll, ref int interpolableIndex, ref int keyframeIndex, ref List<Vector2> allKeyframesPoints)
            {
                foreach (INode node in nodes)
                {
                    switch (node.type)
                    {
                        case INodeType.Leaf:

                            Interpolable interpolable = ((LeafNode<Interpolable>)node).obj;
                            if (_timeline.ShouldShowInterpolable(interpolable, showAll) == false) continue;

                            InterpolableDisplay interpolableDisplay = _timeline._displayedInterpolables[interpolableIndex];

                            foreach (KeyValuePair<float, Keyframe> keyframePair in interpolable.keyframes)
                            {
                                allKeyframesPoints.Add(new Vector2(_baseGridWidth * _timeline._zoomLevel * keyframePair.Key / 10f, ((RectTransform)interpolableDisplay.gameObject.transform).anchoredPosition.y));
                                allKeyframes.Add(keyframePair);
                                allKeyframesSet.Add(keyframePair);

                                ++keyframeIndex;
                            }
                            ++interpolableIndex;
                            break;

                        case INodeType.Group:
                            GroupNode<InterpolableGroup> group = (GroupNode<InterpolableGroup>)node;
                            if (group.obj.expanded)
                                NewUpdateKeyframesTree(group.children, showAll, ref interpolableIndex, ref keyframeIndex, ref allKeyframesPoints);
                            break;
                    }
                }
            }
        }
    }
}