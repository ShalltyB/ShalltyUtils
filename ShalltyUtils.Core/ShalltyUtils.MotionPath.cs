using Studio;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vectrosity;
using static ShalltyUtils.ShalltyUtils;
using Keyframe = Timeline.Keyframe;

namespace ShalltyUtils
{
    public class MotionPath
    {
        public static bool showMotionPath = false;

        public static int motionPathMode = 1;
        public static int rangeLeft = 1;
        public static int rangeRight = 1;

        private static VectorLine motionPathLine;
        private static VectorLine motionPathKeys;

        private static bool keyframeHover = false;
        private static List<KeyValuePair<float, Keyframe>> motionPathPoints = new List<KeyValuePair<float, Keyframe>>();

        public static void Init()
        {
            motionPathLine = new VectorLine("MotionPathLine", new List<Vector3> { Vector3.zero, Vector3.zero }, 4f, LineType.Continuous, Joins.Fill);
            motionPathLine.color = Color.red;
            motionPathLine.active = false;

            motionPathKeys = new VectorLine("MotionPathPoints", new List<Vector3> { Vector3.zero }, 12f, LineType.Points);
            motionPathKeys.color = Color.yellow;
            motionPathKeys.texture = keyframeTexture;
            motionPathKeys.active = false;
        }

        public static void UpdateMotionPath()
        {
            ClearPathLines();
            if (!showMotionPath) return;


            /// MOVE TIMELINE IN TIME WITH CTRL SCROLL

            if (Input.mouseScrollDelta.y != 0 && Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.mouseScrollDelta.y > 0)
                {
                    Timeline.Timeline.NextFrame();
                }
                else if (Input.mouseScrollDelta.y < 0)
                {
                    Timeline.Timeline.PreviousFrame();
                }
            }

            motionPathPoints.Clear();

            ///// GET MOTION PATH POINTS:

            /// SELECTED KEYFRAMES
            if (motionPathMode == 0)
            {
                if (_timeline._selectedKeyframes.Count == 0) return;
                Timeline.Interpolable parent = _timeline._selectedKeyframes.FirstOrDefault().Value?.parent;
                if (parent == null || parent.id != "guideObjectPos") return;

                List<KeyValuePair<float, Keyframe>> orderedKeyframes = _timeline._selectedKeyframes.OrderBy(kvp => kvp.Key).ToList();
                if (!(orderedKeyframes.All(pair => pair.Value.parent == parent)) || !IsSequence(orderedKeyframes, parent.keyframes)) return;

                motionPathPoints = orderedKeyframes;
            }
            /// SELECTED INTERPOLABLES
            else if (motionPathMode == 1)
            {
                if (_timeline._selectedInterpolables.Count == 0) return;
                Timeline.Interpolable interpolable = _timeline._selectedInterpolables.FirstOrDefault();
                if (interpolable == null || interpolable.id != "guideObjectPos") return;

                List<KeyValuePair<float, Keyframe>> keyframesList = interpolable.keyframes.OrderBy(kvp => kvp.Key).ToList();
                motionPathPoints = keyframesList;
            }
            /// SELECTED INTERPOLABLES (RANGE)
            else if (motionPathMode == 2)
            {
                if (_timeline._selectedInterpolables.Count == 0) return;
                Timeline.Interpolable interpolable = _timeline._selectedInterpolables.FirstOrDefault();
                if (interpolable == null || interpolable.id != "guideObjectPos") return;

                List<KeyValuePair<float, Keyframe>> keyframesList = interpolable.keyframes.ToList();

                keyframesList.Sort((a, b) => a.Key.CompareTo(b.Key));

                int currentIndex = keyframesList.FindIndex(kv => kv.Key >= _timeline._playbackTime);
                if (currentIndex == -1) return;
                if (currentIndex - 1 > 0 && currentIndex - 1 < keyframesList.Count) currentIndex--;

                List<KeyValuePair<float, Keyframe>> totalRangeKeyframes = new List<KeyValuePair<float, Keyframe>>();

                for (int i = currentIndex - rangeLeft; i <= currentIndex + rangeRight; i++)
                {
                    if (i >= 0 && i < keyframesList.Count)
                    {
                        totalRangeKeyframes.Add(keyframesList[i]);
                    }
                }

                motionPathPoints = totalRangeKeyframes;
            }
            

            /// GET THE POINTS AND DRAW THE LINE
            if (motionPathPoints.Count > 0)
            {
                List<KeyValuePair<float, Keyframe>> orderedKeyframes = motionPathPoints.OrderBy(kvp => kvp.Key).ToList();
                List<KeyValuePair<Vector3, Transform>> allPoints = orderedKeyframes.Select(pair => new KeyValuePair<Vector3, Transform>((Vector3)pair.Value.value, ((GuideObject)pair.Value.parent.parameter).transformTarget)).ToList();
                if (allPoints.Count == 0) return;
               
                List<Vector3> transformedPositions = new List<Vector3>();

                foreach (var pair in allPoints)
                {
                    if (pair.Value == null) continue;

                    if (pair.Value.parent != null)
                    {
                        Vector3 worldPosition = pair.Value.parent.TransformPoint(pair.Key);
                        transformedPositions.Add(worldPosition);
                    }
                    else
                    {
                        Vector3 worldPosition = pair.Key;
                        transformedPositions.Add(worldPosition);
                    }
                }

                motionPathLine.points3 = transformedPositions;
                motionPathKeys.points3 = transformedPositions;

                if (transformedPositions.Count < 2)
                    motionPathLine.active = false;
                else
                {
                    motionPathLine.drawDepth = 0;
                    motionPathLine.active = true;
                    motionPathLine.Draw();
                }
                motionPathLine.drawDepth = 1;
                motionPathKeys.active = true;
                motionPathKeys.Draw();

                /// FUNCTION WHEN CLICKING MOTION PATH KEYFRAMES
                int index = 0;
                if (motionPathKeys.Selected(Input.mousePosition, out index))
                {
                    keyframeHover = true;

                    _timeline._tooltip.transform.parent.gameObject.SetActive(true);
                    float time = motionPathPoints[index].Key;
                    _timeline._tooltip.text = $"Keyframe [{index}]\nT: {Mathf.FloorToInt(time / 60):00}:{time % 60:00.##}\nV: {motionPathPoints[index].Value.value}";

                    if (Input.GetMouseButtonDown(1))
                        _timeline.SeekPlaybackTime(time);
                }
                else
                {
                    if (keyframeHover)
                    {
                        _timeline._tooltip.transform.parent.gameObject.SetActive(false);
                        keyframeHover = false;
                    }
                }
            }
            else
            {
                if (motionPathLine != null)
                {
                    motionPathLine.points3.Clear();
                    motionPathLine.active = false;
                }
                if (motionPathKeys != null)
                {
                    motionPathKeys.points3.Clear();
                    motionPathKeys.active = false;
                }
            }
        }

        private static bool IsSequence(List<KeyValuePair<float, Keyframe>> selectedKeyframes, SortedList<float, Keyframe> sortedKeyframes)
        {
            if (selectedKeyframes.Count <= 1)
                return true;

            int prevIndex = -1;
            foreach (var pickedItem in selectedKeyframes)
            {
                int index = sortedKeyframes.IndexOfKey(pickedItem.Key);
                if (index == -1)
                    return false;

                if (prevIndex != -1 && index != prevIndex + 1)
                    return false;

                prevIndex = index;
            }

            return true;
        }

        private static void ClearPathLines()
        {
            if (motionPathLine != null)
            {
                motionPathLine.points3.Clear();
                motionPathLine.active = false;
            }
            if (motionPathKeys != null)
            {
                motionPathKeys.points3.Clear();
                motionPathKeys.active = false;
            }
        }
    }
}