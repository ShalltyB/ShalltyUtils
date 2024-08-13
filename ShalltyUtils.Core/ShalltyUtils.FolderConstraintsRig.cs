using KKAPI.Utilities;
using Studio;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;
using NodesConstraints;
using static NodesConstraints.NodesConstraints;
using static ShalltyUtils.ShalltyUtils;
using ToolBox.Extensions;
using System.Linq;
using System.Text;
using static ShalltyUtils.GuideObjectPicker;

namespace ShalltyUtils
{
    public class FolderConstraintsRig
    {
        public static Rect windowFoldersConstraintsRigsRect = new Rect(400, 100, 300, 500);
        public static bool showFoldersConstraintsRigsUI = false;

        public static string fcr_loadedRigName = string.Empty;
        public static ObjectInfo fcr_rootObjectInfo;
        public static Dictionary<int, string> fcr_dicNodesNames = new Dictionary<int, string>();
        public static Dictionary<int, int> fcr_dicChangeKey = new Dictionary<int, int>();
        public static List<TemporaryConstraintData> fcr_temporaryConstraints = new List<TemporaryConstraintData>();
        public static string fcr_constraintsData = string.Empty;
        public static string fcr_guideObjectPickerData = string.Empty;

        public static bool fcr_replaceTimeline = false;
        public static Vector2 fcr_scrollBar = Vector2.zero;
       

        public class TemporaryConstraintData
        {
            public bool enabled;

            public TransformLock positionLocks;
            public TransformLock rotationLocks;
            public TransformLock scaleLocks;

            public int parentDicKey;
            public string parentPath;
            public int childDicKey;
            public string childPath;

            public bool position;
            public bool rotation;
            public bool lookAt;
            public bool scale;

            public bool mirrorPosition;
            public bool mirrorRotation;
            public bool mirrorScale;

            public float positionChangeFactor;
            public float rotationChangeFactor;
            public float scaleChangeFactor;

            public float positionDamp;
            public float rotationDamp;
            public float scaleDamp;

            public bool resetOriginalPosition;
            public bool resetOriginalRotation;
            public bool resetOriginalScale;

            public Vector3 positionOffset;
            public Quaternion rotationOffset;
            public Vector3 scaleOffset;

            public Vector3 originalChildPosition;
            public Quaternion originalChildRotation;
            public Vector3 originalChildScale;

            public string alias;
            public bool fixDynamicBone;

            public Vector3 originalParentPosition;
            public Quaternion originalParentRotation;
            public Vector3 originalParentScale;

            public int missingLinkIndex;
            public int missingLinkKind;
            public string missingLinkName;

            public ObjectCtrlInfo linkedOCI;
        }

        public struct FolderConstraint
        {
            public ObjectCtrlInfo parent;
            public string parentPath;
            public ObjectCtrlInfo child;
            public string childPath;
            public Constraint constraint;
        }

        public static void Window(int WindowID)
        {
            GUI.enabled = true;
            GUI.color = defColor;

            if (GUI.Button(new Rect(windowFoldersConstraintsRigsRect.width - 18, 0, 18, 18), "X")) showFoldersConstraintsRigsUI = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            GUILayout.Space(10f);

            string selectedItem = "(Nothing)";
            if (firstObject?.guideObject != null)
                selectedItem = firstObject.treeNodeObject.textName;

            GUI.enabled = firstObject?.guideObject != null;
            if (GUILayout.Button($"Save FCR: {selectedItem}"))
            {
                SaveFCR();
            }

            GUILayout.Space(5f);

            GUI.enabled = true;
            if (GUILayout.Button($"Load FolderConstraints Rig"))
            {
                LoadFCR();
            }

            GUILayout.Space(20f);

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            string currentFolderConstraintsRig = "None";
            if (fcr_rootObjectInfo != null)
                currentFolderConstraintsRig = fcr_loadedRigName;

            GUILayout.Label("Loaded FCR: " + currentFolderConstraintsRig);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            if (fcr_rootObjectInfo != null)
            {

                GUILayout.Label("Objects Count: " + fcr_dicChangeKey.Count);
                GUILayout.Label("Constraints Count: " + fcr_temporaryConstraints.Count);

                GUILayout.BeginVertical(GUI.skin.box);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Missing Links: ");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                fcr_scrollBar = GUILayout.BeginScrollView(fcr_scrollBar, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

                HashSet<int> usedKeys = new HashSet<int>();
                for (int x = 0; x < fcr_temporaryConstraints.Count; x++)
                {
                    TemporaryConstraintData c = fcr_temporaryConstraints[x];

                    if (c.parentDicKey != -1 && c.childDicKey != -1) continue;

                    if (usedKeys.Contains(c.missingLinkIndex))
                        continue;
                    else
                        usedKeys.Add(c.missingLinkIndex);

                    string kind = "Unknown";

                    switch (c.missingLinkKind)
                    {
                        case 0:
                            kind = "Character";
                            break;
                        case 1:
                            kind = "Item";
                            break;
                        case 2:
                            kind = "Light";
                            break;
                        case 3:
                            kind = "Folder";
                            break;
                        case 4:
                            kind = "Route";
                            break;
                        case 5:
                            kind = "Camera";
                            break;
                    }

                    GUILayout.BeginVertical(GUI.skin.box);

                    GUILayout.Label("Original Name: " + c.missingLinkName);

                    GUILayout.Label("Type: " + kind);

                    GUI.color = c.linkedOCI == null ? Color.red : Color.green;
                    string linkedItem = "(None)";
                    if (c.linkedOCI != null)
                        linkedItem = c.linkedOCI.treeNodeObject.textName;
                    GUILayout.Label("Linked Item: " + linkedItem);
                    GUI.color = defColor;

                    if (GUILayout.Button("Link to: " + selectedItem))
                    {
                        foreach (TemporaryConstraintData t in fcr_temporaryConstraints)
                        {
                            if (t.missingLinkIndex == c.missingLinkIndex)
                            {
                                if (firstObject?.guideObject != null)
                                    t.linkedOCI = firstObject;
                                else
                                    t.linkedOCI = null;
                            }
                        }
                    }

                    GUILayout.EndVertical();
                }

                GUILayout.EndScrollView();

                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                fcr_replaceTimeline = GUILayout.Toggle(fcr_replaceTimeline, "Replace Timeline?");

                GUILayout.Space(10f);

                if (GUILayout.Button("Create FolderConstraints Rig"))
                {
                    CreateFCR();
                }

                if (GUILayout.Button("Cancel Loading"))
                {
                    fcr_rootObjectInfo.DeleteKey();
                    ClearLoadedRigData();
                }

            }
            else
            {
                GUILayout.FlexibleSpace();
            }


            GUILayout.EndVertical();


            GUI.enabled = true;

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            windowFoldersConstraintsRigsRect = IMGUIUtils.DragResizeEatWindow(_uniqueId + 40, windowFoldersConstraintsRigsRect);
        }


        private static void SaveFCR()
        {
            if (firstObject == null) return;

            var studio = Singleton<Studio.Studio>.Instance;

            List<TreeNodeObject> allChildrens = GetAllTreeNodesChildren(firstObject.treeNodeObject);

            Dictionary<TreeNodeObject, ObjectCtrlInfo> dicInfo = studio.dicInfo;
            List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();

            List<ObjectCtrlInfo> allOCIs = allChildrens.Where(x => dicInfo.ContainsKey(x)).Select(x => dicInfo[x]).ToList();
            List<GuideObject> allGuideObjects = allOCIs.Select(x => x.guideObject).ToList();

            List<FolderConstraint> parentlessConstraints = new List<FolderConstraint>();
            List<FolderConstraint> childrenlessConstraints = new List<FolderConstraint>();
            List<FolderConstraint> selfContainedConstraints = new List<FolderConstraint>();

            List<FolderConstraint> folderConstraints = new List<FolderConstraint>();

            foreach (var c in _nodeConstraints._constraints)
            {
                if (c == null || c.childTransform == null || c.parentTransform == null) continue;

                int parentObjectIndex = -1;
                Transform parentT = c.parentTransform;

                while ((parentObjectIndex = dic.FindIndex(e => e.Value.guideObject.transformTarget == parentT)) == -1)
                    parentT = parentT.parent;

                string parentPath = c.parentTransform.GetPathFrom(parentT);

                int childObjectIndex = -1;
                Transform childT = c.childTransform;

                while ((childObjectIndex = dic.FindIndex(e => e.Value.guideObject.transformTarget == childT)) == -1)
                    childT = childT.parent;

                string childPath = c.childTransform.GetPathFrom(childT);

                if (parentObjectIndex == -1 || childObjectIndex == -1) continue;

                var parentOCI = dic[parentObjectIndex].Value;
                var childOCI = dic[childObjectIndex].Value;

                folderConstraints.Add(new FolderConstraint() { constraint = c, parent = parentOCI, child = childOCI, parentPath = parentPath, childPath = childPath });
            }

            foreach (GuideObject guideObject in allGuideObjects)
            {
                if (guideObject == null) continue;

                foreach (var c in folderConstraints)
                {
                    if (c.child == null || c.parent == null || c.constraint == null) continue;

                    if (c.parent.guideObject == guideObject)
                    {
                        if (allGuideObjects.Contains(c.child.guideObject))
                        {
                            if (selfContainedConstraints.Contains(c) == false)
                                selfContainedConstraints.Add(c);
                        }
                        else
                        {
                            if (childrenlessConstraints.Contains(c) == false)
                                childrenlessConstraints.Add(c);
                        }
                    }

                    if (c.child.guideObject == guideObject)
                    {
                        if (allGuideObjects.Contains(c.parent.guideObject))
                        {
                            if (selfContainedConstraints.Contains(c) == false)
                                selfContainedConstraints.Add(c);
                        }
                        else
                        {
                            if (parentlessConstraints.Contains(c) == false)
                                parentlessConstraints.Add(c);
                        }
                    }
                }
            }

            studio.SavePreprocessingLoop(firstObject.treeNodeObject);

            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("SAVE RIG", _defaultDir, "FoldersConstraintsRig files (*.fcr)|*.fcr", "fcr", SingleFileFlags);
            if (file == null) return;

            var sceneInfo = studio.sceneInfo;
            var rootObject = firstObject.objectInfo;

            Dictionary<int, string> nodeNamesDic = Studio.Studio.Instance.dicObjectCtrl.ToDictionary(x => x.Key, x => x.Value.treeNodeObject.textName);

            using (FileStream fileStream = new FileStream(file[0], FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    var constraintsData = Encoding.UTF8.GetBytes(SaveConstraintsData(selfContainedConstraints, parentlessConstraints, childrenlessConstraints));
                    binaryWriter.Write(constraintsData.Length);
                    binaryWriter.Write(constraintsData);

                    var guideObjectPickerData = Encoding.UTF8.GetBytes(SaveGuideObjectPickerData(allGuideObjects));
                    binaryWriter.Write(guideObjectPickerData.Length);
                    binaryWriter.Write(guideObjectPickerData);

                    binaryWriter.Write(nodeNamesDic.Count);
                    foreach (var kvp in nodeNamesDic)
                    {
                        binaryWriter.Write(kvp.Key);
                        binaryWriter.Write(kvp.Value);
                    }

                    switch (rootObject.kind)
                    {
                        case 1:
                            WriteObjectInfoItem(binaryWriter, rootObject as OIItemInfo);
                            break;
                        case 3:
                            WriteObjectInfoFolder(binaryWriter, rootObject as OIFolderInfo);
                            break;
                    }
                }
            }
        }

        private static void LoadFCR()
        {
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("LOAD RIG", _defaultDir, "FoldersConstraintsRig files (*.fcr)|*.fcr", "fcr", SingleFileFlags);
            if (file == null || file[0].IsNullOrEmpty()) return;

            var studio = Singleton<Studio.Studio>.Instance;
            var sceneInfo = studio.sceneInfo;

            // INIT THE DATA STRUCTURES

            ClearLoadedRigData();
            fcr_loadedRigName = Path.GetFileNameWithoutExtension(file[0]);

            // READ THE DATA

            using (FileStream fileStream = new FileStream(file[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (BinaryReader binaryReader = new BinaryReader(fileStream))
                {
                    // Read the constraints data
                    int constraintsDataLength = binaryReader.ReadInt32();
                    var constraintsData = binaryReader.ReadBytes(constraintsDataLength);
                    fcr_constraintsData = Encoding.UTF8.GetString(constraintsData);

                    // Read the GuideObject Picker data
                    int guideObjectPickerDataLength = binaryReader.ReadInt32();
                    var guideObjectPickerData = binaryReader.ReadBytes(guideObjectPickerDataLength);
                    fcr_guideObjectPickerData = Encoding.UTF8.GetString(guideObjectPickerData);

                    //Read the TreeNodes names
                    int count = binaryReader.ReadInt32();
                    for (int i = 0; i < count; i++)
                        fcr_dicNodesNames.Add(binaryReader.ReadInt32(), binaryReader.ReadString());

                    // Read all the OCIs data
                    int kind = binaryReader.ReadInt32();
                    switch (kind)
                    {
                        case 1:
                            fcr_rootObjectInfo = new OIItemInfo(-1, -1, -1, Studio.Studio.GetNewIndex());
                            ReadObjectInfoItem(binaryReader, (OIItemInfo)fcr_rootObjectInfo, ref fcr_dicChangeKey);
                            break;
                        case 3:
                            fcr_rootObjectInfo = new OIFolderInfo(Studio.Studio.GetNewIndex());
                            ReadObjectInfoFolder(binaryReader, (OIFolderInfo)fcr_rootObjectInfo, ref fcr_dicChangeKey);
                            break;
                        case 5:
                            fcr_rootObjectInfo = new OICameraInfo(Studio.Studio.GetNewIndex());
                            ReadObjectInfoCamera(binaryReader, (OICameraInfo)fcr_rootObjectInfo, ref fcr_dicChangeKey);
                            break;
                    }
                }
            }

            if (fcr_rootObjectInfo == null) return;

            if (!fcr_constraintsData.IsNullOrEmpty())
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(fcr_constraintsData);
                var node = doc.FirstChild;

                foreach (XmlNode childNode in node.ChildNodes)
                {

                    TemporaryConstraintData c = new TemporaryConstraintData
                    {
                        parentDicKey = childNode.ReadInt("parentOCIdicKey"),
                        childDicKey = childNode.ReadInt("childOCIdicKey"),
                        parentPath = childNode.Attributes["parentPath"].Value,
                        childPath = childNode.Attributes["childPath"].Value,
                        enabled = childNode.ReadBool("enabled"),
                        fixDynamicBone = childNode.ReadBool("dynamic"),
                        position = childNode.ReadBool("position"),
                        rotation = childNode.ReadBool("rotation"),
                        scale = childNode.ReadBool("scale"),
                        positionOffset = childNode.ReadVector3("positionOffset"),
                        rotationOffset = childNode.ReadQuaternion("rotationOffset"),
                        scaleOffset = childNode.ReadVector3("scaleOffset"),
                        mirrorPosition = childNode.ReadBool("mirrorPosition"),
                        mirrorRotation = childNode.ReadBool("mirrorRotation"),
                        mirrorScale = childNode.ReadBool("mirrorScale"),
                        lookAt = childNode.ReadBool("lookAt"),
                        originalParentPosition = childNode.ReadVector3("originalParentPosition"),
                        originalParentRotation = childNode.ReadQuaternion("originalParentRotation"),
                        originalParentScale = childNode.ReadVector3("originalParentScale"),
                        //originalChildPosition = childNode.ReadVector3("originalChildPosition"),
                        //originalChildRotation = childNode.ReadQuaternion("originalChildRotation"),
                        //originalChildScale = childNode.ReadVector3("originalChildScale"),
                        positionChangeFactor = childNode.ReadFloat("positionChangeFactor"),
                        rotationChangeFactor = childNode.ReadFloat("rotationChangeFactor"),
                        scaleChangeFactor = childNode.ReadFloat("scaleChangeFactor"),
                        positionDamp = childNode.ReadFloat("positionDamp"),
                        rotationDamp = childNode.ReadFloat("rotationDamp"),
                        scaleDamp = childNode.ReadFloat("scaleDamp"),
                        positionLocks = new TransformLock(childNode.ReadBool("positionLocksX"), childNode.ReadBool("positionLocksY"), childNode.ReadBool("positionLocksZ")),
                        rotationLocks = new TransformLock(childNode.ReadBool("rotationLocksX"), childNode.ReadBool("rotationLocksY"), childNode.ReadBool("rotationLocksZ")),
                        scaleLocks = new TransformLock(childNode.ReadBool("scaleLocksX"), childNode.ReadBool("scaleLocksY"), childNode.ReadBool("scaleLocksZ")),
                        resetOriginalPosition = childNode.ReadBool("resetOriginalPosition"),
                        resetOriginalRotation = childNode.ReadBool("resetOriginalRotation"),
                        resetOriginalScale = childNode.ReadBool("resetOriginalScale"),
                        alias = childNode.Attributes["alias"] != null ? childNode.Attributes["alias"].Value : ""
                    };

                    if (childNode.Name == "parentlessConstraint" || childNode.Name == "childrenlessConstraint")
                    {
                        c.missingLinkIndex = childNode.ReadInt("missingLinkIndex");
                        c.missingLinkKind = childNode.ReadInt("missingLinkKind");
                        c.missingLinkName = childNode.Attributes["missingLinkName"].Value;
                    }

                    fcr_temporaryConstraints.Add(c);
                }
            }
        }

        private static void CreateFCR()
        {
            // LOAD THE DATA

            var studio = Singleton<Studio.Studio>.Instance;
            var sceneInfo = studio.sceneInfo;

            sceneInfo.dicObject.Add(fcr_rootObjectInfo.dicKey, fcr_rootObjectInfo);
            switch (fcr_rootObjectInfo.kind)
            {
                case 1:
                    AddObjectItem.Load(fcr_rootObjectInfo as OIItemInfo, null, null);
                    break;
                case 3:
                    AddObjectFolder.Load(fcr_rootObjectInfo as OIFolderInfo, null, null);
                    break;
                case 5:
                    AddObjectCamera.Load(fcr_rootObjectInfo as OICameraInfo, null, null);
                    break;
            }

            studio.treeNodeCtrl.RefreshHierachy();

            Dictionary<int, ObjectCtrlInfo> loadedOCIs = fcr_dicChangeKey.ToDictionary(kvp => kvp.Value, kvp => Studio.Studio.Instance.dicObjectCtrl[kvp.Key]);

            foreach (var kvp in loadedOCIs)
                kvp.Value.treeNodeObject.textName = fcr_dicNodesNames[kvp.Key];


            // CREATE THE CONSTRAINTS
            foreach (var c in fcr_temporaryConstraints)
            {
                Transform parentTransform = null;
                Transform childTransform = null;

                if (c.parentDicKey == -1)
                    parentTransform = c.linkedOCI?.guideObject.transformTarget;
                else
                    parentTransform = loadedOCIs[c.parentDicKey].guideObject.transformTarget;

                if (c.childDicKey == -1)
                    childTransform = c.linkedOCI?.guideObject.transformTarget;
                else
                    childTransform = loadedOCIs[c.childDicKey].guideObject.transformTarget;

                parentTransform = parentTransform?.Find(c.parentPath);
                childTransform = childTransform?.Find(c.childPath);

                if (parentTransform == null || childTransform == null) continue;

                var nC = _nodeConstraints.AddConstraint
                   (
                       c.enabled,
                       parentTransform,
                       childTransform,
                       c.position,
                       c.positionOffset,
                       c.rotation,
                       c.rotationOffset,
                       c.scale,
                       c.scaleOffset,
                       c.alias
                   );

                nC.fixDynamicBone = c.fixDynamicBone;
                nC.positionChangeFactor = c.positionChangeFactor;
                nC.rotationChangeFactor = c.rotationChangeFactor;
                nC.scaleChangeFactor = c.scaleChangeFactor;
                nC.positionDamp = c.positionDamp;
                nC.rotationDamp = c.rotationDamp;
                nC.scaleDamp = c.scaleDamp;
                nC.positionLocks = c.positionLocks;
                nC.rotationLocks = c.rotationLocks;
                nC.scaleLocks = c.scaleLocks;
                nC.originalParentPosition = c.originalParentPosition;
                nC.originalParentRotation = c.originalParentRotation;
                nC.originalParentScale = c.originalParentScale;
                nC.resetOriginalPosition = c.resetOriginalPosition;
                nC.resetOriginalRotation = c.resetOriginalRotation;
                nC.resetOriginalScale = c.resetOriginalScale;

                if (fcr_replaceTimeline && _timeline._interpolables.Count > 0)
                {
                    if (c.parentDicKey == -1)
                        ReplaceTimelineInterpolables(loadedOCIs[c.childDicKey], parentTransform, c.linkedOCI, c.alias);
                    if (c.childDicKey == -1)
                        ReplaceTimelineInterpolables(loadedOCIs[c.parentDicKey], childTransform, c.linkedOCI, c.alias);

                    foreach (var kvp in loadedOCIs)
                    {
                        kvp.Value.guideObject.changeAmount.pos = Vector3.zero;
                        kvp.Value.guideObject.changeAmount.rot = Vector3.zero;
                    }

                    Timeline.Timeline.NextFrame();
                    Timeline.Timeline.PreviousFrame();
                }
            }

            // CREATE THE GUIDE OBJECT PICKER PAGES
            LoadGuideObjectPickerData(fcr_guideObjectPickerData, loadedOCIs);

            ClearLoadedRigData();
        }


        private static void ClearLoadedRigData()
        {
            fcr_loadedRigName = string.Empty;
            fcr_rootObjectInfo = null;
            fcr_dicChangeKey.Clear();
            fcr_dicNodesNames.Clear();
            fcr_temporaryConstraints.Clear();
            fcr_constraintsData = string.Empty;
            fcr_guideObjectPickerData = string.Empty;
        }

        private static List<TreeNodeObject> GetAllTreeNodesChildren(TreeNodeObject parent)
        {
            List<TreeNodeObject> childNodes = new List<TreeNodeObject>();

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                childNodes.Add(parent.child[i]);
                childNodes.AddRange(GetAllTreeNodesChildren(parent.child[i]));
            }

            return childNodes;
        }

        private static string SaveConstraintsData(List<FolderConstraint> selfContainedConstraints, List<FolderConstraint> parentlessConstraints, List<FolderConstraint> childrenlessConstraints)
        {
            void WriteConstraintData(XmlTextWriter xmlWriter, FolderConstraint folderConstraint)
            {
                Constraint constraint = folderConstraint.constraint;

                xmlWriter.WriteAttributeString("parentPath", folderConstraint.parentPath);
                xmlWriter.WriteAttributeString("childPath", folderConstraint.childPath);

                xmlWriter.WriteValue("enabled", constraint.enabled);

                xmlWriter.WriteValue("position", constraint.position);
                xmlWriter.WriteValue("mirrorPosition", constraint.mirrorPosition);
                xmlWriter.WriteValue("positionOffset", constraint.positionOffset);

                xmlWriter.WriteValue("rotation", constraint.rotation);
                xmlWriter.WriteValue("mirrorRotation", constraint.mirrorRotation);
                xmlWriter.WriteValue("rotationOffset", constraint.rotationOffset);

                xmlWriter.WriteValue("scale", constraint.scale);
                xmlWriter.WriteValue("mirrorScale", constraint.mirrorScale);
                xmlWriter.WriteValue("scaleOffset", constraint.scaleOffset);

                xmlWriter.WriteValue("lookAt", constraint.lookAt);

                xmlWriter.WriteValue("originalParentPosition", constraint.originalParentPosition);
                xmlWriter.WriteValue("originalParentRotation", constraint.originalParentRotation);
                xmlWriter.WriteValue("originalParentScale", constraint.originalParentScale);

                xmlWriter.WriteValue("positionChangeFactor", constraint.positionChangeFactor);
                xmlWriter.WriteValue("rotationChangeFactor", constraint.rotationChangeFactor);
                xmlWriter.WriteValue("scaleChangeFactor", constraint.scaleChangeFactor);

                xmlWriter.WriteValue("positionDamp", constraint.positionDamp);
                xmlWriter.WriteValue("rotationDamp", constraint.rotationDamp);
                xmlWriter.WriteValue("scaleDamp", constraint.scaleDamp);

                xmlWriter.WriteAttributeString("positionLocksX", XmlConvert.ToString(constraint.positionLocks.x));
                xmlWriter.WriteAttributeString("positionLocksY", XmlConvert.ToString(constraint.positionLocks.y));
                xmlWriter.WriteAttributeString("positionLocksZ", XmlConvert.ToString(constraint.positionLocks.z));

                xmlWriter.WriteAttributeString("rotationLocksX", XmlConvert.ToString(constraint.rotationLocks.x));
                xmlWriter.WriteAttributeString("rotationLocksY", XmlConvert.ToString(constraint.rotationLocks.y));
                xmlWriter.WriteAttributeString("rotationLocksZ", XmlConvert.ToString(constraint.rotationLocks.z));

                xmlWriter.WriteAttributeString("scaleLocksX", XmlConvert.ToString(constraint.scaleLocks.x));
                xmlWriter.WriteAttributeString("scaleLocksY", XmlConvert.ToString(constraint.scaleLocks.y));
                xmlWriter.WriteAttributeString("scaleLocksZ", XmlConvert.ToString(constraint.scaleLocks.z));

                xmlWriter.WriteValue("resetOriginalPosition", constraint.resetOriginalPosition);
                xmlWriter.WriteValue("resetOriginalRotation", constraint.resetOriginalRotation);
                xmlWriter.WriteValue("resetOriginalScale", constraint.resetOriginalScale);

                xmlWriter.WriteValue("dynamic", constraint.fixDynamicBone);

                xmlWriter.WriteAttributeString("alias", constraint.alias);
            }

            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter))
                {
                    List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();

                    xmlWriter.WriteStartElement("constraintsData");

                    foreach (FolderConstraint folderConstraint in selfContainedConstraints)
                    {
                        xmlWriter.WriteStartElement("selfContainedConstraint");
                        xmlWriter.WriteValue("parentOCIdicKey", folderConstraint.parent.objectInfo.dicKey);
                        xmlWriter.WriteValue("childOCIdicKey", folderConstraint.child.objectInfo.dicKey);
                        WriteConstraintData(xmlWriter, folderConstraint);
                        xmlWriter.WriteEndElement();
                    }


                    List<ObjectCtrlInfo> parents = new List<ObjectCtrlInfo>();
                    foreach (FolderConstraint folderConstraint in parentlessConstraints)
                    {
                        xmlWriter.WriteStartElement("parentlessConstraint");

                        if (parents.Contains(folderConstraint.parent) == false)
                            parents.Add(folderConstraint.parent);

                        int index = parents.IndexOf(folderConstraint.parent);

                        xmlWriter.WriteValue("parentOCIdicKey", -1);
                        xmlWriter.WriteValue("childOCIdicKey", folderConstraint.child.objectInfo.dicKey);

                        xmlWriter.WriteValue("missingLinkIndex", index);
                        xmlWriter.WriteValue("missingLinkKind", folderConstraint.parent.objectInfo.kind);
                        xmlWriter.WriteAttributeString("missingLinkName", folderConstraint.parent.treeNodeObject.textName);

                        WriteConstraintData(xmlWriter, folderConstraint);
                        xmlWriter.WriteEndElement();
                    }

                    foreach (FolderConstraint folderConstraint in childrenlessConstraints)
                    {
                        xmlWriter.WriteStartElement("childrenlessConstraint");

                        xmlWriter.WriteValue("parentOCIdicKey", folderConstraint.parent.objectInfo.dicKey);
                        xmlWriter.WriteValue("childOCIdicKey", -1);

                        if (parents.Contains(folderConstraint.child) == false)
                            parents.Add(folderConstraint.child);

                        int index = parents.IndexOf(folderConstraint.child);

                        xmlWriter.WriteValue("missingLinkIndex", index);
                        xmlWriter.WriteValue("missingLinkKind", folderConstraint.child.objectInfo.kind);
                        xmlWriter.WriteAttributeString("missingLinkName", folderConstraint.child.treeNodeObject.textName);

                        WriteConstraintData(xmlWriter, folderConstraint);
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        private static string SaveGuideObjectPickerData(List<GuideObject> allGuideObjects)
        {
            Dictionary<int, ObjectCtrlInfo> dic = new Dictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl);
            Dictionary<PickerPage, List<PickerButton>> allPagesDic = new Dictionary<PickerPage, List<PickerButton>>();

            foreach (PickerPage page in goPickerPages)
            {
                foreach (PickerButton button in page.pageButtons)
                {
                    if (allGuideObjects.Contains(button.guideObject))
                    {
                        if (allPagesDic.ContainsKey(page) == false)
                            allPagesDic.Add(page, new List<PickerButton>() { button });
                        else
                            allPagesDic[page].Add(button);
                    }
                }
            }

            if (allPagesDic.Count == 0) return "";

            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter))
                {
                    xmlWriter.WriteStartElement("guideObjectPickerData");

                    foreach (var kvp in allPagesDic)
                    {
                        var page = new PickerPage(kvp.Key) { pageButtons = kvp.Value };
                        SaveSinglePageDic(page, xmlWriter, dic);
                    }

                    xmlWriter.WriteEndElement();
                }

                return stringWriter.ToString();
            }
        }

        private static void LoadGuideObjectPickerData(string data, Dictionary<int, ObjectCtrlInfo> dic)
        {
            if (data.IsNullOrEmpty()) return;

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(data);

            XmlNode node = doc.FirstChild;
            if (node == null) return;

            foreach (XmlNode childNode in node.ChildNodes)
                LoadSinglePageDic(childNode, dic);

            UpdatePickerPages();

            _self.ExecuteDelayed2(() =>
            {
                UpdateAllColors();
            });
        }


        #region Write ObjectInfo Data

        private static void WriteObjectInfoBase(BinaryWriter binaryWriter, ObjectInfo objectInfo)
        {
            binaryWriter.Write(objectInfo.kind);
            binaryWriter.Write(objectInfo.dicKey);

            binaryWriter.Write(objectInfo.changeAmount.m_Pos.x);
            binaryWriter.Write(objectInfo.changeAmount.m_Pos.y);
            binaryWriter.Write(objectInfo.changeAmount.m_Pos.z);
            binaryWriter.Write(objectInfo.changeAmount.m_Rot.x);
            binaryWriter.Write(objectInfo.changeAmount.m_Rot.y);
            binaryWriter.Write(objectInfo.changeAmount.m_Rot.z);
            binaryWriter.Write(objectInfo.changeAmount.m_Scale.x);
            binaryWriter.Write(objectInfo.changeAmount.m_Scale.y);
            binaryWriter.Write(objectInfo.changeAmount.m_Scale.z);

            binaryWriter.Write((int)objectInfo.treeState);
            binaryWriter.Write(objectInfo.visible);
        }

        private static void WriteObjectInfoFolder(BinaryWriter binaryWriter, OIFolderInfo objectInfo)
        {
            WriteObjectInfoBase(binaryWriter, objectInfo);
            binaryWriter.Write(objectInfo.name);
            WriteObjectInfoChildren(binaryWriter, objectInfo.child);
        }

        private static void WriteObjectInfoItem(BinaryWriter binaryWriter, OIItemInfo objectInfo)
        {
            WriteObjectInfoBase(binaryWriter, objectInfo);
            binaryWriter.Write(objectInfo.group);
            binaryWriter.Write(objectInfo.category);
            binaryWriter.Write(objectInfo.no);
            binaryWriter.Write(objectInfo.animeSpeed);
#if !HS2
            for (int i = 0; i < 8; i++)
            {
                binaryWriter.Write(JsonUtility.ToJson(objectInfo.color[i]));
            }
            for (int j = 0; j < 3; j++)
            {
                binaryWriter.Write(objectInfo.pattern[j]._key.Value);
                binaryWriter.Write(objectInfo.pattern[j]._filePath.Value);
                binaryWriter.Write(objectInfo.pattern[j].clamp);
                binaryWriter.Write(JsonUtility.ToJson(objectInfo.pattern[j].uv));
                binaryWriter.Write(objectInfo.pattern[j].rot);
            }

            binaryWriter.Write(objectInfo.alpha);
            binaryWriter.Write(JsonUtility.ToJson(objectInfo.lineColor));
            binaryWriter.Write(objectInfo.lineWidth);
            binaryWriter.Write(JsonUtility.ToJson(objectInfo.emissionColor));
            binaryWriter.Write(objectInfo.emissionPower);
            binaryWriter.Write(objectInfo.lightCancel);
#endif

            binaryWriter.Write(objectInfo.panel._key.Value);
            binaryWriter.Write(objectInfo.panel._filePath.Value);
            binaryWriter.Write(objectInfo.panel.clamp);
            binaryWriter.Write(JsonUtility.ToJson(objectInfo.panel.uv));
            binaryWriter.Write(objectInfo.panel.rot);

            binaryWriter.Write(objectInfo.enableFK);
            binaryWriter.Write(objectInfo.bones.Count);
            foreach (KeyValuePair<string, OIBoneInfo> keyValuePair in objectInfo.bones)
            {
                binaryWriter.Write(keyValuePair.Key);
                binaryWriter.Write(keyValuePair.Value.dicKey);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Pos.x);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Pos.y);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Pos.z);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Rot.x);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Rot.y);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Rot.z);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Scale.x);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Scale.y);
                binaryWriter.Write(keyValuePair.Value.changeAmount.m_Scale.z);
            }
            binaryWriter.Write(objectInfo.enableDynamicBone);
            binaryWriter.Write(objectInfo.animeNormalizedTime);
            WriteObjectInfoChildren(binaryWriter, objectInfo.child);
        }

        private static void WriteObjectInfoCamera(BinaryWriter binaryWriter, OICameraInfo objectInfo)
        {
            WriteObjectInfoBase(binaryWriter, objectInfo);
            binaryWriter.Write(objectInfo.name);
            binaryWriter.Write(objectInfo.active);
        }

        public static void WriteObjectInfoChildren(BinaryWriter binaryWriter, List<ObjectInfo> _list)
        {
            int count = _list.Count;
            binaryWriter.Write(count);
            for (int i = 0; i < count; i++)
            {
                switch (_list[i].kind)
                {
                    case 1:
                        WriteObjectInfoItem(binaryWriter, _list[i] as OIItemInfo);
                        break;
                    case 3:
                        WriteObjectInfoFolder(binaryWriter, _list[i] as OIFolderInfo);
                        break;
                    case 5:
                        WriteObjectInfoCamera(binaryWriter, _list[i] as OICameraInfo);
                        break;
                }
            }
        }

        #endregion

        #region Read ObjectInfo Data

        private static void ReadObjectInfoBase(BinaryReader binaryReader, ObjectInfo objectInfo, ref Dictionary<int, int> dicChangeKey)
        {
            // save old dicKey
            dicChangeKey.Add(objectInfo.dicKey, binaryReader.ReadInt32());

            Vector3 vector = objectInfo.changeAmount.m_Pos;
            vector.Set(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
            objectInfo.changeAmount.m_Pos = vector;
            vector = objectInfo.changeAmount.m_Rot;
            vector.Set(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
            objectInfo.changeAmount.m_Rot = vector;
            vector = objectInfo.changeAmount.m_Scale;
            vector.Set(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
            objectInfo.changeAmount.m_Scale = vector;

            objectInfo.treeState = (TreeNodeObject.TreeState)binaryReader.ReadInt32();
            objectInfo.visible = binaryReader.ReadBoolean();
        }

        private static void ReadObjectInfoFolder(BinaryReader binaryReader, OIFolderInfo objectInfo, ref Dictionary<int, int> dicChangeKey)
        {
            ReadObjectInfoBase(binaryReader, objectInfo, ref dicChangeKey);
            objectInfo.name = binaryReader.ReadString();
            ReadObjectInfoChildren(binaryReader, objectInfo.child, ref dicChangeKey);
        }

        private static void ReadObjectInfoCamera(BinaryReader binaryReader, OICameraInfo objectInfo, ref Dictionary<int, int> dicChangeKey)
        {
            ReadObjectInfoBase(binaryReader, objectInfo, ref dicChangeKey);
            objectInfo.name = binaryReader.ReadString();
            objectInfo.active = binaryReader.ReadBoolean();
        }

        private static void ReadObjectInfoItem(BinaryReader binaryReader, OIItemInfo objectInfo, ref Dictionary<int, int> dicChangeKey)
        {
            ReadObjectInfoBase(binaryReader, objectInfo, ref dicChangeKey);
            objectInfo.group = binaryReader.ReadInt32();
            objectInfo.category = binaryReader.ReadInt32();
            objectInfo.no = binaryReader.ReadInt32();
            objectInfo.animeSpeed = binaryReader.ReadSingle();
#if !HS2
            for (int i = 0; i < 8; i++)
            {
                objectInfo.color[i] = JsonUtility.FromJson<Color>(binaryReader.ReadString());
            }
            for (int k = 0; k < 3; k++)
            {
                objectInfo.pattern[k]._key.Value = binaryReader.ReadInt32();
                objectInfo.pattern[k]._filePath.Value = binaryReader.ReadString();
                objectInfo.pattern[k].clamp = binaryReader.ReadBoolean();
                objectInfo.pattern[k].uv = JsonUtility.FromJson<Vector4>(binaryReader.ReadString());
                objectInfo.pattern[k].rot = binaryReader.ReadSingle();
            }
            objectInfo.alpha = binaryReader.ReadSingle();
            objectInfo.lineColor = JsonUtility.FromJson<Color>(binaryReader.ReadString());
            objectInfo.lineWidth = binaryReader.ReadSingle();
            objectInfo.emissionColor = JsonUtility.FromJson<Color>(binaryReader.ReadString());
            objectInfo.emissionPower = binaryReader.ReadSingle();
            objectInfo.lightCancel = binaryReader.ReadSingle();
#endif

            //Pattern
            objectInfo.panel._key.Value = binaryReader.ReadInt32();
            objectInfo.panel._filePath.Value = binaryReader.ReadString();
            objectInfo.panel.clamp = binaryReader.ReadBoolean();
            objectInfo.panel.uv = JsonUtility.FromJson<Vector4>(binaryReader.ReadString());
            objectInfo.panel.rot = binaryReader.ReadSingle();

            objectInfo.enableFK = binaryReader.ReadBoolean();
            int num = binaryReader.ReadInt32();
            for (int l = 0; l < num; l++)
            {
                string text = binaryReader.ReadString();
                objectInfo.bones[text] = new OIBoneInfo(Studio.Studio.GetNewIndex());
                binaryReader.ReadInt32();
                objectInfo.bones[text].changeAmount.Load(binaryReader);
            }
            objectInfo.enableDynamicBone = binaryReader.ReadBoolean();
            objectInfo.animeNormalizedTime = binaryReader.ReadSingle();
            ReadObjectInfoChildren(binaryReader, objectInfo.child, ref dicChangeKey);
        }

        public static void ReadObjectInfoChildren(BinaryReader binaryReader, List<ObjectInfo> _list, ref Dictionary<int, int> dicChangeKey)
        {
            int count = binaryReader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                switch (binaryReader.ReadInt32())
                {
                    case 1:
                        {
                            OIItemInfo oiitemInfo = new OIItemInfo(-1, -1, -1, Studio.Studio.GetNewIndex());
                            ReadObjectInfoItem(binaryReader, oiitemInfo, ref dicChangeKey);
                            _list.Add(oiitemInfo);
                            break;
                        }
                    case 3:
                        {
                            OIFolderInfo oifolderInfo = new OIFolderInfo(Studio.Studio.GetNewIndex());
                            ReadObjectInfoFolder(binaryReader, oifolderInfo, ref dicChangeKey);
                            _list.Add(oifolderInfo);
                            break;
                        }
                    case 5:
                        {
                            OICameraInfo oicameraInfo = new OICameraInfo(Studio.Studio.GetNewIndex());
                            ReadObjectInfoCamera(binaryReader, oicameraInfo, ref dicChangeKey);
                            _list.Add(oicameraInfo);
                            break;
                        }
                }
            }
        }

        #endregion
    }
}
