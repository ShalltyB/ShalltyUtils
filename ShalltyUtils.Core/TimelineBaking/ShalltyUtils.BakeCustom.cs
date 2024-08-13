extern alias aliasTimeline;
using aliasTimeline::Timeline;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Keyframe = Timeline.Keyframe;
using KKAPI.Utilities;
using ToolBox.Extensions;
using static ShalltyUtils.ShalltyUtils;
using HSPE;
using HSPE.AMModules;

namespace ShalltyUtils.TimelineBaking
{
    public class BakeCustom
    {
        public static bool showCustomBakeUI = false;
        public static Rect customBakeRect = new Rect(300f, 300f, 150f, 150f);

        private static List<CustomBakeGuideObject> customBakeGODic = new List<CustomBakeGuideObject>();
        private static List<CustomBakeKKPEBone> customBakeKKPEDic = new List<CustomBakeKKPEBone>();

        private static Vector2 customBakeGoDicScroll;
        private static Vector2 customBakeKKPEDicScroll;

        public static bool customBakingUseRealtime = false;
        public static bool customBakingToFile = false;

        public static bool isBakingCustom = false;

        public static float customBakingSeconds = 10f;
        public static int customBakingPrewarmLoops = 0;
        public static int customBakingFPS = 10;
        public static int customBakingCountdown = 5;
        public static int customBakingRealCountdown = 0;

        public class CustomBakeGuideObject
        {
            public GuideObject guideObject;
            public string interpolableID;
            public string name;

            public CustomBakeGuideObject(GuideObject guideObject, string interpolableID, string name)
            {
                this.guideObject = guideObject;
                this.interpolableID = interpolableID;
                this.name = name;
            }

        }

        public class CustomBakeKKPEBone
        {
            public object parameter;
            public ObjectCtrlInfo oci;
            public Transform bone;
            public BonesEditor bonesEditor;
            public string interpolableID;

            public CustomBakeKKPEBone(object parameter, Transform bone, BonesEditor bonesEditor, string interpolableID, ObjectCtrlInfo oci)
            {
                this.parameter = parameter;
                this.bone = bone;
                this.interpolableID = interpolableID;
                this.bonesEditor = bonesEditor;
                this.oci = oci;
            }
        }


        public static void Window(int WindowID)
        {
            GUI.enabled = true;
            GUI.color = defColor;

            if (GUI.Button(new Rect(customBakeRect.width - 18, 0, 18, 18), "X")) showCustomBakeUI = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();


            GUILayout.BeginVertical(GUI.skin.box);

            if (selectedObjects != null && selectedObjects.Count() > 0)
            {
                string selectedItem = "(Nothing)";

                if (selectedObjects.Count() > 1)
                {
                    selectedItem = $"All Selected ({selectedObjects.Count()})";
                }
                else if (selectedObjects.Count() == 1)
                {
                    selectedItem = selectedObjects.FirstOrDefault().treeNodeObject.textName;
                }
                
                GUILayout.Label("GuideObject: " + selectedItem);

                GUILayout.Label("Add to list: ");
                if (GUILayout.Button("Location."))
                {
                    AddSelectedGOCustomBake("guideObjectPos");
                }
                if (GUILayout.Button("Rotation."))
                {
                    AddSelectedGOCustomBake("guideObjectRot");
                }
                if (GUILayout.Button("Scale."))
                {
                    AddSelectedGOCustomBake("guideObjectScale");
                }
            }

            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);

            if (firstKKPE != null && firstKKPE._bonesEditor?._boneTarget != null)
            {
                GUILayout.Label("KKPE Bone: " + firstKKPE._bonesEditor._boneTarget.name);

                GUILayout.Label("Add to list: ");
                if (GUILayout.Button("Location."))
                {
                    AddSelectedKKPECustomBake("bonePos");
                }
                if (GUILayout.Button("Rotation."))
                {
                    AddSelectedKKPECustomBake("boneRot");
                }
                if (GUILayout.Button("Scale."))
                {
                    AddSelectedKKPECustomBake("boneScale");
                }
            }

            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("GuideObjects list: ");

            customBakeGoDicScroll = GUILayout.BeginScrollView(customBakeGoDicScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box, GUILayout.Height(200));

            foreach (var item in new List<CustomBakeGuideObject>(customBakeGODic))
            {
                GuideObject guideObject = item.guideObject;
                if (guideObject == null) continue;

                GUI.color = defColor;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(guideObject.transformTarget.name + " - " + item.interpolableID))
                {
                    bool hasParent = guideObject.parentGuide != null;
                    Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                    TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? guideObject : guideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                    if (node == null) return;

                    if (hasParent)
                    {
                        ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(node);
                        if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count != 1 || !Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                            Singleton<TreeNodeCtrl>.Instance.SelectSingle(node, false);

                        Singleton<GuideObjectManager>.Instance.StopSelectObject();
                        Singleton<GuideObjectManager>.Instance.selectObject = guideObject;
                        guideObject.isActive = true;
                        _self.ExecuteDelayed2(() =>
                        {
                            guideObject.SetLayer(guideObject.gameObject, LayerMask.NameToLayer("Studio/Select"));
                        });
                    }
                    else
                    {
                        if (!guideObject.isActive)
                        {
                            Singleton<TreeNodeCtrl>.Instance.SetSelectNode(node);
                            if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                            {
                                Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                guideObject.SetActive(true);
                            }
                        }
                    }
                }

                GUI.color = Color.red;

                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                {
                    customBakeGODic.Remove(item);
                }

                GUI.color = defColor;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("KKPE Bones list: ");

            customBakeKKPEDicScroll = GUILayout.BeginScrollView(customBakeKKPEDicScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box, GUILayout.Height(200));

            foreach (var item in new List<CustomBakeKKPEBone>(customBakeKKPEDic))
            {
                if (item.parameter == null || item.bone == null) continue;

                GUI.color = defColor;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(item.bone.name + " - " + item.interpolableID))
                {
                    if (item.bonesEditor != null)
                        item.bonesEditor._boneTarget = item.bone;
                }

                GUI.color = Color.red;

                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                {
                    customBakeKKPEDic.Remove(item);
                }

                GUI.color = defColor;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            //
            GUILayout.BeginVertical(GUI.skin.box);

            customBakingUseRealtime = GUILayout.Toggle(customBakingUseRealtime, "Bake at Real Time?");
            customBakingToFile = GUILayout.Toggle(customBakingToFile, "Export to file?");

            IMGUIExtensions.FloatValue("Duration", customBakingSeconds, "0.00", (value) => { customBakingSeconds = value; });
            IMGUIExtensions.IntValue("FPS", customBakingFPS, "0", (value) => { customBakingFPS = value; });
            IMGUIExtensions.IntValue("Prewarm Loops", customBakingPrewarmLoops, "0", (value) => { customBakingPrewarmLoops = value; });
            IMGUIExtensions.IntValue("Countdown", customBakingCountdown, "0", (value) => { customBakingCountdown = value; });

            GUI.color = isBakingCustom ? Color.red : Color.green;

            string buttonName = isBakingCustom ? "Stop Recording" : "Start Recording";

            if (customBakingRealCountdown > 0)
                buttonName = "Recording start in... " + customBakingRealCountdown;

            if (GUILayout.Button(buttonName))
            {
                Bake();
            }
            GUI.color = defColor;


            GUILayout.EndVertical();


            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            customBakeRect = IMGUIUtils.DragResizeEatWindow(_uniqueId + 30, customBakeRect);
        }

        private static void AddSelectedGOCustomBake(string interpolableID)
        {
            var ocis = Singleton<Studio.Studio>.Instance.dicInfo;

            InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.id == interpolableID);

            foreach (GuideObject guideObject in Singleton<GuideObjectManager>.Instance.selectObjects)
            {
                if (guideObject == null || guideObject.dicKey == kkpeGuideObjectDictKey) continue;

                bool hasParent = guideObject.parentGuide != null;
                ObjectCtrlInfo oci = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? guideObject : guideObject.parentGuide)).Select(pair => pair.Value).FirstOrDefault();
                if (oci == null) continue;

                customBakeGODic.Add(new CustomBakeGuideObject(guideObject, interpolableID, guideObject.transformTarget.name));
            }
        }

        private static void AddSelectedKKPECustomBake(string interpolableID)
        {
            if (selectedObjects == null || selectedObjects.Count() == 0) return;

            var parameterOci = selectedObjects.FirstOrDefault();
            if (parameterOci == null) return;

            PoseController component = parameterOci.guideObject.transformTarget.GetComponent<PoseController>();

            var parameter = BonesEditor.TimelineCompatibility.GetParameter(parameterOci);

            customBakeKKPEDic.Add(new CustomBakeKKPEBone(parameter, component._bonesEditor._boneTarget, component._bonesEditor, interpolableID, parameterOci));
        }



        public static void Bake()
        {
            if (isBakingCustom)
            {
                isBakingCustom = false;
                return;
            }

            if (customBakeKKPEDic.Count == 0 && customBakeGODic.Count == 0)
            {
                ShalltyUtils.Logger.LogMessage("First add bones to the list!");
                return;
            }

            float duration = customBakingSeconds;
            int frameRate = customBakingFPS;
            int prewarmLoops = customBakingPrewarmLoops;

            List<Interpolable> interpolableList = new List<Interpolable>();
            List<CustomBakeGuideObject> customBakeGuideObjects = new List<CustomBakeGuideObject>(customBakeGODic);
            Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

            foreach (var item in customBakeGuideObjects)
            {
                GuideObject guideObject = item.guideObject;
                if (guideObject == null || guideObject.dicKey == kkpeGuideObjectDictKey) continue;

                InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.id == item.interpolableID);


                bool hasParent = guideObject.parentGuide != null;
                ObjectCtrlInfo oci = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? guideObject : guideObject.parentGuide)).Select(pair => pair.Value).FirstOrDefault();
                if (oci == null) continue;

                Interpolable newInterpolable = new Interpolable(oci, guideObject, model);


                if (!customBakingToFile)
                {
                    string name = oci.treeNodeObject.textName;
                    string type = "Bone";

                    switch (item.interpolableID)
                    {
                        case "guideObjectPos": type = "POS"; break;
                        case "guideObjectRot": type = "ROT"; break;
                        case "guideObjectScale": type = "SCALE"; break;
                    }

                    newInterpolable.alias = type + " | " + name;

                    if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
                    {
                        _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                        _timeline._interpolablesTree.AddLeaf(newInterpolable);
                    }
                    else
                        newInterpolable = _timeline._interpolables[newInterpolable.GetHashCode()];
                }

                interpolableList.Add(newInterpolable);
            }


            List<CustomBakeKKPEBone> customBakeKKPEBones = new List<CustomBakeKKPEBone>(customBakeKKPEDic);

            foreach (var item in customBakeKKPEBones)
            {
                var parameter = item.parameter;

                InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.id == item.interpolableID);

                Interpolable newInterpolable = new Interpolable(item.oci, parameter, model);

                if (!customBakingToFile)
                {
                    if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
                    {
                        _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                        _timeline._interpolablesTree.AddLeaf(newInterpolable);
                    }
                    else
                        newInterpolable = _timeline._interpolables[newInterpolable.GetHashCode()];
                }

                interpolableList.Add(newInterpolable);
            }

            if (interpolableList.Count == 0) return;

            _timeline.UpdateInterpolablesView();

            isBakingCustom = true;
            _self.StartCoroutine(BakeCoroutine(interpolableList, frameRate, duration, prewarmLoops));
        }

        private static IEnumerator BakeCoroutine(List<Interpolable> interpolableList, int frameRate, float duration, int prewarmLoops)
        {
            if (customBakingCountdown > 0)
            {
                customBakingRealCountdown += customBakingCountdown;

                while (customBakingRealCountdown > 0)
                {
                    yield return new WaitForSecondsRealtime(1);
                    customBakingRealCountdown--;
                }
            }

            if (TimelineCompatibility.GetIsPlaying())
                TimelineCompatibility.Play();

            float startTime = _timeline._playbackTime;
            float currentTime = startTime;
            float fps = 1f / frameRate;

            if (_timeline._duration < startTime + duration) _timeline._duration = startTime + duration + 1f;

            TimelineCompatibility.Play();

            if (prewarmLoops > 0)
            {
                if (TimelineCompatibility.GetIsPlaying() == false)
                    TimelineCompatibility.Play();

                int j = 0;
                float lastTime = 0;
                while (true)
                {
                    float timelineDuration = TimelineCompatibility.GetDuration();
                    float currentLoopTime = TimelineCompatibility.GetPlaybackTime() % timelineDuration;
                    if (currentLoopTime < lastTime)
                        j++;
                    lastTime = currentLoopTime;
                    if (j == prewarmLoops)
                        break;
                    yield return new WaitForEndOfFrame();
                    customBakingRealCountdown = j;
                }

                customBakingRealCountdown = 0;
            }

            List<KeyValuePair<float, Keyframe>> newKeyframes = new List<KeyValuePair<float, Keyframe>>();

            while (currentTime <= startTime + duration)
            {
                if (_timeline._duration < currentTime + fps) _timeline._duration = currentTime + fps;

                foreach (Interpolable interpolable in interpolableList)
                {
                    try
                    {
                        interpolable.enabled = customBakingToFile;
                        newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, new Keyframe(interpolable.GetValue(), interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f))));
                    }
                    catch (Exception e)
                    {
                        ShalltyUtils.Logger.LogError($"Error while baking!: {e}");
                    }
                }

                if (!customBakingUseRealtime)
                {
                    NextFrame(frameRate);
                    currentTime = _timeline._playbackTime;
                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    currentTime = _timeline._playbackTime;
                    //if (startTime == 0f && Mathf.Approximately(currentTime, 0f))

                    yield return new WaitForSeconds(fps);
                }

                if (!isBakingCustom) break;
            }


            foreach (var pair in newKeyframes)
            {
                if (!pair.Value.parent.keyframes.ContainsKey(pair.Key))
                    pair.Value.parent.keyframes.Add(pair.Key, pair.Value);
            }


            if (!customBakingToFile)
            {
                if (undoRedoTimeline.Value)
                    Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddMultipleKeyframeCommand(new List<KeyValuePair<float, Keyframe>>(newKeyframes)));

                _timeline.UpdateGrid();

                foreach (Interpolable interpolable in interpolableList)
                    interpolable.enabled = true;
            }

            TimelineCompatibility.Play();
            isBakingCustom = false;

            if (customBakingToFile)
            {
                SaveInterpolablesToFile(interpolableList);
            }
        }

    }
}
