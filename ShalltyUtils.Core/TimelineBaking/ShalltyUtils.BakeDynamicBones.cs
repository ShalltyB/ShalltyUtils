extern alias aliasTimeline;
using aliasTimeline::Timeline;
using KKAPI.Utilities;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToolBox.Extensions;
using UnityEngine;
using UnityEngine.UI;
using HSPE;
using HSPE.AMModules;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;
#if HS2
using AIChara;
#endif

namespace ShalltyUtils.TimelineBaking
{
    public class BakeDynamicBones
    {
        public static bool isBakingDB = false;

        public static float dbBakingSeconds = 10f;
        public static float dbBakingWait = 0f;
        public static int dbBakingFPS = 30;
        public static bool dbBakingRealTime = false;
        public static bool dbBakingPos = false;

        public static void Bake()
        {
            if (isBakingDB)
            {
                isBakingDB = false;
                return;
            }

            PoseController kkpe = null;

            bool isChara = false;

            if (firstChar != null)
            {
                ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                kkpe = chaCtrl.GetComponent<PoseController>();
                isChara = true;
            }
            else if (firstItem != null)
            {
                kkpe = firstItem.objectItem.GetComponent<PoseController>();
            }

            if (kkpe == null)
            {
                ShalltyUtils.Logger.LogMessage("First select an Item with DynamicBones in the Workspace!");
                return;
            }


            if (!isChara)
            {
                if (kkpe._dynamicBonesEditor._dynamicBones.Count == 0)
                {
                    ShalltyUtils.Logger.LogMessage("The selected Item doesn't have DynamicBones!");
                    return;
                }
            }
            else
            {
                CharaPoseController charaPE = kkpe as CharaPoseController;

                if (charaPE._dynamicBonesEditor._dynamicBones.Count == 0 && charaPE._boobsEditor._dynamicBones.Length == 0)
                {
                    ShalltyUtils.Logger.LogMessage("The selected Character doesn't have DynamicBones!");
                    return;
                }
            }


            float AnimEnd = dbBakingSeconds;
            int frameRate = dbBakingFPS;
            float startTime = _timeline._playbackTime;

            if (_timeline._duration < startTime + AnimEnd) _timeline._duration = startTime + AnimEnd;

            InterpolableModel modelRot = _timeline._interpolableModelsList.Find(i => i.owner == "Timeline" && i.id == "guideObjectRot");
            InterpolableModel modelPos = _timeline._interpolableModelsList.Find(i => i.owner == "KKPE" && i.id == "bonePos");

            List<Interpolable> interpolableList = new List<Interpolable>();
            List<GuideObject> guideObjects = new List<GuideObject>();

            foreach (DynamicBone bone in kkpe._dynamicBonesEditor._dynamicBones)
            {
                if (bone.enabled == false) continue;

                foreach (object o in (IList)bone.GetPrivate("m_Particles"))
                {
                    Transform t = (Transform)o.GetPrivate("m_Transform");
                    OCIChar.BoneInfo boneInfo;
                    if (t != null && kkpe._dynamicBonesEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                    {
                        guideObjects.Add(boneInfo.guideObject);
                    }
                }
            }

            if (isChara && kkpe is CharaPoseController chape)
            {

                foreach (DynamicBone_Ver02 bone in chape._boobsEditor._dynamicBones)
                {
                    if (bone.enabled == false) continue;

                    foreach (Transform t in bone.Bones)
                    {
                        OCIChar.BoneInfo boneInfo;
                        if (t != null && chape._boobsEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                        {
                            guideObjects.Add(boneInfo.guideObject);
                        }
                    }
                }
            }

            foreach (GuideObject guideObject in guideObjects)
            {
                Interpolable interpolableRot = new Interpolable(firstObject, guideObject, modelRot);


                string name = guideObject.transformTarget.name;

                interpolableRot.alias = "ROT | " + FancyBoneName(name);

                if (!_timeline._interpolables.ContainsKey(interpolableRot.GetHashCode()))
                {
                    _timeline._interpolables.Add(interpolableRot.GetHashCode(), interpolableRot);
                    _timeline._interpolablesTree.AddLeaf(interpolableRot);

                    interpolableList.Add(interpolableRot);
                }
                else
                    interpolableList.Add(_timeline._interpolables[interpolableRot.GetHashCode()]);

                if (dbBakingPos)
                {

                    PoseController controller = firstObject.guideObject.transformTarget.GetComponent<PoseController>();
                    controller._bonesEditor._boneTarget = guideObject.transformTarget;

                    Interpolable interpolablePos = new Interpolable(firstObject, BonesEditor.TimelineCompatibility.GetParameter(firstObject), modelPos)
                    {
                        alias = "POS | " + FancyBoneName(name)
                    };

                    if (!_timeline._interpolables.ContainsKey(interpolablePos.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolablePos.GetHashCode(), interpolablePos);
                        _timeline._interpolablesTree.AddLeaf(interpolablePos);

                        interpolableList.Add(interpolablePos);
                    }
                    else
                        interpolableList.Add(_timeline._interpolables[interpolablePos.GetHashCode()]);
                }
            }

            if (interpolableList.Count == 0)
            {
                ShalltyUtils.Logger.LogMessage("At least one DynamicBone has to be enabled in the Advanced KKPE window!");
                return;
            }

            _timeline.UpdateInterpolablesView();

            isBakingDB = true;

            if (dbBakingRealTime)
                _self.StartCoroutine(BakeRealTimeCoroutine(startTime, AnimEnd, interpolableList, kkpe, isChara));
            else
                _self.StartCoroutine(BakeCoroutine(startTime, AnimEnd, interpolableList, kkpe, isChara));
        }

        private static IEnumerator BakeCoroutine(float startTime, float animEnd, List<Interpolable> interpolableList, PoseController kkpe, bool isChara = false)
        {
            float currentTime = startTime;
            int fps = dbBakingFPS;
            float waitSeconds = dbBakingWait;

            if (_timeline._duration < startTime + animEnd) _timeline._duration = startTime + animEnd;

            List<KeyValuePair<Transform, OCIChar.BoneInfo>> dynamicBones = new List<KeyValuePair<Transform, OCIChar.BoneInfo>>();

            foreach (Interpolable interpolable in interpolableList)
                interpolable.enabled = false;

            foreach (DynamicBone bone in kkpe._dynamicBonesEditor._dynamicBones)
            {
                if (bone.enabled == false) continue;

                bone.m_UpdateRate = -1;
                foreach (object o in (IList)bone.GetPrivate("m_Particles"))
                {
                    Transform t = (Transform)o.GetPrivate("m_Transform");
                    OCIChar.BoneInfo boneInfo;
                    if (t != null && kkpe._dynamicBonesEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                    {
                        dynamicBones.Add(new KeyValuePair<Transform, OCIChar.BoneInfo>(t, boneInfo));
                    }
                }
            }

            if (isChara && kkpe is CharaPoseController chape)
            {
                foreach (DynamicBone_Ver02 bone in chape._boobsEditor._dynamicBones)
                {
                    if (bone.enabled == false) continue;

                    //bone.UpdateRate = -1;
                    foreach (Transform t in bone.Bones)
                    {
                        OCIChar.BoneInfo boneInfo;
                        if (t != null && chape._boobsEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                        {
                            dynamicBones.Add(new KeyValuePair<Transform, OCIChar.BoneInfo>(t, boneInfo));
                        }
                    }
                }
            }


            List<KeyValuePair<float, Keyframe>> newKeyframes = new List<KeyValuePair<float, Keyframe>>();

            while (currentTime <= startTime + animEnd)
            {
                yield return new WaitForFixedUpdate();

                if (dynamicBones.Count > 0)
                {
                    foreach (KeyValuePair<Transform, OCIChar.BoneInfo> kvp in dynamicBones)
                    {
                        OCIChar.BoneInfo boneInfo = kvp.Value;
                        Transform t = kvp.Key;
                        boneInfo.guideObject.changeAmount.rot = t.localEulerAngles;
                    }
                }
                else
                    break;

                if (waitSeconds > 0)
                    yield return new WaitForSeconds(waitSeconds);

                yield return new WaitForFixedUpdate();

                foreach (Interpolable interpolable in interpolableList)
                    newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, new Keyframe(interpolable.GetValue(), interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f))));


                NextFrame(fps);
                currentTime = _timeline._playbackTime;

                if (!isBakingDB) break;
            }

            foreach (DynamicBone bone in kkpe._dynamicBonesEditor._dynamicBones)
            {
                if (bone.enabled == false) continue;
                bone.m_UpdateRate = 60;
            }

            if (isChara && kkpe is CharaPoseController chape2)
            {
                foreach (DynamicBone_Ver02 bone in chape2._boobsEditor._dynamicBones)
                {
                    if (bone.enabled == false) continue;
                    bone.UpdateRate = 60;
                }
            }

            foreach (var pair in newKeyframes)
            {
                if (!pair.Value.parent.keyframes.ContainsKey(pair.Key))
                    pair.Value.parent.keyframes.Add(pair.Key, pair.Value);
            }

            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddMultipleKeyframeCommand(new List<KeyValuePair<float, Keyframe>>(newKeyframes)));

            foreach (Interpolable interpolable in interpolableList)
                interpolable.enabled = true;

            _timeline.UpdateGrid();

            isBakingDB = false;
            UI.dbBakeButton.GetComponentInChildren<Text>().text = isBakingDB ? "Stop Baking" : "Bake DynamicBones";
        }

        private static IEnumerator BakeRealTimeCoroutine(float startTime, float animEnd, List<Interpolable> interpolableList, PoseController kkpe, bool isChara = false)
        {
            if (TimelineCompatibility.GetIsPlaying())
                TimelineCompatibility.Play();


            float currentTime = startTime;
            float fps = 1f / dbBakingFPS;
            float waitSeconds = dbBakingWait;

            if (_timeline._duration < startTime + animEnd) _timeline._duration = startTime + animEnd;

            foreach (Interpolable interpolable in interpolableList)
                interpolable.enabled = false;

            List<KeyValuePair<Transform, OCIChar.BoneInfo>> dynamicBones = new List<KeyValuePair<Transform, OCIChar.BoneInfo>>();

            foreach (DynamicBone bone in kkpe._dynamicBonesEditor._dynamicBones)
            {
                if (bone.enabled == false) continue;

                bone.m_UpdateRate = -1;
                foreach (object o in (IList)bone.GetPrivate("m_Particles"))
                {
                    Transform t = (Transform)o.GetPrivate("m_Transform");
                    OCIChar.BoneInfo boneInfo;
                    if (t != null && kkpe._dynamicBonesEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                    {
                        dynamicBones.Add(new KeyValuePair<Transform, OCIChar.BoneInfo>(t, boneInfo));
                    }
                }
            }

            if (isChara && kkpe is CharaPoseController chape)
            {
                foreach (DynamicBone_Ver02 bone in chape._boobsEditor._dynamicBones)
                {
                    if (bone.enabled == false) continue;

                    //bone.UpdateRate = -1;
                    foreach (Transform t in bone.Bones)
                    {
                        OCIChar.BoneInfo boneInfo;
                        if (t != null && chape._boobsEditor._target.fkObjects.TryGetValue(t.gameObject, out boneInfo))
                        {
                            dynamicBones.Add(new KeyValuePair<Transform, OCIChar.BoneInfo>(t, boneInfo));
                        }
                    }
                }
            }


            List<KeyValuePair<float, Keyframe>> newKeyframes = new List<KeyValuePair<float, Keyframe>>();


            TimelineCompatibility.Play();

            while (currentTime <= startTime + animEnd)
            {
                if (!TimelineCompatibility.GetIsPlaying())
                    TimelineCompatibility.Play();

                yield return new WaitForFixedUpdate();

                if (dynamicBones.Count > 0)
                {
                    foreach (KeyValuePair<Transform, OCIChar.BoneInfo> kvp in dynamicBones)
                    {
                        OCIChar.BoneInfo boneInfo = kvp.Value;
                        Transform t = kvp.Key;
                        boneInfo.guideObject.changeAmount.rot = t.localEulerAngles;
                    }
                }
                else
                    break;

                if (waitSeconds > 0)
                    yield return new WaitForSeconds(waitSeconds);

                yield return new WaitForFixedUpdate();

                foreach (Interpolable interpolable in interpolableList)
                    newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, new Keyframe(interpolable.GetValue(), interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f))));


                currentTime = _timeline._playbackTime;

                if (!isBakingDB) break;
                yield return new WaitForSeconds(fps);
            }

            foreach (DynamicBone bone in kkpe._dynamicBonesEditor._dynamicBones)
            {
                if (bone.enabled == false) continue;
                bone.m_UpdateRate = 60;
            }

            if (isChara && kkpe is CharaPoseController chape2)
            {
                foreach (DynamicBone_Ver02 bone in chape2._boobsEditor._dynamicBones)
                {
                    if (bone.enabled == false) continue;
                    bone.UpdateRate = 60;
                }
            }

            foreach (var pair in newKeyframes)
            {
                if (!pair.Value.parent.keyframes.ContainsKey(pair.Key))
                    pair.Value.parent.keyframes.Add(pair.Key, pair.Value);
            }

            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddMultipleKeyframeCommand(new List<KeyValuePair<float, Keyframe>>(newKeyframes)));

            foreach (Interpolable interpolable in interpolableList)
                interpolable.enabled = true;

            _timeline.UpdateGrid();

            isBakingDB = false;
            UI.dbBakeButton.GetComponentInChildren<Text>().text = isBakingDB ? "Stop Baking" : "Bake DynamicBones";
        }


    }
}
