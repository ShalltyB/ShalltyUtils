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
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;

namespace ShalltyUtils.TimelineBaking
{
    public class BakeAnimation
    {
        public static float animBakingSeconds = -1f;
        public static float animBakingSpeed = 1f;
        public static int animBakingFPS = 10;
        public static int animBakingLoops = 1;

        public static bool isBakingBones = false;

        public static void Bake()
        {
            if (isBakingBones)
            {
                isBakingBones = false;
                return;
            }

            if (firstChar == null)
            {
                ShalltyUtils.Logger.LogMessage("First select a Character!");
                return;
            }
            List<Interpolable> interpolableList = new List<Interpolable>();

            InterpolableModel modelPos = _timeline._interpolableModelsList.Find(i => i.owner == "Timeline" && i.id == "guideObjectPos");
            InterpolableModel modelRot = _timeline._interpolableModelsList.Find(i => i.owner == "Timeline" && i.id == "guideObjectRot");

            foreach (OCIChar.IKInfo ik in firstChar.listIKTarget)
            {
                if (ShouldBakeBones(ik.guideObject.transformTarget.name) == false) continue;

                if (ik.guideObject.enablePos)
                {
                    Interpolable interpolablePos = new Interpolable(firstChar, ik.guideObject, modelPos);
                    interpolablePos.alias = "POS | " + FancyBoneName(ik.guideObject.transformTarget.name);

                    if (!_timeline._interpolables.ContainsKey(interpolablePos.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolablePos.GetHashCode(), interpolablePos);
                        _timeline._interpolablesTree.AddLeaf(interpolablePos);
                        interpolableList.Add(interpolablePos);
                    }
                    else
                        interpolableList.Add(_timeline._interpolables[interpolablePos.GetHashCode()]);
                }
                if (ik.guideObject.enableRot)
                {
                    Interpolable interpolableRot = new Interpolable(firstChar, ik.guideObject, modelRot);
                    interpolableRot.alias = "ROT | " + FancyBoneName(ik.guideObject.transformTarget.name);

                    if (!_timeline._interpolables.ContainsKey(interpolableRot.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolableRot.GetHashCode(), interpolableRot);
                        interpolableList.Add(interpolableRot);
                        _timeline._interpolablesTree.AddLeaf(interpolableRot);
                    }
                    else
                        interpolableList.Add(_timeline._interpolables[interpolableRot.GetHashCode()]);
                }
            }


            List<OCIChar.BoneInfo> boneList = firstChar.listBones;

            List<string> transformNames = new List<string>();

            List<string> leftFingerBones = new List<string> {
                        "cf_j_thumb01_L",
                        "cf_j_thumb02_L",
                        "cf_j_thumb03_L",

                        "cf_j_index01_L",
                        "cf_j_index02_L",
                        "cf_j_index03_L",

                        "cf_j_middle01_L",
                        "cf_j_middle02_L",
                        "cf_j_middle03_L",

                        "cf_j_ring01_L",
                        "cf_j_ring02_L",
                        "cf_j_ring03_L",

                        "cf_j_little01_L",
                        "cf_j_little02_L",
                        "cf_j_little03_L"
                    };

            List<string> rightFingerBones = new List<string> {
                        "cf_j_thumb01_R",
                        "cf_j_thumb02_R",
                        "cf_j_thumb03_R",

                        "cf_j_index01_R",
                        "cf_j_index02_R",
                        "cf_j_index03_R",

                        "cf_j_middle01_R",
                        "cf_j_middle02_R",
                        "cf_j_middle03_R",

                        "cf_j_ring01_R",
                        "cf_j_ring02_R",
                        "cf_j_ring03_R",

                        "cf_j_little01_R",
                        "cf_j_little02_R",
                        "cf_j_little03_R"
                    };

            if (UI.animBakeToggleHead.isOn)
            {
                transformNames.Add("cf_j_head");
                transformNames.Add("cf_j_neck");
            }

            if (UI.animBakeToggleLeftFingers.isOn)
                transformNames.AddRange(leftFingerBones);

            if (UI.animBakeToggleRightFingers.isOn)
                transformNames.AddRange(rightFingerBones);

            List<GuideObject> guideObjectFK = boneList.Where(targetInfo => transformNames.Contains(targetInfo.guideObject.transformTarget.name)).Select(targetInfo => targetInfo.guideObject).ToList();

            foreach (GuideObject guideObject in guideObjectFK)
            {
                Interpolable interpolableRot = new Interpolable(firstChar, guideObject, modelRot);

                string name = guideObject.transformTarget.name;

                interpolableRot.alias = "ROT | " + FancyBoneName(name);

                if (!_timeline._interpolables.ContainsKey(interpolableRot.GetHashCode()))
                {
                    _timeline._interpolables.Add(interpolableRot.GetHashCode(), interpolableRot);
                    interpolableList.Add(interpolableRot);
                    _timeline._interpolablesTree.AddLeaf(interpolableRot);
                }
                else
                    interpolableList.Add(_timeline._interpolables[interpolableRot.GetHashCode()]);
            }

            if (interpolableList.Count == 0) return;

            _timeline.UpdateInterpolablesView();

            isBakingBones = true;
            _self.StartCoroutine(BakeCoroutine(interpolableList));
        }

        private static IEnumerator BakeCoroutine(List<Interpolable> interpolableList)
        {
            int loopCount = animBakingLoops;
            int frameRate = animBakingFPS;
            float speed = animBakingSpeed;
            float mmddSeconds = animBakingSeconds;

            if (speed < 0 || frameRate < 1 || loopCount < 1 || interpolableList.Count < 1) yield break;

            firstChar.animeSpeed = speed;

            yield return null;

            Animator animator = firstChar.charAnimeCtrl.animator;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            float animEnd = info.length;

            bool animActive = Studio.Studio.Instance.manipulatePanelCtrl.active;

            List<KeyValuePair<float, Keyframe>> newKeyframes = new List<KeyValuePair<float, Keyframe>>();

            if (mmddSeconds == -1)
            {

                for (int loop = 0; loop < loopCount; loop++)
                {
                    float startTime = _timeline._playbackTime;
                    float finalTime = startTime + (animEnd * (loop + 1));

                    if (_timeline._duration < finalTime) _timeline._duration = finalTime;

                    firstChar.animeSpeed = 0f;
                    animator.Play(info.shortNameHash, -1, 0f);

                    Timeline.Timeline.Pause();

                    yield return null;


                    float currentTime = startTime;
                    ManipulatePanelCtrl manipulatePanelCtrl = Studio.Studio.Instance.manipulatePanelCtrl;
                    MPCharCtrl mpCharCtrl = manipulatePanelCtrl.charaPanelInfo.mpCharCtrl;

                    while (currentTime <= startTime + animEnd)
                    {
                        // GET CURRENT POSITION
                        mpCharCtrl.fkInfo.buttonAnime.onClick.Invoke();
                        mpCharCtrl.ikInfo.buttonAnime.onClick.Invoke();
                        if (!manipulatePanelCtrl.active) manipulatePanelCtrl.active = true;

                        yield return null;

                        foreach (Interpolable interpolable in interpolableList)
                        {
                            newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, new Keyframe(interpolable.GetValue(), interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f))));
                            //_timeline.AddKeyframe(interpolable, currentTime);
                        }


                        NextFrame(frameRate);
                        currentTime = _timeline._playbackTime;
                        animator.Play(info.shortNameHash, -1, Mathf.InverseLerp(startTime, startTime + animEnd, currentTime));

                        if (!isBakingBones) break;
                    }

                    if (!isBakingBones) break;
                }
            }
            else
            {
                animEnd = mmddSeconds;
                float startTime = _timeline._playbackTime;
                if (_timeline._duration < startTime + animEnd) _timeline._duration = startTime + animEnd;

                firstChar.animeSpeed = 0f;
                animator.Play(info.shortNameHash, -1, 0f);

                Timeline.Timeline.Pause();

                yield return null;


                float currentTime = startTime;
                ManipulatePanelCtrl manipulatePanelCtrl = Studio.Studio.Instance.manipulatePanelCtrl;
                MPCharCtrl mpCharCtrl = manipulatePanelCtrl.charaPanelInfo.mpCharCtrl;

                while (currentTime <= startTime + animEnd)
                {
                    // GET CURRENT POSITION
                    mpCharCtrl.fkInfo.buttonAnime.onClick.Invoke();
                    mpCharCtrl.ikInfo.buttonAnime.onClick.Invoke();
                    if (!manipulatePanelCtrl.active) manipulatePanelCtrl.active = true;

                    yield return null;

                    foreach (Interpolable interpolable in interpolableList)
                    {
                        newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, new Keyframe(interpolable.GetValue(), interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f))));
                    }


                    NextFrame(frameRate);
                    currentTime = _timeline._playbackTime;
                    //animator.Play(info.shortNameHash, -1, Mathf.InverseLerp(startTime, startTime + animEnd, currentTime));

                    if (!isBakingBones) break;
                }
            }

            foreach (var pair in newKeyframes)
            {
                if (!pair.Value.parent.keyframes.ContainsKey(pair.Key))
                    pair.Value.parent.keyframes.Add(pair.Key, pair.Value);
            }

            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddMultipleKeyframeCommand(new List<KeyValuePair<float, Keyframe>>(newKeyframes)));

            _timeline.UpdateGrid();

            Studio.Studio.Instance.manipulatePanelCtrl.active = animActive;
            isBakingBones = false;
            UI.animBakeButton.GetComponentInChildren<Text>().text = isBakingBones ? "Stop Baking" : "Bake Animation";
        }

        private static bool ShouldBakeBones(string name)
        {
            switch (name)
            {
                case "cf_t_hips(work)":
                    return UI.animBakeToggleHips.isOn;

                case "cf_t_shoulder_L(work)":
                case "cf_t_elbo_L(work)":
                case "cf_t_hand_L(work)":
                    return UI.animBakeToggleLeftArm.isOn;

                case "cf_t_shoulder_R(work)":
                case "cf_t_elbo_R(work)":
                case "cf_t_hand_R(work)":
                    return UI.animBakeToggleRightArm.isOn;

                case "cf_t_waist_L(work)":
                case "cf_t_knee_L(work)":
                case "cf_t_leg_L(work)":
                    return UI.animBakeToggleLeftLeg.isOn;

                case "cf_t_waist_R(work)":
                case "cf_t_knee_R(work)":
                case "cf_t_leg_R(work)":
                    return UI.animBakeToggleRightLeg.isOn;

                default:
                    return false;
            }
        }

    }
}
