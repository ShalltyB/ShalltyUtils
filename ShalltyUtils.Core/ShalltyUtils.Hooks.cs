extern alias aliasTimeline;

using aliasTimeline::Timeline;
using aliasTimeline.UILib.EventHandlers;
using HarmonyLib;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ToolBox.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;

namespace ShalltyUtils
{
    class Hooks
    {

        #region KKPE HOOKS

        [HarmonyPostfix, HarmonyPatch(typeof(HSPE.AMModules.BonesEditor), nameof(HSPE.AMModules.BonesEditor.SetBoneNotDirtyIf))]
        private static void SetBoneNotDirtyIfPostfix(GameObject go, HSPE.AMModules.BonesEditor __instance)
        {
            if (enableKKPEGuideObject.Value)
            {
                if (kkpeGuideObject != null && Singleton<GuideObjectManager>.Instance.selectObject == kkpeGuideObject && kkpeGuideObject.transformTarget == go.transform)
                {
                    _self.ExecuteDelayed2(() => CreateKKPEGuideObject()); 
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSPE.AMModules.BonesEditor), nameof(HSPE.AMModules.BonesEditor.ChangeBoneTarget))]
        private static void ChangeBoneTargetPostfix(Transform newTarget)
        {
            if (enableKKPEGuideObject.Value)
            {
                if (kkpeShowGuideObject)
                    CreateKKPEGuideObject();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(HSPE.AMModules.BonesEditor), nameof(HSPE.AMModules.BonesEditor.GizmosEnabled))]
        private static bool GizmosEnabledPostfix(bool __result)
        {
            if (enableKKPEGuideObject.Value)
            {
                kkpeShowGuideObject = __result;
                if (!__result)
                    Singleton<GuideObjectManager>.Instance.Delete(kkpeGuideObject, true);
                return false;
            }
            else
                return __result;
        }

        #endregion


        #region TIMELINE HOOKS

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UpdateCursor))]
        static void UpdateCursorPostfix()
        {
            if (_graphEditor != null)
                _graphEditor.UpdateCursorTime(true);
        }

        static IEnumerator OnGuideObjectCreatePostfixCoroutine(GuideObject __result)
        {
            yield return null;

            /// ENABLE MULTIPLE SELECTION   
            __result.enableMaluti = true;


            /// MOTION PATH CONTROLLER
            ///
            __result.changeAmount.onChangePosAfter += () =>
            {
                if (!MotionPath.showMotionPath) return;

                if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl))
                {
                    try
                    {
                        GuideObject selectedGuideObject = __result;

                        if (selectedGuideObject != Singleton<GuideObjectManager>.Instance.selectObject) return;
                        Timeline.Interpolable interpolable = _timeline._interpolables.Values.FirstOrDefault(pair => pair.id == "guideObjectPos" && ((GuideObject)pair.parameter) == selectedGuideObject);
                        if (interpolable == null) return;
                        
                        float ktime = _timeline._playbackTime;
                        if (!interpolable.keyframes.ContainsKey(ktime))
                        {
                            _timeline.AddKeyframe(interpolable, ktime);

                            List<KeyValuePair<float, Keyframe>> selectedKeyframes = new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes);
                            selectedKeyframes.Add(new KeyValuePair<float, Keyframe>(ktime, interpolable.keyframes[ktime]));
                            _timeline.SelectKeyframes(selectedKeyframes);

                        }
                        else if (addReplaceKeyframe.Value)
                        {
                            interpolable.keyframes[ktime].value = interpolable.GetValue();
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
            };

        }

        [HarmonyPostfix, HarmonyPatch(typeof(GuideObjectManager), nameof(GuideObjectManager.Add))]
        static void OnGuideObjectCreatePostfix(ref GuideObject __result)
        {
            _self.StartCoroutine(OnGuideObjectCreatePostfixCoroutine(__result));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GuideBase), nameof(GuideBase.OnPointerEnter))]
        static void GuideBaseEnterPostfix(PointerEventData eventData, GuideBase __instance)
        {
            if (__instance.name == "Sphere")
            {
                if (!Singleton<GuideObjectManager>.Instance.isOperationTarget)
                {
                    if (Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(__instance.guideObject))
                        __instance.colorNow = new Color(0f, 1f, 1f, 0.5f);
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(GuideBase), nameof(GuideBase.OnPointerExit))]
        static void GuideBaseExitPostfix(PointerEventData eventData, GuideBase __instance)
        {
            if (__instance.name == "Sphere")
            {
                if (!__instance.isDrag)
                {
                    if (Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(__instance.guideObject))
                        __instance.colorNow = new Color(0f, 1f, 1f, 0.5f);
                }
            }
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(GuideObjectManager), nameof(GuideObjectManager.SetSelectObject))]
        static void SetSelectObjectPostfix(GuideObjectManager __instance, GuideObject _object, bool _multiple = true)
        {
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

            GuideObjectPicker.UpdateAllColors();

            GuideObjectPicker.UpdateEditButtonsWindow();

        }

        [HarmonyPostfix, HarmonyPatch(typeof(GuideObjectManager), nameof(GuideObjectManager.SetDeselectObject))]
        static void SetDeselectObjectPostfix(GuideObjectManager __instance, GuideObject _object)
        {
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

            GuideObjectPicker.UpdateAllColors();

            GuideObjectPicker.UpdateEditButtonsWindow();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UpdateCurve))]
        static void UpdateKeyframeWindowPostfix()
        {
            _graphEditor.UpdateCurve();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.OnKeyframeContainerMouseDown))]
        static void OnKeyframeContainerMouseDownPostfix(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle && Input.GetKey(KeyCode.LeftControl) == false && RectTransformUtility.ScreenPointToLocalPointInRectangle(_timeline._keyframesContainer, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                float time = 10f * localPoint.x / (Timeline.Timeline._baseGridWidth * _timeline._zoomLevel);
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    float beat = _timeline._blockLength / _timeline._divisions;
                    float mod = time % beat;
                    if (mod / beat > 0.5f)
                        time += beat - mod;
                    else
                        time -= mod;
                }
                if (Input.GetKey(KeyCode.LeftAlt) && _timeline._selectedInterpolables.Count != 0)
                {
                    return;
                }
                else
                {
                    if (_timeline._selectedInterpolables.Count != 0)
                        _timeline.ClearSelectedInterpolables();
                    InterpolableModel model = null;
                    float distance = float.MaxValue;
                    foreach (Timeline.Timeline.InterpolableDisplay display in _timeline._displayedInterpolables)
                    {
                        if (!display.gameObject.activeSelf)
                            continue;
                        float distance2 = Mathf.Abs(localPoint.y - ((RectTransform)display.gameObject.transform).anchoredPosition.y);
                        if (distance2 < distance)
                        {
                            distance = distance2;
                            model = display.interpolable.obj;
                        }
                    }
                    foreach (Timeline.Timeline.InterpolableModelDisplay display in _timeline._displayedInterpolableModels)
                    {
                        if (!display.gameObject.activeSelf)
                            continue;
                        float distance2 = Mathf.Abs(localPoint.y - ((RectTransform)display.gameObject.transform).anchoredPosition.y);
                        if (distance2 < distance)
                        {
                            distance = distance2;
                            model = display.model;
                        }
                    }
                    if (model != null)
                    {
                        Interpolable interpolable;
                        if (model is Interpolable)
                            interpolable = (Interpolable)model;
                        else
                            interpolable = _timeline.AddInterpolable(model);

                        if (interpolable != null)
                        {
                            if (interpolable.parameter is GuideObject)
                            {
                                if (Singleton<GuideObjectManager>.Instance.selectObjects.Length > 1)
                                {
                                    bool first = false;
                                    foreach (GuideObject guideObject in Singleton<GuideObjectManager>.Instance.selectObjects)
                                    {
                                        if (!first)
                                        {
                                            first = true;
                                            continue;
                                        }

                                        if (guideObject.parentGuide == null || guideObject.parentGuide != interpolable.oci.guideObject) continue;

                                        InterpolableModel newModel = _timeline._interpolableModelsList.Find(i => i.id == interpolable.id);
                                        Interpolable newInterpolable = new Interpolable(interpolable.oci, guideObject, newModel);
                                        if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
                                        {
                                            _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                                            _timeline._interpolablesTree.AddLeaf(newInterpolable);
                                            if (!newInterpolable.keyframes.ContainsKey(time))
                                                _timeline.AddKeyframe(newInterpolable, time);
                                        }
                                    }
                                    _timeline.UpdateInterpolablesView();
                                }
                            }
                        }
                    }
                }
            }


            // Clean if there's any Interpolable with the KKPE Temporary GuideObject.

            if (_timeline._interpolables.Count > 0)
            {
                bool update = false;
                List<Interpolable> toDelete = new List<Interpolable>();
                foreach (var pair in _timeline._interpolables)
                {
                    Interpolable interpolable = pair.Value;

                    if (interpolable.parameter is GuideObject gO)
                    {
                      if (gO.dicKey == kkpeGuideObjectDictKey)
                        {
                            update = true;
                            toDelete.Add(interpolable);
                        }
                    }
                }

                foreach (Interpolable interpolable in toDelete) 
                {
                    if (_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                        _timeline._interpolables.Remove(interpolable.GetHashCode());

                    _timeline._interpolablesTree.RemoveLeaf(interpolable);

                    int index = _timeline._selectedInterpolables.IndexOf(interpolable);
                    if (index != -1)
                        _timeline._selectedInterpolables.RemoveAt(index);
                    _timeline._selectedKeyframes.RemoveAll(elem => elem.Value.parent == interpolable);
                }

                if (update)
                    _timeline.UpdateInterpolablesView();
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.PasteKeyframes))]
        static void PasteKeyframesPrefix(out List<KeyValuePair<float, Keyframe>> __state)
        {
            bool showAll = _timeline._allToggle.isOn;

            __state = new List<KeyValuePair<float, Keyframe>>(_timeline._copiedKeyframes);

            if (!showAll)
            {
                if (_timeline._copiedKeyframes.Any(k => k.Value.parent.oci == firstObject))
                {
                    List<KeyValuePair<float, Keyframe>> keys = _timeline._copiedKeyframes.Where(k => k.Value.parent.oci == firstObject || k.Value.parent.oci == null).ToList();
                    _timeline._copiedKeyframes.Clear();
                    foreach (KeyValuePair<float, Keyframe> key in keys)
                        _timeline._copiedKeyframes.Add(key);
                }
                else
                {
                    List<KeyValuePair<float, Keyframe>> nullKeys = _timeline._copiedKeyframes.Where(k => k.Value.parent.oci == null).ToList();
                    List<KeyValuePair<float, Keyframe>> keys = _timeline._copiedKeyframes.Where(k => k.Value.parent.oci != null).ToList();

                    List<KeyValuePair<float, Keyframe>> toSelect = new List<KeyValuePair<float, Keyframe>>();
                    float time = _timeline._playbackTime % _timeline._duration;
                    if (time == 0f && _timeline._playbackTime == _timeline._duration)
                        time = _timeline._duration;
                    float startOffset = _timeline._copiedKeyframes.Min(k => k.Key);

                    foreach (KeyValuePair<float, Keyframe> key in keys)
                    {
                        Interpolable interpolable = key.Value.parent;

                        Interpolable newInterpolable = new Interpolable(firstObject, interpolable);

                        if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
                        {
                            _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                            _timeline._interpolablesTree.AddLeaf(newInterpolable);
                        }

                        float finalTime = time + key.Key - startOffset;
                        Keyframe newKeyframe = new Keyframe(key.Value.value, newInterpolable, key.Value.curve);
                        newInterpolable.keyframes.Add(finalTime, newKeyframe);
                    }

                    _timeline.UpdateInterpolablesView();

                    _timeline._copiedKeyframes.Clear();
                    foreach (KeyValuePair<float, Keyframe> key in nullKeys)
                        _timeline._copiedKeyframes.Add(key);
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.PasteKeyframes))]
        static void PasteKeyframesPostfix(List<KeyValuePair<float, Keyframe>> __state)
        {
            _timeline._copiedKeyframes.Clear();
            foreach (KeyValuePair<float, Keyframe> key in __state)
                _timeline._copiedKeyframes.Add(key);
        }

        /* UNDO/REDO Selection: Generates a lot of commands, annoying but can be useful sometimes      
         * 
        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SelectKeyframes), new Type[] { typeof(IEnumerable<KeyValuePair<float, Keyframe>>) })]
        static void SelectKeyframesPrefix(IEnumerable<KeyValuePair<float, Keyframe>> keyframes)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.SelectAddKeyframesCommand(new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes), keyframes.ToList()));
        }
        */

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SelectKeyframes), new Type[] { typeof(IEnumerable<KeyValuePair<float, Keyframe>>) })]
        static void SelectKeyframesPostfix(IEnumerable<KeyValuePair<float, Keyframe>> keyframes)
        {
            if (_timeline._selectedKeyframes.Count > 0)
                UI.ResetKeyframeValue(_timeline._selectedKeyframes[0].Value.value);
        }

        /* UNDO/REDO Selection: Generates a lot of commands, annoying but can be useful sometimes     
         * 
        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SelectAddKeyframes), new Type[] { typeof(IEnumerable<KeyValuePair<float, Keyframe>>) })]
        static void SelectAddKeyframes(IEnumerable<KeyValuePair<float, Keyframe>> keyframes)
        {
            if (undoRedoTimeline.Value && _timeline._selectedKeyframes.Count > 0)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.SelectAddKeyframesCommand(new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes), keyframes.ToList()));
        }
        */

        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.MoveKeyframe))]
        static void MoveKeyframePrefix(Keyframe keyframe, float destinationTime, out float __state)
        {
            __state = keyframe.parent.keyframes.Keys[keyframe.parent.keyframes.IndexOfValue(keyframe)];
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.MoveKeyframe))]
        static void MoveKeyframePostfix(Keyframe keyframe, float destinationTime, float __state)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.MoveKeyframeCommand(keyframe, destinationTime, __state));
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.AddKeyframe))]
        static void AddKeyframePrefix(Timeline.Interpolable interpolable, float time, out int __state)
        {
            __state = interpolable.keyframes.Count;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.AddKeyframe))]
        static void AddKeyframePostfix(Timeline.Interpolable interpolable, float time, int __state)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddKeyframeCommand(interpolable, time, __state));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.DeleteKeyframes), new Type[] { typeof(IEnumerable<KeyValuePair<float, Keyframe>>), typeof(bool) })]
        static void DeleteKeyframesPrefix(IEnumerable<KeyValuePair<float, Keyframe>> keyframes, bool removeInterpolables, out List<KeyValuePair<float, Keyframe>> __state)
        {
            __state = new List<KeyValuePair<float, Keyframe>>(keyframes);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.DeleteKeyframes), new Type[] { typeof(IEnumerable<KeyValuePair<float, Keyframe>>), typeof(bool) })]
        static void DeleteKeyframesPostfix(IEnumerable<KeyValuePair<float, Keyframe>> keyframes, bool removeInterpolables, List<KeyValuePair<float, Keyframe>> __state)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.DeleteKeyframeCommand(keyframes, removeInterpolables, __state));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.RemoveInterpolables), new Type[] { typeof(IEnumerable<Timeline.Interpolable>) })]
        static void RemoveInterpolablesPostfix(IEnumerable<Timeline.Interpolable> interpolables)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.RemoveInterpolablesCommand(interpolables));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UseCurrentValue))]
        static void UseCurrentValuePrefix(out List<object> __state)
        {
            __state = new List<object>();

            foreach (KeyValuePair<float, Keyframe> pair in _timeline._selectedKeyframes)
                __state.Add(pair.Value.value);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.UseCurrentValue))]
        static void UseCurrentValuePostfix(List<object> __state)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.UseCurrentValueCommand(__state, new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes)));
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SaveKeyframeTime))]
        static void SaveKeyframeTimePrefix(float time, out List<float> __state)
        {
            List<float> oldTime = new List<float>();

            foreach (KeyValuePair<float, Keyframe> pair in _timeline._selectedKeyframes)
            {
                oldTime.Add(pair.Key);
            }

            __state = oldTime;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SaveKeyframeTime))]
        static void SaveKeyframeTimePostfix(float time, List<float> __state)
        {
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.SaveKeyframeTimeCommand(time, __state, new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes)));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.DragAtCurrentTime))]
        static void DragAtCurrentTimePrefix(out List<float> __state)
        {
            List<float> oldTime = new List<float>();

            foreach (KeyValuePair<float, Keyframe> pair in _timeline._selectedKeyframes)
            {
                oldTime.Add(pair.Key);
            }

            __state = oldTime;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.DragAtCurrentTime))]
        static void DragAtCurrentTimePostfix(List<float> __state)
        {
            List<float> newTime = new List<float>();

            foreach (KeyValuePair<float, Keyframe> pair in _timeline._selectedKeyframes)
            {
                newTime.Add(pair.Key);
            }
            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.DragAtCurrentTimeCommand(newTime, __state, new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes)));
        }
        
        /*/ OLD ALT SELECT LINKED GUIDEOBJECT        
         
        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.SelectAddInterpolable))]
        static void SelectAddInterpolablePostfix(Timeline.Timeline __instance, params Interpolable[] interpolables)
        {
            if (linkedGameObjectTimeline.Value)
            {
                //if (_timeline._selectedInterpolables.Count == 0 || !Input.GetKey(KeyCode.LeftAlt)) return;

                _self.ExecuteDelayed2(() =>
                {
                    foreach (TreeNodeObject _node in Singleton<TreeNodeCtrl>.Instance.hashSelectNode)
                        _node.OnDeselect();

                    Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Clear();

                    foreach (GuideObject go in new HashSet<GuideObject>(Singleton<GuideObjectManager>.Instance.hashSelectObject))
                        Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);

                    Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;
                    foreach (Interpolable interpolable in __instance._selectedInterpolables)
                    {
                        if (interpolable == null) continue;

                        GuideObject linkedGuideObject = interpolable.parameter as GuideObject;

                        if (linkedGuideObject != null)
                        {
                            bool hasParent = linkedGuideObject.parentGuide != null;
                            TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? linkedGuideObject : linkedGuideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                            if (node == null) return;

                            if (hasParent)
                            {
                                ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(node);
                                if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count != 1 || !Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                    Singleton<TreeNodeCtrl>.Instance.SelectSingle(node, false);

                                Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                Singleton<GuideObjectManager>.Instance.selectObject = linkedGuideObject;
                                linkedGuideObject.isActive = true;
                                _self.ExecuteDelayed2(() =>
                                {
                                    linkedGuideObject.SetLayer(linkedGuideObject.gameObject, LayerMask.NameToLayer("Studio/Select"));
                                });

                            }
                            else
                            {
                                if (!linkedGuideObject.isActive)
                                {

                                    Singleton<TreeNodeCtrl>.Instance.SetSelectNode(node);
                                    if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                    {
                                        Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                        linkedGuideObject.SetActive(true);
                                    }
                                }
                            }

                            /*
                            bool hasParent = linkedGuideObject.parentGuide != null;

                            TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? linkedGuideObject : linkedGuideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                            if (node == null) continue;

                            // Select Parent GuideObject if any
                            if (hasParent)
                            {
                                if (!Singleton<GuideObjectManager>.Instance.hashSelectObject.Contains(linkedGuideObject))
                                    Singleton<GuideObjectManager>.Instance.AddSelectMultiple(linkedGuideObject);
                            }

                            if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Add(node))
                            {
                                if (Singleton<TreeNodeCtrl>.Instance.onSelect != null && Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count == 1)
                                {
                                    Singleton<TreeNodeCtrl>.Instance.onSelect(node);
                                }
                                else if (Singleton<TreeNodeCtrl>.Instance.onSelectMultiple != null && Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count > 1)
                                {
                                    Singleton<TreeNodeCtrl>.Instance.onSelectMultiple();
                                }
                                node.colorSelect = ((Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count == 1) ? ConvertColor(91, 164, 82) : ConvertColor(94, 139, 100));

                                ObjectCtrlInfo objectCtrlInfo;
                                Singleton<GuideObjectManager>.Instance.AddSelectMultiple(Singleton<Studio.Studio>.Instance.dicInfo.TryGetValue(node, out objectCtrlInfo) ? objectCtrlInfo.guideObject : null);
                            }

                        }
                        else
                        {
                            if (interpolable.oci != null)
                                linkedGuideObject = interpolable.oci.guideObject;

                            if (linkedGuideObject != null)
                                GuideObjectManager.Instance.selectObject = linkedGuideObject;
                        }

                    }
                }, 10);
            }

        }*/

        [HarmonyPostfix, HarmonyPatch(typeof(Timeline.Timeline), nameof(Timeline.Timeline.GetInterpolableDisplay))]
        static void GetInterpolableDisplayPostfix(int i)
        {
            if (i < _timeline._displayedInterpolables.Count)
            {
                Timeline.Timeline.InterpolableDisplay display = _timeline._displayedInterpolables[i];

                if (displayNamesTimeline.Value)
                {
                    if (display.container.gameObject.GetComponent<PointerEnterHandler>() == null)
                    {
                        PointerEnterHandler pointerEnter = display.container.gameObject.AddComponent<PointerEnterHandler>();
                        pointerEnter.onPointerEnter = (e) =>
                        {
                            _timeline._tooltip.transform.parent.gameObject.SetActive(true);
                            _timeline._tooltip.text = display.name.text;
                        };
                        pointerEnter.onPointerExit = (e) => { _timeline._tooltip.transform.parent.gameObject.SetActive(false); };
                    }
                }

                var pointer = display.container.gameObject.GetComponent<PointerDownHandler>();
                if (pointer == null) return;

                pointer.onPointerDown -= SelectLinkedGuideObject;
                pointer.onPointerDown += SelectLinkedGuideObject;
            }

        }

        #endregion

    }
}