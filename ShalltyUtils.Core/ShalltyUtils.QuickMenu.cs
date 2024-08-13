extern alias aliasTimeline;
using aliasTimeline::Timeline;
using KKAPI.Utilities;
using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToolBox.Extensions;
using UnityEngine;
using HSPE;
using HSPE.AMModules;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;

namespace ShalltyUtils
{
    public class QuickMenu
    {
        public static bool showQuickMenuUI = false;
        public static Rect quickMenuRect = new Rect(150f, 150f, 150f, 150f);

        public static void Window(int WindowID)
        {
            GUI.enabled = true;
            GUI.color = defColor;

            if (GUI.Button(new Rect(quickMenuRect.width - 18, 0, 18, 18), "X")) showQuickMenuUI = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();



            if (selectedObjects != null && selectedObjects.Count() > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;

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

                GUILayout.Label("Insert Keyframe: ");

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;

                if (GUILayout.Button("All Transform"))
                {
                    InsertKeyframeToSelectedGO("guideObjectPos");
                    InsertKeyframeToSelectedGO("guideObjectRot");
                    InsertKeyframeToSelectedGO("guideObjectScale");
                }

                if (GUILayout.Button("Position"))
                {
                    InsertKeyframeToSelectedGO("guideObjectPos");
                }

                GUILayout.BeginHorizontal();

                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedGO("guideObjectXPos");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedGO("guideObjectYPos");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedGO("guideObjectZPos");
                }

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;
                if (GUILayout.Button("Rotation"))
                {
                    InsertKeyframeToSelectedGO("guideObjectRot");
                }

                GUILayout.BeginHorizontal();

                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedGO("guideObjectXRot");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedGO("guideObjectYRot");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedGO("guideObjectZRot");
                }

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();


                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;
                if (GUILayout.Button("Scale"))
                {
                    InsertKeyframeToSelectedGO("guideObjectScale");
                }

                GUILayout.BeginHorizontal();

                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedGO("guideObjectXScale");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedGO("guideObjectYScale");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedGO("guideObjectZScale");
                }
                GUI.color = defColor;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();


                GUILayout.EndVertical();
            }





            if (firstKKPE != null && firstKKPE._bonesEditor?._boneTarget != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;

                GUILayout.Label("KKPE Bone: " + firstKKPE._bonesEditor._boneTarget.name);

                GUILayout.Label("Insert Keyframe: ");

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;

                if (GUILayout.Button("All Transform"))
                {
                    InsertKeyframeToSelectedGO("bonePos");
                    InsertKeyframeToSelectedGO("boneRot");
                    InsertKeyframeToSelectedGO("boneScale");
                }

                if (GUILayout.Button("Position"))
                {
                    InsertKeyframeToSelectedKKPEBone("bonePos");
                }

                GUILayout.BeginHorizontal();

                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneXPos");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneYPos");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneZPos");
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;
                if (GUILayout.Button("Rotation"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneRot");
                }

                GUILayout.BeginHorizontal();

                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneXRot");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneYRot");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneZRot");
                }
                GUILayout.EndHorizontal();


                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.color = defColor;
                if (GUILayout.Button("Scale"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneScale");
                }

                GUILayout.BeginHorizontal();
                GUI.color = Color.red;
                if (GUILayout.Button("X"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneXScale");
                }
                GUI.color = Color.green;
                if (GUILayout.Button("Y"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneYScale");
                }
                GUI.color = Color.blue;
                if (GUILayout.Button("Z"))
                {
                    InsertKeyframeToSelectedKKPEBone("boneZScale");
                }
                GUI.color = defColor;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUILayout.EndVertical();
            }



            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            quickMenuRect = IMGUIUtils.DragResizeEatWindow(_uniqueId + 20, quickMenuRect);
        }

        private static void InsertKeyframeToSelectedGO(string interpolableID)
        {
            var ocis = Singleton<Studio.Studio>.Instance.dicInfo;

            InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.id == interpolableID);

            var selectedGO = Singleton<GuideObjectManager>.Instance.selectObjects;

            if (model == null || selectedGO.IsNullOrEmpty()) return;

            foreach (GuideObject guideObject in selectedGO)
            {
                if (guideObject == null || guideObject.dicKey == kkpeGuideObjectDictKey) continue;

                bool hasParent = guideObject.parentGuide != null;
                ObjectCtrlInfo oci = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? guideObject : guideObject.parentGuide)).Select(pair => pair.Value).FirstOrDefault();
                if (oci == null) continue;
                Interpolable newInterpolable = new Interpolable(oci, guideObject, model);

                float time = _timeline._playbackTime;

                if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
                {
                    _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                    _timeline._interpolablesTree.AddLeaf(newInterpolable);
                }
                else
                    newInterpolable = _timeline._interpolables[newInterpolable.GetHashCode()];

                if (!newInterpolable.keyframes.ContainsKey(time))
                {
                    _self.ExecuteDelayed2(() => _timeline.AddKeyframe(newInterpolable, time), 2);
                }
                else
                {
                    _timeline.DeleteKeyframes(new List<KeyValuePair<float, Keyframe>> { new KeyValuePair<float, Keyframe>(time, newInterpolable.keyframes[time]) }, false);
                    _timeline.AddKeyframe(newInterpolable, time);
                }
            }

            _timeline.UpdateInterpolablesView();
        }

        private static void InsertKeyframeToSelectedKKPEBone(string interpolableID)
        {
            if (selectedObjects == null || selectedObjects.Count() == 0) return;

            var parameterOci = selectedObjects.FirstOrDefault();
            if (parameterOci == null) return;

            var parameter = BonesEditor.TimelineCompatibility.GetParameter(parameterOci);

            InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.id == interpolableID);

            Interpolable newInterpolable = new Interpolable(parameterOci, parameter, model);

            float time = _timeline._playbackTime;

            if (!_timeline._interpolables.ContainsKey(newInterpolable.GetHashCode()))
            {
                _timeline._interpolables.Add(newInterpolable.GetHashCode(), newInterpolable);
                _timeline._interpolablesTree.AddLeaf(newInterpolable);
            }
            else
                newInterpolable = _timeline._interpolables[newInterpolable.GetHashCode()];


            if (!newInterpolable.keyframes.ContainsKey(time))
            {
                _self.ExecuteDelayed2(() => _timeline.AddKeyframe(newInterpolable, time), 2);
            }
            else
            {
                _timeline.DeleteKeyframes(new List<KeyValuePair<float, Keyframe>> { new KeyValuePair<float, Keyframe>(time, newInterpolable.keyframes[time]) }, false);
                _timeline.AddKeyframe(newInterpolable, time);
            }

            _timeline.UpdateInterpolablesView();
        }
    }
}
