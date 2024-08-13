extern alias aliasTimeline;

#if HS2
using AIChara;
#endif
using aliasTimeline::Timeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static ShalltyUtils.ShalltyUtils;

namespace ShalltyUtils
{
    public class MeshSequencer
    {
        public static bool showMeshSequencerUI = false;
        public static Rect windowMeshSequencerRect = new Rect(600, 300, 300, 150);

        public static string cachePath = "";

        private static List<string> meshList = new List<string>();
        private static string groupName = "New Sequence";

        private static bool nameOrder = true;
        private static bool lengthOrder = false;
        private static bool numOrder = false;
        private static bool hideStart = false;
        private static bool hideEnd = true;

        private static string RenderersSearch = "";
        private static Vector2 scrollPosition;
        private static float spd = 0.01f;
        private static string strSpd = "0.01";
        private static float startTime = 0f;
        private static string strStartTime = "";
        private static string lastSelected;

        public static void Window(int WindowID)
        {
            GUI.color = defColor;
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical();

                GUI.enabled = true;
                if (GUILayout.Button(meshList.Count <= 0 ? "Create Mesh List" : "Refresh Mesh List"))
                {
                    if (firstObject == null)
                    {
                        ShalltyUtils.Logger.LogMessage("First select an Object in the Workspace!");
                        return;
                    }

                    meshList.Clear();

                    SkinnedMeshRenderer[] skinnedMeshRenderers = new SkinnedMeshRenderer[0];
                    MeshFilter[] meshRenderers = new MeshFilter[0];

                    string root = null;

                    if (firstChar != null)
                    {
                        ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                        skinnedMeshRenderers = chaCtrl.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        meshRenderers = chaCtrl.GetComponentsInChildren<MeshFilter>(true);
                        root = chaCtrl.gameObject.name;
                    }
                    else if (firstItem != null)
                    {
                        skinnedMeshRenderers = firstItem.objectItem.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        meshRenderers = firstItem.objectItem.GetComponentsInChildren<MeshFilter>(true);
                        root = firstItem.objectItem.gameObject.name;
                    }

                    for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                    {
                        if (root != null)
                        {
                            string fullPath = KKAPI.Utilities.Extensions.GetFullPath(skinnedMeshRenderers[i].gameObject);
                            if (fullPath.Contains(root))
                            {
                                int index = fullPath.IndexOf(root) + root.Length + 1;
                                if (index != -1)
                                {
                                    string newPath = fullPath.Substring(index);
                                    meshList.Add(newPath);
                                }
                            }
                        }
                    }

                    for (int i = 0; i < meshRenderers.Length; i++)
                    {
                        if (root != null)
                        {
                            string fullPath = KKAPI.Utilities.Extensions.GetFullPath(meshRenderers[i].gameObject);
                            if (fullPath.Contains(root))
                            {
                                int index = fullPath.IndexOf(root) + root.Length + 1;
                                if (index != -1)
                                {
                                    string newPath = fullPath.Substring(index);
                                    meshList.Add(newPath);
                                }
                            }
                        }
                    }
                }


                if (meshList != null)
                {
                    if (meshList.Count > 0)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Search:", GUILayout.ExpandWidth(false));
                            RenderersSearch = GUILayout.TextField(RenderersSearch, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                RenderersSearch = "";
                        }
                        GUILayout.EndHorizontal();

                        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box, GUILayout.Height(250));

                        for (int i = 0; i < meshList.Count; i++)
                        {
                            var mesh = meshList[i];

                            if (mesh.IndexOf(RenderersSearch, StringComparison.CurrentCultureIgnoreCase) != -1)
                            {
                                GUILayout.BeginHorizontal(GUI.skin.box);

                                if (GUILayout.Button("Delete", GUILayout.Width(50)))
                                {
                                    meshList.RemoveAt(i);
                                    return;
                                }

                                if (GUILayout.Button("↑", GUILayout.Width(25)))
                                {
                                    if (i > 0)
                                    {
                                        string temp = meshList[i];
                                        meshList.RemoveAt(i);
                                        meshList.Insert(i - 1, temp);
                                        lastSelected = temp;
                                    }
                                }

                                if (GUILayout.Button("↓", GUILayout.Width(25)))
                                {
                                    if (i < meshList.Count - 1)
                                    {
                                        string temp = meshList[i];
                                        meshList.RemoveAt(i);
                                        meshList.Insert(i + 1, temp);
                                        lastSelected = temp;
                                    }
                                }

                                if (lastSelected == meshList[i]) GUI.color = Color.green;
                                GUILayout.Label("[" + i + "] " + Path.GetFileName(mesh), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
                                GUI.color = defColor;

                                GUILayout.EndHorizontal();
                            }
                            else
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                            }
                        }

                        GUILayout.EndScrollView();

                        GUILayout.BeginHorizontal();

                        GUI.enabled = (meshList != null || meshList.Count < 0);

                        if (GUILayout.Button(nameOrder ? "[↑] Name" : "[↓] Name"))
                        {
                            nameOrder = !nameOrder;
                            if (nameOrder)
                                meshList.Sort((a, b) => Path.GetFileName(a).CompareTo(Path.GetFileName(b)));
                            else
                                meshList.Sort((a, b) => Path.GetFileName(b).CompareTo(Path.GetFileName(a)));
                        }

                        if (GUILayout.Button(lengthOrder ? "[↑] Length" : "[↓] Length"))
                        {
                            lengthOrder = !lengthOrder;
                            if (lengthOrder)
                                meshList.Sort((a, b) => Path.GetFileName(a).Length.CompareTo(Path.GetFileName(b).Length));
                            else
                                meshList.Sort((a, b) => Path.GetFileName(b).Length.CompareTo(Path.GetFileName(a).Length));
                        }

                        if (GUILayout.Button(numOrder ? "[↑] Number" : "[↓] Number"))
                        {
                            int value;
                            numOrder = !numOrder;
                            if (numOrder)
                            {
                                meshList.Sort((a, b) =>
                                {
                                    if (int.TryParse(Path.GetFileName(a), out value) && int.TryParse(Path.GetFileName(b), out value))
                                        return int.Parse(Path.GetFileName(a)).CompareTo(int.Parse(Path.GetFileName(b)));
                                    else return Path.GetFileName(a).CompareTo(Path.GetFileName(b));
                                });
                            }
                            else
                            {
                                meshList.Sort((a, b) =>
                                {
                                    if (int.TryParse(Path.GetFileName(a), out value) && int.TryParse(Path.GetFileName(b), out value))
                                        return int.Parse(Path.GetFileName(b)).CompareTo(int.Parse(Path.GetFileName(a)));
                                    else return Path.GetFileName(b).CompareTo(Path.GetFileName(a));
                                });
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);

                        #region BUTTON: CREATE SEQUENCE

                        GUILayout.BeginHorizontal();

                        GUI.color = Color.cyan;
                        GUI.enabled = (meshList.Count > 0);
                        if (GUILayout.Button("Create Sequence", GUILayout.ExpandWidth(true)))
                        {
                            float n_spd = startTime + (spd * meshList.Count);

                            using (StreamWriter sw = new StreamWriter(cachePath))
                            {
                                StringBuilder sb = new StringBuilder();

                                sb.AppendLine("<root>");
                                sb.AppendLine($@"<interpolableGroup name = ""{groupName}"" >");

                                for (int i = meshList.Count - 1; i >= 0; i--)
                                {
                                    sb.AppendLine($@"<interpolable enabled=""true"" owner=""RendererEditor"" objectIndex=""0"" id=""targetEnabled""  parameterPath = ""{meshList[i]}"" parameterType = ""0"" bgColorR = ""1"" bgColorG = ""0.3"" bgColorB = ""0.3"" alias = ""{Path.GetFileName(meshList[i])}"" >");

                                    if (i == 0)
                                        sb.AppendLine($@"<keyframe time=""0"" value=""{(hideStart ? "false" : "true")}"">");
                                    else
                                        sb.AppendLine(@"<keyframe time=""0"" value=""false"">");

                                    sb.AppendLine(@"<curveKeyframe time=""0"" value=""0"" inTangent=""0"" outTangent=""1""/>");
                                    sb.AppendLine(@"<curveKeyframe time=""1"" value=""1"" inTangent=""1"" outTangent=""0""/>");
                                    sb.AppendLine("</keyframe>");

                                    float newSpd = Mathf.Clamp(n_spd - spd, 0f, startTime + (spd * meshList.Count));

                                    if (newSpd != 0)
                                    {
                                        sb.AppendLine($@"<keyframe time=""{newSpd}"" value=""true"">");
                                        sb.AppendLine(@"<curveKeyframe time=""0"" value=""0"" inTangent=""0"" outTangent=""1""/>");
                                        sb.AppendLine(@"<curveKeyframe time=""1"" value=""1"" inTangent=""1"" outTangent=""0""/>");
                                        sb.AppendLine("</keyframe>");
                                    }

                                    if (i == meshList.Count - 1)
                                        sb.AppendLine($@"<keyframe time=""{n_spd}"" value=""{(hideEnd ? "false" : "true")}"">");
                                    else
                                        sb.AppendLine($@"<keyframe time=""{n_spd}"" value=""false"">");

                                    sb.AppendLine(@"<curveKeyframe time=""0"" value=""0"" inTangent=""0"" outTangent=""1""/>");
                                    sb.AppendLine(@"<curveKeyframe time=""1"" value=""1"" inTangent=""1"" outTangent=""0""/>");
                                    sb.AppendLine("</keyframe>");

                                    sb.AppendLine("</interpolable>");

                                    n_spd -= spd;
                                }

                                sb.AppendLine("</interpolableGroup>");
                                sb.AppendLine("</root>");

                                string finalString = sb.ToString();

                                if (!finalString.IsNullOrEmpty())
                                    sw.Write(finalString);
                            }

                            _timeline.LoadSingle(cachePath);
                        }

                        GUI.color = Color.red;
                        GUI.enabled = (meshList.Count > 0);
                        if (GUILayout.Button("Delete list", GUILayout.ExpandWidth(true)))
                        {
                            meshList.Clear();
                        }
                        GUILayout.EndHorizontal();

                        #endregion BUTTON: CREATE SEQUENCE

                        GUI.color = defColor;

                        #region START TIME FIELD

                        GUILayout.BeginVertical(GUI.skin.box);

                        GUILayout.BeginHorizontal();

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("Start\ntime:");
                        strStartTime = GUILayout.TextField(strStartTime, GUI.skin.box, GUILayout.Width(50));
                        if (!float.TryParse(strStartTime, out startTime) || startTime < 0f)
                        {
                            strStartTime = "0";
                            startTime = 0f;
                        }
                        GUILayout.EndVertical();

                        #endregion START TIME FIELD

                        #region SPEED FIELD

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("Frame\nspeed:");
                        strSpd = GUILayout.TextField(strSpd, GUI.skin.box, GUILayout.Width(50));
                        if (!float.TryParse(strSpd, out spd) || spd <= 0f)
                        {
                            strSpd = "0.1";
                            spd = 0.1f;
                        }
                        GUILayout.EndVertical();

                        #endregion SPEED FIELD

                        #region NAME FIELD

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("Group\nname:");
                        groupName = GUILayout.TextField(groupName, GUI.skin.box, GUILayout.Width(120));
                        if (groupName.IsNullOrEmpty()) groupName = "Sequence";
                        GUILayout.EndVertical();

                        GUILayout.EndHorizontal();

                        #endregion NAME FIELD

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(5);
                        hideStart = GUILayout.Toggle(hideStart, "Hide first mesh");

                        hideEnd = GUILayout.Toggle(hideEnd, "Hide last mesh");
                        GUILayout.Space(5);
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            GUI.DragWindow();
        }

    }
}
