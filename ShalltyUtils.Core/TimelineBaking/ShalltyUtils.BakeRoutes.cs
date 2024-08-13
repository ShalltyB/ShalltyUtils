extern alias aliasTimeline;
using aliasTimeline::Timeline;
using KKAPI.Utilities;
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
    public class BakeRoutes
    {
        public static int routeBakingFPS = 30;
        public static bool isBakingRoute = false;

        public static void Bake()
        {
            if (isBakingRoute)
            {
                isBakingRoute = false;
                return;
            }

            if (firstObject == null || !(firstObject is OCIRoute))
            {
                ShalltyUtils.Logger.LogMessage("First select a Route in the Workspace!");
                return;
            }

            OCIRoute route = (OCIRoute)firstObject;
            if (((OIRouteInfo)route.objectInfo).loop)
            {
                ShalltyUtils.Logger.LogMessage("Turn OFF the Loop of the Route!");
                return;
            }

            int frameRate = routeBakingFPS;

            InterpolableModel modelPos = _timeline._interpolableModelsList.Find(i => i.owner == "Timeline" && i.id == "guideObjectPos");

            List<Interpolable> interpolableList = new List<Interpolable>();
            List<ObjectCtrlInfo> allOCIs = new List<ObjectCtrlInfo>();

            foreach (TreeNodeObject node in route.childNodeRoot.child)
            {
                ObjectCtrlInfo oci;
                if (Singleton<Studio.Studio>.Instance.dicInfo.TryGetValue(node, out oci))
                    allOCIs.Add(oci);

            }

            foreach (ObjectCtrlInfo oci in allOCIs)
            {
                Interpolable interpolablePos = new Interpolable(oci, oci.guideObject, modelPos);

                string name = oci.treeNodeObject.textName;

                interpolablePos.alias = "POS | " + name;

                if (!_timeline._interpolables.ContainsKey(interpolablePos.GetHashCode()))
                {
                    _timeline._interpolables.Add(interpolablePos.GetHashCode(), interpolablePos);
                    _timeline._interpolablesTree.AddLeaf(interpolablePos);

                    interpolableList.Add(interpolablePos);
                }
                else
                    interpolableList.Add(_timeline._interpolables[interpolablePos.GetHashCode()]);
            }

            if (interpolableList.Count == 0) return;
            _timeline.UpdateInterpolablesView();

            isBakingRoute = true;
            _self.StartCoroutine(BakeCoroutine(interpolableList, route));
        }

        private static IEnumerator BakeCoroutine(List<Interpolable> interpolableList, OCIRoute route)
        {
            if (TimelineCompatibility.GetIsPlaying())
                TimelineCompatibility.Play();

            float startTime = _timeline._playbackTime;
            float currentTime = startTime;
            float fps = 1f / routeBakingFPS;

            route.Stop();
            route.Play();
            TimelineCompatibility.Play();

            List<KeyValuePair<float, Keyframe>> newKeyframes = new List<KeyValuePair<float, Keyframe>>();

            while (((OIRouteInfo)route.objectInfo).active == true)
            {
                if (_timeline._duration < currentTime + fps) _timeline._duration = currentTime + fps;

                foreach (Interpolable interpolable in interpolableList)
                {
                    try
                    {
                        interpolable.enabled = false;

                        Vector3 pos = ((GuideObject)interpolable.parameter).transformTarget.position;
                        Keyframe keyframe = new Keyframe(pos, interpolable, AnimationCurve.Linear(0f, 0f, 1f, 1f));

                        newKeyframes.Add(new KeyValuePair<float, Keyframe>(currentTime, keyframe));
                    }
                    catch (Exception e)
                    {
                        ShalltyUtils.Logger.LogError($"Error while baking route: {e}");
                    }
                }


                currentTime = _timeline._playbackTime;

                if (!isBakingRoute) break;
                if (((OIRouteInfo)route.objectInfo).loop) break;

                yield return new WaitForSeconds(fps);
            }



            foreach (var pair in newKeyframes)
            {
                if (!pair.Value.parent.keyframes.ContainsKey(pair.Key))
                    pair.Value.parent.keyframes.Add(pair.Key, pair.Value);
            }

            if (undoRedoTimeline.Value)
                Singleton<UndoRedoManager>.Instance.Do(new UndoRedoCommands.AddMultipleKeyframeCommand(new List<KeyValuePair<float, Keyframe>>(newKeyframes)));

            _timeline.UpdateGrid();
            route.Stop();
            TimelineCompatibility.Play();

            foreach (Interpolable interpolable in interpolableList)
                interpolable.enabled = true;

            isBakingRoute = false;
            UI.routeBakeButton.GetComponentInChildren<Text>().text = isBakingRoute ? "Stop Baking" : "Bake Route";
        }

    }

}
