extern alias aliasTimeline;

using KKAPI.Utilities;
using System.Collections.Generic;
using UnityEngine;
using Keyframe = Timeline.Keyframe;
using Timeline;
using static ShalltyUtils.ShalltyUtils;
using ToolBox.Extensions;
using System.Reflection.Emit;
using System.Linq;
using Vectrosity;
using static aliasTimeline::Timeline.Timeline;
using System.IO;
using System.Xml;
using ExtensibleSaveFormat;
using Studio;

namespace ShalltyUtils
{
    public class KeyframesGroups
    {
        public static bool showKeyframeGroupsUI = false;
        public static Rect windowKeyframeGroupsRect = new Rect(100, 100, 220, 700);

        public static string keyframesGroupsSearch = "";
        public static Vector2 keyframesGroupsScroll = Vector2.zero;

        public static List<Group> allKeyframeGroups = new List<Group>();
        public static Group selectedKeyframeGroup;

        public class Group
        {
            public string name = "Keyframe Group";
            public Color color = Color.blue;
            public List<Keyframe> keyframes = new List<Keyframe>();

            public List<VectorLine> performanceModeLines = new List<VectorLine>();

            public Group(string name, Color color, List<Keyframe> keyframes)
            {
                this.name = name;
                this.color = color;
                this.keyframes = keyframes;
            }

            public Group(Group group)
            {
                this.name = group.name;
                this.color = group.color;
                this.keyframes = new List<Keyframe>(group.keyframes);
            }

            public void CleanRemovedKeyframes()
            {
                if (keyframes == null || keyframes.Count == 0) return;
                keyframes.RemoveAll(keyframe => keyframe == null || keyframe.parent == null || keyframe.parent.keyframes.Count == 0 || keyframe.parent.keyframes.ContainsValue(keyframe) == false);
            }

            public void AddSelectedKeyframes()
            {
                if (_timeline._selectedKeyframes.Count == 0) return;
                List<Keyframe> _selectedKeyframes = _timeline._selectedKeyframes.Select(x => x.Value).ToList();
                foreach (Keyframe keyframe in _selectedKeyframes)
                {
                    if (allKeyframeGroups.Any(x => x.keyframes.Contains(keyframe)) == false)
                    {
                        selectedKeyframeGroup.keyframes.Add(keyframe);
                    }
                    else
                    {
                        allKeyframeGroups.Where(x => x.keyframes.Contains(keyframe)).First().keyframes.Remove(keyframe);
                        selectedKeyframeGroup.keyframes.Add(keyframe);
                    }
                }
            }
        }

        public static void UpdateGroupKeyframesLines()
        {
            foreach (var group in allKeyframeGroups)
            {
                group.CleanRemovedKeyframes();

                List<Vector2> allPoints = new List<Vector2>();
                foreach (var keyframe in group.keyframes)
                {
                    if (_timeline.ShouldShowInterpolable(keyframe.parent, _timeline._allToggle.isOn) == false) continue;

                    float time = keyframe.parent.keyframes.First(k => k.Value == keyframe).Key;
                    InterpolableDisplay interpolableDisplay = _timeline._displayedInterpolables.Where(dsp => dsp.interpolable.obj == keyframe.parent).FirstOrDefault();
                    if (interpolableDisplay == null) return;

                    allPoints.Add(new Vector2(_baseGridWidth * _timeline._zoomLevel * time / 10f, ((RectTransform)interpolableDisplay.gameObject.transform).anchoredPosition.y));
                }

                int linesCount = Mathf.CeilToInt((allPoints.Count) / 8192f);

                foreach (VectorLine line in group.performanceModeLines)
                    line.points2.Clear();

                if (linesCount > 0)
                {
                    List<List<Vector2>> lines = PerformanceMode.SplitPointList(allPoints, linesCount);

                    /// CREATE NEW LINES
                    if (group.performanceModeLines.Count < linesCount)
                    {
                        int linesToGo = linesCount - group.performanceModeLines.Count;

                        for (int i = 0; i < linesToGo; i++)
                        {
                            string name = group.name;
                            Color color = group.color;
                            VectorLine newLine = new VectorLine($"{name} ({group.performanceModeLines.Count + 1})", new List<Vector2> { Vector2.zero, Vector2.one }, keyframesSize.Value, LineType.Points, Joins.None);
                            newLine.active = true;
                            newLine.SetCanvas(_timeline._horizontalScrollView.content.gameObject);
                            newLine.SetMask(_timeline._keyframesContainer.parent.gameObject);
                            newLine.color = color;
                            newLine.texture = keyframeTexture;
                            group.performanceModeLines.Add(newLine);
                        }
                    }

                    /// GENERATE LINES POINTS
                    for (int i = 0; i < lines.Count; i++)
                    {
                        VectorLine newLine = group.performanceModeLines[i];
                        newLine.points2 = lines[i];
                    }
                }

                /// REMOVE EMPTY LINES

                List<VectorLine> linesToDelete = new List<VectorLine>();
                foreach (VectorLine line in group.performanceModeLines)
                    if (line.points2.Count == 0)
                        linesToDelete.Add(line);

                foreach (VectorLine line in linesToDelete)
                    group.performanceModeLines.Remove(line);

                VectorLine.Destroy(linesToDelete);
            }
        }

        public static void UpdateGroupKeyframes()
        {
            foreach (var group in KeyframesGroups.allKeyframeGroups)
            {
                foreach (KeyframeDisplay display in _timeline._displayedKeyframes)
                {
                    if (group.keyframes.Contains(display.keyframe))
                        display.image.color = _timeline._selectedKeyframes.Any(k => k.Value == display.keyframe) ? Color.green : group.color;
                }
            }
        }

        public static void Window(int WindowID)
        {
            GUI.enabled = true;
            GUI.color = defColor;

            if (GUI.Button(new Rect(windowKeyframeGroupsRect.width - 18, 0, 18, 18), "X")) showKeyframeGroupsUI = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUI.skin.box);


            if (GUILayout.Button("Add Keyframe Group"))
            {
                selectedKeyframeGroup = new Group("Keyframe Group", new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)), new List<Keyframe>());
                allKeyframeGroups.Add(selectedKeyframeGroup);
                selectedKeyframeGroup.AddSelectedKeyframes();
            }

            GUILayout.Space(15f);

            GUILayout.BeginVertical(GUI.skin.box);
            GUI.enabled = selectedKeyframeGroup != null;

            GUILayout.Label("Selected Keyframe Group: ");
            if (selectedKeyframeGroup != null)
                selectedKeyframeGroup.name = GUILayout.TextField(selectedKeyframeGroup.name);
            else
                GUILayout.TextField(" (Nothing selected) ");

            Color groupColor = Color.white;
            if (selectedKeyframeGroup != null)
                groupColor = selectedKeyframeGroup.color;

            IMGUIExtensions.ColorValue("Color: ", groupColor, (x) =>
            {
                if (selectedKeyframeGroup.performanceModeLines.IsNullOrEmpty() == false)
                {
                    VectorLine.Destroy(selectedKeyframeGroup.performanceModeLines);
                    selectedKeyframeGroup.performanceModeLines = new List<VectorLine>();
                }

                selectedKeyframeGroup.color = x;
                UpdateGroupKeyframesLines();

            }, false, false, false, 20f);

            GUILayout.Space(15f);

            GUI.color = Color.red;
            if (GUILayout.Button("Delete Group"))
            {
                if (selectedKeyframeGroup.performanceModeLines.IsNullOrEmpty() == false)
                {
                    VectorLine.Destroy(selectedKeyframeGroup.performanceModeLines);
                    selectedKeyframeGroup.performanceModeLines = new List<VectorLine>();
                }

                allKeyframeGroups.Remove(selectedKeyframeGroup);
                selectedKeyframeGroup = null;
            }
            GUI.color = defColor;
                
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Search:", GUILayout.ExpandWidth(false));
                keyframesGroupsSearch = GUILayout.TextField(keyframesGroupsSearch, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                    keyframesGroupsSearch = "";
            }
            GUILayout.EndHorizontal();

            keyframesGroupsScroll = GUILayout.BeginScrollView(keyframesGroupsScroll, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

            foreach (Group group in allKeyframeGroups)
            {
                if (group.name.ToLower().Contains(keyframesGroupsSearch.ToLower()) == false) continue;

                GUI.color = group == selectedKeyframeGroup ? Color.cyan : defColor;
                
                GUILayout.BeginVertical(GUI.skin.box);

                IMGUIExtensions.DrawColorButton(group.name + " (" + group.keyframes.Count + ")", group.color, 40f, () =>
                {
                    selectedKeyframeGroup = group;
                    group.CleanRemovedKeyframes();
                });

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Select"))
                {
                    group.CleanRemovedKeyframes();
                    List<KeyValuePair<float, Keyframe>> keyframes = new List<KeyValuePair<float, Keyframe>>();
                    foreach (var keyframe in group.keyframes)
                    {
                        if (keyframe.parent.keyframes.ContainsValue(keyframe))
                        {
                            float time = keyframe.parent.keyframes.First(x => x.Value == keyframe).Key;
                            keyframes.Add(new KeyValuePair<float, Keyframe>(time, keyframe));
                        }
                    }

                    _timeline.SelectKeyframes(keyframes);
                }
                if (GUILayout.Button("Add Select"))
                {
                    group.CleanRemovedKeyframes();
                    List<KeyValuePair<float, Keyframe>> keyframes = new List<KeyValuePair<float, Keyframe>>();

                    foreach (var keyframe in group.keyframes)
                    {
                        if (keyframe.parent.keyframes.ContainsValue(keyframe))
                        {
                            float time = keyframe.parent.keyframes.First(x => x.Value == keyframe).Key;
                            keyframes.Add(new KeyValuePair<float, Keyframe>(time, keyframe));
                        }
                    }

                    try
                    {

                        if (_timeline._selectedKeyframes.Count > 0)
                            _timeline.SelectAddKeyframes(keyframes);
                        else
                            _timeline.SelectKeyframes(keyframes);
                    }
                    catch
                    {
                        // ignored
                    }
                }
                if (GUILayout.Button("Deselect"))
                {
                    group.CleanRemovedKeyframes();
                    _timeline._selectedKeyframes.RemoveAll(x => group.keyframes.Contains(x.Value));
                    _timeline.UpdateKeyframeSelection();
                }

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Add Keyframes"))
                {
                    group.AddSelectedKeyframes();
                }
                if (GUILayout.Button("Remove Keyframes"))
                {
                    List<Keyframe> _selectedKeyframes = _timeline._selectedKeyframes.Select(x => x.Value).ToList();
                    group.keyframes.RemoveAll(x => _selectedKeyframes.Contains(x));
                }

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUI.color = defColor;
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            windowKeyframeGroupsRect = IMGUIUtils.DragResizeEatWindow(_uniqueId + 20, windowKeyframeGroupsRect);
        }

        #region SAVE/LOAD

        public static void ClearSceneData()
        {
            foreach (var group in allKeyframeGroups)
            {
                if (group.performanceModeLines.IsNullOrEmpty() == false)
                    VectorLine.Destroy(group.performanceModeLines);
            }
            allKeyframeGroups.Clear();
            selectedKeyframeGroup = null;
        }

        public static string SaveSceneData()
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(stringWriter))
                {
                    writer.WriteStartElement("root");

                    foreach (Group group in allKeyframeGroups)
                    {
                        writer.WriteStartElement("group");

                        writer.WriteAttributeString("name", group.name);
                        writer.WriteValue("color", group.color);

                        List<KeyValuePair<int, Interpolable>> interpolables = _timeline._interpolables.ToList();

                        foreach (Keyframe keyframe in group.keyframes)
                        {
                            int interpolableIndex = interpolables.FindIndex(x => x.Value == keyframe.parent);
                            float keyframeKey = keyframe.parent.keyframes.First(x => x.Value == keyframe).Key;

                            writer.WriteStartElement("keyframe");
                            writer.WriteValue("interpolableIndex", interpolableIndex);
                            writer.WriteValue("keyframeKey", keyframeKey);
                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        public static void LoadSceneData(PluginData pluginData)
        {
            if (pluginData == null) return;

            ClearSceneData();

            string data = (string)pluginData.data["keyframesGroupsData"];
            if (data.IsNullOrEmpty()) return;

            _self.ExecuteDelayed2(() =>
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(data);

                XmlNode node = doc.FirstChild;
                if (node == null) return;

                List<KeyValuePair<int, Interpolable>> interpolables = _timeline._interpolables.ToList();

                foreach (XmlNode childNode in node.ChildNodes)
                {
                    if (childNode.Name == "group")
                    {
                        string name = childNode.Attributes["name"].Value;
                        Color color = childNode.ReadColor("color");

                        List<Keyframe> keyframes = new List<Keyframe>();
                        foreach (XmlNode keyframe in childNode.ChildNodes)
                        {
                            if (keyframe.Name == "keyframe")
                            {
                                int interpolableIndex = keyframe.ReadInt("interpolableIndex");

                                float keyframeKey = keyframe.ReadFloat("keyframeKey");

                                if (interpolableIndex >= 0 && interpolableIndex < interpolables.Count)
                                {
                                    Interpolable interpolable = interpolables[interpolableIndex].Value;
                                    if (interpolable.keyframes.ContainsKey(keyframeKey))
                                        keyframes.Add(interpolable.keyframes[keyframeKey]);
                                }
                            }
                        }

                        Group group = new Group(name, color, keyframes);
                        allKeyframeGroups.Add(group);
                        group.CleanRemovedKeyframes();
                    }
                }

            }, 25);
        }

        #endregion


    }
}
