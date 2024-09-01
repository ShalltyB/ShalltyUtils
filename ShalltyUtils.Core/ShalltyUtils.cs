extern alias aliasTimeline;
using aliasTimeline::Timeline;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HSPE;
using HSPE.AMModules;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ShalltyUtils.TimelineBaking;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using ToolBox.Extensions;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ShalltyUtils.GuideObjectPicker;
using Keyframe = Timeline.Keyframe;
using TimelineCompatibility = KKAPI.Utilities.TimelineCompatibility;

#if HS2
using AIChara;
#endif

namespace ShalltyUtils
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess(KK_Plugins.Constants.StudioProcessName)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    [BepInDependency(Timeline.Timeline.GUID, Timeline.Timeline.Version)]
    [BepInDependency(NodesConstraints.NodesConstraints.GUID, NodesConstraints.NodesConstraints.Version)]
    public class ShalltyUtils : BaseUnityPlugin
    {
        #region PLUGIN VARIABLES

        public const string GUID = "com.shallty.shalltyutils";
        public const string PluginName = "Shallty Utils";
#if KK
        public const string PluginNameInternal = "KK_ShalltyUtils";
#elif KKS
        public const string PluginNameInternal = "KKS_ShalltyUtils";
#elif HS2
        public const string PluginNameInternal = "HS2_ShalltyUtils";
#endif
        public const string Version = "1.3";
        public const int _uniqueId = ('S' << 24) | ('U' << 16) | ('T' << 8);
        internal static new ManualLogSource Logger;
        internal Dictionary<Transform, GuideObject> _allGuideObjects;
        internal static ShalltyUtils _self;
        public static GraphEditor _graphEditor;
        internal static NodesConstraints.NodesConstraints _nodeConstraints;
        internal static Timeline.Timeline _timeline;
        internal static MainWindow _kkpeWindow;
        internal const int kkpeGuideObjectDictKey = 169090;

        #endregion CONFIG VARIABLES

        #region CONFIG VARIABLES

        private static ConfigEntry<KeyboardShortcut> renameShortcut;
        private static ConfigEntry<KeyboardShortcut> addKeyframeShortcut;
        private static ConfigEntry<KeyboardShortcut> quickMenuShortcut;

        private static ConfigEntry<Color> timelinePrimaryColor;
        private static ConfigEntry<Color> timelineSecondaryColor;
        private static ConfigEntry<Color> timelineTextColor;
        private static ConfigEntry<Color> timelineText2Color;

        public static ConfigEntry<bool> addReplaceKeyframe;
        private static ConfigEntry<bool> keepTimeline;
        private static ConfigEntry<bool> resetTimelineAfterLoad;
        public static ConfigEntry<bool> enableKKPEGuideObject;
        public static ConfigEntry<bool> undoRedoTimeline;
        public static ConfigEntry<bool> linkedGameObjectTimeline;
        public static ConfigEntry<bool> displayNamesTimeline;
        public static ConfigEntry<bool> displayTooltipsTimeline;

        public static ConfigEntry<bool> addHeigthCompensation;

        public static ConfigEntry<float> keyframesSize;

        public static ConfigEntry<bool> enableGoPicker;
        public static ConfigEntry<int> goPickerGridSize;
        public static ConfigEntry<float> goPickerGridScale;
        public static ConfigEntry<float> goPickerNodeSize;
        public static ConfigEntry<bool> goPickerHideWithAxis;
        public static ConfigEntry<bool> goPickerEnableTooltip;
        public static ConfigEntry<bool> goPickerShowPageNameTooltip;

        public static ConfigEntry<string> goPickerNodeTexture;

        #endregion

        #region NODE CONSTRAINTS WINDOW VARIABLES

        public static bool toggleConstraintsWindow = false;
        public static Rect constraintsWindowRect = new Rect(0f, 0f, 30f, 180f);

        private static bool constraintSphere = false;
        private static bool constraintPos = true;
        private static bool constraintRot = true;
        private static bool constraintScale = false;
        private static bool constraintInverse = false;
        private static bool constraintWithParent = false;

        #endregion

        public static IEnumerable<ObjectCtrlInfo> selectedObjects;
        public static ObjectCtrlInfo firstObject;
        public static OCIItem firstItem;
        public static OCIChar firstChar;

        public static GuideObject kkpeGuideObject;
        public static bool kkpeShowGuideObject = true;

        public static PoseController firstKKPE;
        public static Transform kkpeTargetBone;

        public static Color defColor = GUI.color;
        public static string _defaultDir;
        public static Texture keyframeTexture;


        internal void Awake()
        {
            #region BEPINEX CONFIG

            Logger = base.Logger;
            _self = this;
            _graphEditor = gameObject.AddComponent<GraphEditor>();

            _timeline = Singleton<Timeline.Timeline>.Instance;
            if (_timeline == null) Logger.LogError("Timeline isn't instantiated!");

            _nodeConstraints = Singleton<NodesConstraints.NodesConstraints>.Instance;
            if (_nodeConstraints == null) Logger.LogError("NodeConstraints isn't instantiated!");

            Harmony harmony = Harmony.CreateAndPatchAll(typeof(Hooks));
            harmony.PatchAll(typeof(PerformanceMode.Hooks));

            StudioSaveLoadApi.RegisterExtraBehaviour<ShalltyUtilsSceneData>(GUID);
            SceneManager.sceneLoaded += LoadedEvent;
            StudioSaveLoadApi.ObjectsSelected += OnObjectsSelected;
            StudioSaveLoadApi.ObjectDeleted += OnObjectsSelected;
            StudioSaveLoadApi.SceneLoad += OnSceneLoad;

            _defaultDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ShalltyUtils");
            if (!Directory.Exists(_defaultDir))
                Directory.CreateDirectory(_defaultDir);

            MeshSequencer.cachePath = Path.Combine(_defaultDir, "LastSequence.XML");

            keyframeTexture = CreateKeyframeTexture();

            keyframesSize = Config.Bind("Timeline", "Keyframes Size", 12f, "The size of the Keyframes (in Performance mode only)");

            KeyboardShortcut _quickMenuShortcut = new KeyboardShortcut(KeyCode.I);
            quickMenuShortcut = Config.Bind("Timeline", "Open Quick Menu", _quickMenuShortcut, "Press this key to open the Quick Context Menu.");


            KeyboardShortcut _renameShortcut = new KeyboardShortcut(KeyCode.F2);
            renameShortcut = Config.Bind("Timeline", "Timeline Rename Interpolable", _renameShortcut, "Press this key to rename all selected interpolables in Timeline.");

            KeyboardShortcut _addKeyframeShortcut = new KeyboardShortcut(KeyCode.T);
            addKeyframeShortcut = Config.Bind("Timeline", "Add Keyframes to selected Interpolables", _addKeyframeShortcut, "Press this key to add keyframes to the selected Interpolables at current time.");

            addReplaceKeyframe = Config.Bind("Timeline", "Replace existing keyframes.", true, "When adding a Keyframes with the shortcut replace keyframes in the same time if already exist.");

            timelinePrimaryColor = Config.Bind("Timeline Color", "Primary color", Color.HSVToRGB(0f, 0f, 0.3f));

            timelineSecondaryColor = Config.Bind("Timeline Color", "Secondary color", Color.HSVToRGB(0f, 0f, 0.35f));

            timelineTextColor = Config.Bind("Timeline Color", "Primary Text color", Color.white);

            timelineText2Color = Config.Bind("Timeline Color", "Secondary Text color", Color.white);

            keepTimeline = Config.Bind("Timeline", "Keep timeline", true, "Keep timeline interpolables after creating a folder Constraint.");

            addHeigthCompensation = Config.Bind("Timeline", "Add Height Compensation", true, "Add a constraint to compensate the pose for differents character heights when creating FolderConstraint IK Armatures");

            resetTimelineAfterLoad = Config.Bind("Timeline", "Reset Timeline after load", true, "Reset the Timeline time back to 0 after loading or importing an scene.");

            undoRedoTimeline = Config.Bind("Timeline", "Enable Undo/Redo", true, "Enable Undo/Redo commands for Timeline keyframes.");

            linkedGameObjectTimeline = Config.Bind("Timeline", "Automatically select linked GuideObject", true, "Enable to automatically select linked GuideObject when selecting an interpolable.");

            displayNamesTimeline = Config.Bind("Timeline", "Display Interpolable names when hovering", true, "Enable to display interpolables names in tooltip when hovering.");

            displayTooltipsTimeline = Config.Bind("Timeline", "Display Help Tooltips", true, "Enable to display the Help Tooltips in the buttons added by ShalltyUtils.");



            // KKPE

            enableKKPEGuideObject = Config.Bind("KKPE", "Create bone GuideObject", true, "Create a GuideObject after selecting a bone in the KKPE Advanced Bones Window.");

            // GO PICKER

            enableGoPicker = Config.Bind("GuideObject Picker", "Enable/Disable", true, "Requires restarting studio");

            goPickerGridSize = Config.Bind("GuideObject Picker", "Grid Cell Size", 50, "The size of the grid cells in the GuideObject Picker");

            goPickerGridScale = Config.Bind("GuideObject Picker", "Grid Scale", 0.9f, "The scale of the whole grid in the GuideObject Picker");

            goPickerNodeSize = Config.Bind("GuideObject Picker", "Nodes Size", 20f, "The size of the GuideObject Nodes");
            goPickerNodeSize.SettingChanged += (v, e) => GuideObjectPicker.UpdateAllColors();

            goPickerEnableTooltip = Config.Bind("GuideObject Picker", "Enable Tooltips", true, "Enable Tooltips for GuideObject Nodes");

            goPickerShowPageNameTooltip = Config.Bind("GuideObject Picker", "Display Page in Tooltip", true, "Enable to display the page name in tooltips for GuideObject Nodes");

            goPickerHideWithAxis = Config.Bind("GuideObject Picker", "Link Nodes with Axis Button", true, "Enable to show/hide the GuideObject Nodes with the default studio Axis Button.");

            goPickerNodeTexture = Config.Bind("GuideObject Picker", "Node Texture Path", "", "Add a .PNG image path to change the texture of the nodes in the GuideObject Picker");
            goPickerNodeTexture.SettingChanged += (v, e) => GuideObjectPicker.LoadNodesTexture();

            if (enableGoPicker.Value)
                harmony.PatchAll(typeof(GuideObjectPicker.Hooks));

            #endregion BEPINEX CONFIG

        }

        #region New Interpolables

        private void LoadedEvent(Scene scene, LoadSceneMode loadMode)
        {
            if (loadMode == LoadSceneMode.Single && scene.buildIndex ==
#if KK
                1
#elif HS2 || KKS
                2
#endif
                )
            {
                if (_timeline != null && _timeline._ui != null)
                {
                    UI.Init();
                    _graphEditor.CreateGridWindow();

                    TimelineFirstColor(false);
                    TimelineSecondColor(false);
                    TimelineTextColor(false);
                    TimelineText2Color(false);

                    MotionPath.Init();
                }

                CreateTreeStateButton();

                _allGuideObjects = GuideObjectManager.Instance.dicGuideObject;
                try
                {
                    //Set up the timeline interpolable tool
                    if (TimelineCompatibility.IsTimelineAvailable())
                    {
                        TimelineCompatibility.AddInterpolableModelStatic
                        (
                        owner: "ShalltyUtils",
                        id: "fkEnabled",
                        parameter: null,
                        name: "FK Enabled",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            bool value = (bool)leftValue;
                            OCIChar ociChar = (OCIChar)oci;

                            if (ociChar.fkCtrl.enabled != value)
                            {
                                ociChar.fkCtrl.enabled = value;
                                ociChar.oiCharInfo.enableFK = value;
                                ociChar.ActiveKinematicMode(OICharInfo.KinematicMode.FK, value, true);
                            }
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: (oci) => oci is OCIChar,
                        getValue: (oci, parameter) =>
                        {
                            OCIChar ociChar = (OCIChar)oci;
                            return ociChar.fkCtrl.enabled && ociChar.oiCharInfo.enableFK;
                        },
                        readValueFromXml: (parameter, node) => node.ReadBool("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (bool)o)
                        );

                        TimelineCompatibility.AddInterpolableModelStatic
                        (
                        owner: "ShalltyUtils",
                        id: "ikEnabled",
                        parameter: null,
                        name: "IK Enabled",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            bool value = (bool)leftValue;
                            OCIChar ociChar = (OCIChar)oci;

                            if (ociChar.oiCharInfo.enableIK != value)
                            {
                                ociChar.oiCharInfo.enableIK = value;
                                ociChar.ActiveKinematicMode(OICharInfo.KinematicMode.IK, value, true);
                            }
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: (oci) => oci is OCIChar,
                        getValue: (oci, parameter) =>
                        {
                            OCIChar ociChar = (OCIChar)oci;
                            return ociChar.ikCtrl.enabled && ociChar.oiCharInfo.enableIK;
                        },
                        readValueFromXml: (parameter, node) => node.ReadBool("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (bool)o)
                        );

                        
                        #region NODES CONSTRAINTS

                        /*

                        // OFFSET POSITION

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: NodesConstraints.NodesConstraints.Name,
                        id: "constraintPos",
                        name: "Constraint Position Offset",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).positionOffset = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).positionOffset = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor),
                        isCompatibleWithTarget: oci => _nodeConstraints._selectedConstraint != null,
                        getValue: (oci, parameter) => ((Constraint)parameter).positionOffset,
                        readValueFromXml: (parameter, node) => node.ReadVector3("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (Vector3)o),
                        getParameter: oci => _nodeConstraints._selectedConstraint,
                        readParameterFromXml: (oci, node) =>
                        {
                            int uniqueLoadId = node.ReadInt("parameter");
                            foreach (Constraint c in _nodeConstraints._constraints)
                                if (c.uniqueLoadId != null && c.uniqueLoadId.Value == uniqueLoadId)
                                {
                                    c.uniqueLoadId = null;
                                    return c;
                                }
                            return null;
                        },
                        writeParameterToXml: (oci, writer, parameter) =>
                        {
                            Constraint c = (Constraint)parameter;
                            int uniqueLoadId = _nodeConstraints._constraints.IndexOf(c);
                            if (uniqueLoadId != -1)
                                writer.WriteValue("parameter", uniqueLoadId);
                        },
                        checkIntegrity: (oci, parameter, leftValue, rightValue) =>
                        {
                            if (parameter == null)
                                return false;
                            Constraint c = (Constraint)parameter;
                            if (c.destroyed || c.parentTransform == null || c.childTransform == null)
                                return false;
                            return true;
                        },
                        getFinalName: (name, oci, parameter) =>
                        {
                            if (parameter is Constraint c)
                                return string.IsNullOrEmpty(c.alias) == false ? $"(NC POS): {c.alias}" : $"(NC POS): {c.parentTransform?.name}/{c.childTransform?.name}";
                            return name;
                        }
                        );

                        // OFFSET ROTATION

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: NodesConstraints.NodesConstraints.Name,
                        id: "constraintRot",
                        name: "Constraint Rotation Offset",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).rotationOffset = Quaternion.SlerpUnclamped((Quaternion)leftValue, (Quaternion)rightValue, factor),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).rotationOffset = Quaternion.SlerpUnclamped((Quaternion)leftValue, (Quaternion)rightValue, factor),
                        isCompatibleWithTarget: oci => _nodeConstraints._selectedConstraint != null,
                        getValue: (oci, parameter) => ((Constraint)parameter).rotationOffset,
                        readValueFromXml: (parameter, node) => node.ReadQuaternion("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (Quaternion)o),
                        getParameter: oci => _nodeConstraints._selectedConstraint,
                        readParameterFromXml: (oci, node) =>
                        {
                            int uniqueLoadId = node.ReadInt("parameter");
                            foreach (Constraint c in _nodeConstraints._constraints)
                                if (c.uniqueLoadId != null && c.uniqueLoadId.Value == uniqueLoadId)
                                {
                                    c.uniqueLoadId = null;
                                    return c;
                                }
                            return null;
                        },
                        writeParameterToXml: (oci, writer, parameter) =>
                        {
                            Constraint c = (Constraint)parameter;
                            int uniqueLoadId = _nodeConstraints._constraints.IndexOf(c);
                            if (uniqueLoadId != -1)
                                writer.WriteValue("parameter", uniqueLoadId);
                        },
                        checkIntegrity: (oci, parameter, leftValue, rightValue) =>
                        {
                            if (parameter == null)
                                return false;
                            Constraint c = (Constraint)parameter;
                            if (c.destroyed || c.parentTransform == null || c.childTransform == null)
                                return false;
                            return true;
                        },
                        getFinalName: (name, oci, parameter) =>
                        {
                            if (parameter is Constraint c)
                                return string.IsNullOrEmpty(c.alias) == false ? $"(NC ROT): {c.alias}" : $"(NC ROT): {c.parentTransform?.name}/{c.childTransform?.name}";
                            return name;
                        }
                        );

                        // OFFSET SCALE

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: NodesConstraints.NodesConstraints.Name,
                        id: "constraintScale",
                        name: "Constraint Scale Offset",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).scaleOffset = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((Constraint)parameter).scaleOffset = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor),
                        isCompatibleWithTarget: oci => _nodeConstraints._selectedConstraint != null,
                        getValue: (oci, parameter) => ((Constraint)parameter).scaleOffset,
                        readValueFromXml: (parameter, node) => node.ReadVector3("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (Vector3)o),
                        getParameter: oci => _nodeConstraints._selectedConstraint,
                        readParameterFromXml: (oci, node) =>
                        {
                            int uniqueLoadId = node.ReadInt("parameter");
                            foreach (Constraint c in _nodeConstraints._constraints)
                                if (c.uniqueLoadId != null && c.uniqueLoadId.Value == uniqueLoadId)
                                {
                                    c.uniqueLoadId = null;
                                    return c;
                                }
                            return null;
                        },
                        writeParameterToXml: (oci, writer, parameter) =>
                        {
                            Constraint c = (Constraint)parameter;
                            int uniqueLoadId = _nodeConstraints._constraints.IndexOf(c);
                            if (uniqueLoadId != -1)
                                writer.WriteValue("parameter", uniqueLoadId);
                        },
                        checkIntegrity: (oci, parameter, leftValue, rightValue) =>
                        {
                            if (parameter == null)
                                return false;
                            Constraint c = (Constraint)parameter;
                            if (c.destroyed || c.parentTransform == null || c.childTransform == null)
                                return false;
                            return true;
                        },
                        getFinalName: (name, oci, parameter) =>
                        {
                            if (parameter is Constraint c)
                                return string.IsNullOrEmpty(c.alias) == false ? $"(NC SCALE): {c.alias}" : $"(NC SCALE): {c.parentTransform?.name}/{c.childTransform?.name}";
                            return name;
                        }
                        );

                        */
                        #endregion NODES CONSTRAINTS
                        
                        #region GuideObjects

                        #region POSITION

                        // X Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectXPos",
                        name: "(GO) X Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.pos.y, ((GuideObject)parameter).changeAmount.pos.z),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.pos.y, ((GuideObject)parameter).changeAmount.pos.z),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.pos.x,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) X Pos - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        // Y Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectYPos",
                        name: "(GO) Y Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(((GuideObject)parameter).changeAmount.pos.x, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.pos.z),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(((GuideObject)parameter).changeAmount.pos.x, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.pos.z),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.pos.y,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Y Pos - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        // Z Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectZPos",
                        name: "(GO) Z Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(((GuideObject)parameter).changeAmount.pos.x, ((GuideObject)parameter).changeAmount.pos.y, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor)),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.pos = new Vector3(((GuideObject)parameter).changeAmount.pos.x, ((GuideObject)parameter).changeAmount.pos.y, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor)),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.pos.z,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Z Pos - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        #endregion POSITION

                        #region ROTATION

                        // X Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectXRot",
                        name: "(GO) X Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startXRotation = Quaternion.Euler((float)leftValue, currentRotation.y, currentRotation.z);
                            var endXRotation = Quaternion.Euler((float)rightValue, currentRotation.y, currentRotation.z);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startXRotation, endXRotation, factor).eulerAngles;

                        },
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startXRotation = Quaternion.Euler((float)leftValue, currentRotation.y, currentRotation.z);
                            var endXRotation = Quaternion.Euler((float)rightValue, currentRotation.y, currentRotation.z);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startXRotation, endXRotation, factor).eulerAngles;
                        },
                        isCompatibleWithTarget: (oci) => oci != null,
                        getValue: (oci, parameter) => (((GuideObject)parameter).changeAmount.rot.x),
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) X Rot - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        // Y Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectYRot",
                        name: "(GO) Y Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startRotation = Quaternion.Euler(currentRotation.x, (float)leftValue, currentRotation.z);
                            var endRotation = Quaternion.Euler(currentRotation.x, (float)rightValue, currentRotation.z);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startRotation, endRotation, factor).eulerAngles;
                        },
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startRotation = Quaternion.Euler(currentRotation.x, (float)leftValue, currentRotation.z);
                            var endRotation = Quaternion.Euler(currentRotation.x, (float)rightValue, currentRotation.z);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startRotation, endRotation, factor).eulerAngles;
                        },
                        isCompatibleWithTarget: (oci) => oci != null,
                        getValue: (oci, parameter) => (((GuideObject)parameter).changeAmount.rot.y),
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Y Rot - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        // Z Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectZRot",
                        name: "(GO) Z Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startZRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)leftValue);
                            var endZRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)rightValue);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startZRotation, endZRotation, factor).eulerAngles;

                        },
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            var currentRotation = ((GuideObject)parameter).changeAmount.rot;
                            var startZRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)leftValue);
                            var endZRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)rightValue);
                            ((GuideObject)parameter).changeAmount.rot = Quaternion.SlerpUnclamped(startZRotation, endZRotation, factor).eulerAngles;
                        },
                        isCompatibleWithTarget: (oci) => oci != null,
                        getValue: (oci, parameter) => (((GuideObject)parameter).changeAmount.rot.z),
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Z Rot - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        #endregion ROTATION

                        #region SCALE

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectXScale",
                        name: "(GO) X Scale",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.scale.y, ((GuideObject)parameter).changeAmount.scale.z),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.scale.y, ((GuideObject)parameter).changeAmount.scale.z),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.scale.x,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) X Scale - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectYScale",
                        name: "(GO) Y Scale",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(((GuideObject)parameter).changeAmount.scale.x, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.scale.z),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(((GuideObject)parameter).changeAmount.scale.x, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor), ((GuideObject)parameter).changeAmount.scale.z),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.scale.y,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Y Scale - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "guideObjectZScale",
                        name: "(GO) Z Scale",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(((GuideObject)parameter).changeAmount.scale.x, ((GuideObject)parameter).changeAmount.scale.y, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor)),
                        interpolateAfter: (oci, parameter, leftValue, rightValue, factor) => ((GuideObject)parameter).changeAmount.scale = new Vector3(((GuideObject)parameter).changeAmount.scale.x, ((GuideObject)parameter).changeAmount.scale.y, Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor)),
                        isCompatibleWithTarget: oci => oci != null,
                        getValue: (oci, parameter) => ((GuideObject)parameter).changeAmount.scale.z,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: oci => GuideObjectManager.Instance.selectObject,
                        readParameterFromXml: (oci, node) =>
                        {
                            Transform t = oci.guideObject.transformTarget.Find(node.Attributes["guideObjectPath"].Value);
                            if (t == null)
                                return null;
                            GuideObject guideObject;
                            _allGuideObjects.TryGetValue(t, out guideObject);
                            return guideObject;
                        },
                        writeParameterToXml: (oci, writer, o) => writer.WriteAttributeString("guideObjectPath", ((GuideObject)o).transformTarget.GetPathFrom(oci.guideObject.transformTarget)),
                        checkIntegrity: (oci, parameter, leftValue, rightValue) => parameter != null,
                        getFinalName: (name, oci, parameter) => $"(GO) Z Scale - ({((GuideObject)parameter).transformTarget.name})"
                        );

                        #endregion

                        #endregion GuideObjects

                        #region KKPE Bones

                        #region POSITION

                        // X Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneXPos",
                        name: "(KKPE) X Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                            Vector3 oldPos = pair.key.GetBonePosition(pair.value);

                            pair.key.SetBonePosition(pair.value, new Vector3(value, oldPos.y, oldPos.z));
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localPosition.x,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) X Pos ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        // Y Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneYPos",
                        name: "(KKPE) Y Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                            Vector3 oldPos = pair.key.GetBonePosition(pair.value);

                            pair.key.SetBonePosition(pair.value, new Vector3(oldPos.x, value, oldPos.z));
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localPosition.y,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) Y Pos ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        // Z Position

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneZPos",
                        name: "(KKPE) Z Position",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                            Vector3 oldPos = pair.key.GetBonePosition(pair.value);

                            pair.key.SetBonePosition(pair.value, new Vector3(oldPos.x, oldPos.y, value));
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localPosition.z,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) Z Pos ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        #endregion POSITION

                        #region ROTATION

                        // X Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneXRot",
                        name: "(KKPE) X Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            var currentRotation = ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation;
                            var startRotation = Quaternion.Euler((float)leftValue, currentRotation.y, currentRotation.z);
                            var endRotation = Quaternion.Euler((float)rightValue, currentRotation.y, currentRotation.z);

                            pair.key.SetBoneRotation(pair.value, Quaternion.SlerpUnclamped(startRotation, endRotation, factor));
                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation.x,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) X Rot ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                         );

                        // Y Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneYRot",
                        name: "(KKPE) Y Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            var currentRotation = ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation;
                            var startRotation = Quaternion.Euler(currentRotation.x, (float)leftValue, currentRotation.z);
                            var endRotation = Quaternion.Euler(currentRotation.x, (float)rightValue, currentRotation.z);

                            pair.key.SetBoneRotation(pair.value, Quaternion.SlerpUnclamped(startRotation, endRotation, factor));

                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation.y,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) Y Rot ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                         );

                        // Z Rotation

                        TimelineCompatibility.AddInterpolableModelDynamic(
                        owner: "ShalltyUtils",
                        id: "boneZRot",
                        name: "(KKPE) Z Rotation",
                        interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                        {
                            HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                            var currentRotation = ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation;
                            var startRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)leftValue);
                            var endRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, (float)rightValue);

                            pair.key.SetBoneRotation(pair.value, Quaternion.SlerpUnclamped(startRotation, endRotation, factor));

                        },
                        interpolateAfter: null,
                        isCompatibleWithTarget: IsCompatibleWithTarget,
                        getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localRotation.z,
                        readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                        writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                        getParameter: GetParameter,
                        readParameterFromXml: ReadParameterFromXml,
                        writeParameterToXml: WriteParameterToXml,
                        getFinalName: (name, oci, parameter) => $"(KKPE) Z Rot ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        #endregion ROTATION

                        #region SCALE

                        // X Scale

                        TimelineCompatibility.AddInterpolableModelDynamic(
                            owner: "ShalltyUtils",
                            id: "boneXScale",
                            name: "(KKPE) X Scale",
                            interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                            {
                                HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                                float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                                Vector3 oldScale = pair.key.GetBoneScale(pair.value);

                                pair.key.SetBoneScale(pair.value, new Vector3(value, oldScale.y, oldScale.z));
                            },
                            interpolateAfter: null,
                            isCompatibleWithTarget: IsCompatibleWithTarget,
                            getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localScale.x,
                            readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                            writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                            getParameter: GetParameter,
                            readParameterFromXml: ReadParameterFromXml,
                            writeParameterToXml: WriteParameterToXml,
                            getFinalName: (name, oci, parameter) => $"(KKPE) X Scale ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        // Y Scale

                        TimelineCompatibility.AddInterpolableModelDynamic(
                            owner: "ShalltyUtils",
                            id: "boneYScale",
                            name: "(KKPE) Y Scale",
                            interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                            {
                                HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                                float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                                Vector3 oldScale = pair.key.GetBoneScale(pair.value);

                                pair.key.SetBoneScale(pair.value, new Vector3(oldScale.x, value, oldScale.z));
                            },
                            interpolateAfter: null,
                            isCompatibleWithTarget: IsCompatibleWithTarget,
                            getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localScale.y,
                            readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                            writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                            getParameter: GetParameter,
                            readParameterFromXml: ReadParameterFromXml,
                            writeParameterToXml: WriteParameterToXml,
                            getFinalName: (name, oci, parameter) => $"(KKPE) Y Scale ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );

                        // Z Scale

                        TimelineCompatibility.AddInterpolableModelDynamic(
                            owner: "ShalltyUtils",
                            id: "boneZScale",
                            name: "(KKPE) Z Scale",
                            interpolateBefore: (oci, parameter, leftValue, rightValue, factor) =>
                            {
                                HashedPair<BonesEditor, Transform> pair = (HashedPair<BonesEditor, Transform>)parameter;
                                float value = Mathf.LerpUnclamped((float)leftValue, (float)rightValue, factor);
                                Vector3 oldScale = pair.key.GetBoneScale(pair.value);

                                pair.key.SetBoneScale(pair.value, new Vector3(oldScale.x, oldScale.y, value));
                            },
                            interpolateAfter: null,
                            isCompatibleWithTarget: IsCompatibleWithTarget,
                            getValue: (oci, parameter) => ((HashedPair<BonesEditor, Transform>)parameter).value.localScale.z,
                            readValueFromXml: (parameter, node) => node.ReadFloat("value"),
                            writeValueToXml: (parameter, writer, o) => writer.WriteValue("value", (float)o),
                            getParameter: GetParameter,
                            readParameterFromXml: ReadParameterFromXml,
                            writeParameterToXml: WriteParameterToXml,
                            getFinalName: (name, oci, parameter) => $"(KKPE) Z Scale ({((HashedPair<BonesEditor, Transform>)parameter).value.name})"
                        );


                        #endregion



                        #endregion KKPE Bones
                    }
                }
                catch { }
            }
        }

        private static bool IsCompatibleWithTarget(ObjectCtrlInfo oci)
        {
            return oci != null && oci.guideObject != null && oci.guideObject.transformTarget != null && oci.guideObject.transformTarget.GetComponent<PoseController>() != null;
        }

        public static object GetParameter(ObjectCtrlInfo oci)
        {
            PoseController controller = oci.guideObject.transformTarget.GetComponent<PoseController>();
            return new HashedPair<BonesEditor, Transform>(controller._bonesEditor, controller._bonesEditor._boneTarget);
        }

        private static object ReadParameterFromXml(ObjectCtrlInfo oci, XmlNode node)
        {
            PoseController controller = oci.guideObject.transformTarget.GetComponent<PoseController>();
            return new HashedPair<BonesEditor, Transform>(controller._bonesEditor, controller.transform.Find(node.Attributes["parameter"].Value));
        }

        private static void WriteParameterToXml(ObjectCtrlInfo oci, XmlTextWriter writer, object parameter)
        {
            writer.WriteAttributeString("parameter", ((HashedPair<BonesEditor, Transform>)parameter).value.GetPathFrom(oci.guideObject.transformTarget));
        }

        #endregion

        internal void Update()
        {
            PerformanceMode.UpdateKeyframes();
            MotionPath.UpdateMotionPath();
            GuideObjectPicker.Update();

            if (quickMenuShortcut.Value.IsDown())
                QuickMenu.showQuickMenuUI = !QuickMenu.showQuickMenuUI;

            if (_timeline._ui == null || _timeline._ui.gameObject == null || !_timeline._ui.gameObject.activeSelf) return;

            if (renameShortcut.Value.IsDown())
                RenameSelectedInterpolables();

            if (addKeyframeShortcut.Value.IsDown())
            {
                if (_timeline._selectedInterpolables.Count <= 0) return;

                foreach (Interpolable interpolable in _timeline._selectedInterpolables)
                {
                    float time = _timeline._playbackTime;
                    if (!interpolable.keyframes.ContainsKey(time))
                    {
                        _timeline.AddKeyframe(interpolable, time);
                    }
                    else if (addReplaceKeyframe.Value)
                    {
                        _timeline.DeleteKeyframes(new List<KeyValuePair<float, Keyframe>> { new KeyValuePair<float, Keyframe>(time, interpolable.keyframes[time]) }, false);
                        _timeline.AddKeyframe(interpolable, time);
                    }
                }
            }
        }

        internal void OnGUI()
        {
            var skin = GUI.skin;
            GUI.skin = IMGUIUtils.SolidBackgroundGuiSkin;

            GuideObjectPicker.DrawSelectionRect();

            if (_nodeConstraints._showUI)
            {
                constraintsWindowRect.position = new Vector2(_nodeConstraints._windowRect.x + _nodeConstraints._windowRect.width, _nodeConstraints._windowRect.y);
                constraintsWindowRect = GUILayout.Window(_uniqueId, constraintsWindowRect, ConstraintsWindow, PluginName + "  " + Version);
                IMGUIUtils.EatInputInRect(constraintsWindowRect);
            }
            
            if (MeshSequencer.showMeshSequencerUI)
            {
                MeshSequencer.windowMeshSequencerRect = GUILayout.Window(_uniqueId + 10, MeshSequencer.windowMeshSequencerRect, MeshSequencer.Window, PluginName + ": Mesh Sequencer"); ;
                IMGUIUtils.EatInputInRect(MeshSequencer.windowMeshSequencerRect);
            }

            if (QuickMenu.showQuickMenuUI)
            {
                QuickMenu.quickMenuRect = GUILayout.Window(_uniqueId + 20, QuickMenu.quickMenuRect, QuickMenu.Window, PluginName + ": Quick Menu"); ;
            }

            if (BakeCustom.showCustomBakeUI)
            {
                BakeCustom.customBakeRect = GUILayout.Window(_uniqueId + 30, BakeCustom.customBakeRect, BakeCustom.Window, PluginName + ": Custom Bake"); ;
            }

            if (FolderConstraintsRig.showFoldersConstraintsRigsUI)
            {
                FolderConstraintsRig.windowFoldersConstraintsRigsRect = GUILayout.Window(_uniqueId + 40, FolderConstraintsRig.windowFoldersConstraintsRigsRect, FolderConstraintsRig.Window, PluginName + ": Folders Constraints Rigs");
            }

            if (KeyframesGroups.showKeyframeGroupsUI)
            {
                KeyframesGroups.windowKeyframeGroupsRect = GUILayout.Window(_uniqueId + 50, KeyframesGroups.windowKeyframeGroupsRect, KeyframesGroups.Window, PluginName + ": Keyframe Groups"); ;
            }

            GUI.skin = skin;
        }

        private static void ConstraintsWindow(int WindowID)
        {
            GUI.color = defColor;
            GUILayout.BeginHorizontal();
            {
                GUI.enabled = true;
                GUILayout.BeginVertical();
                if (GUILayout.Button(toggleConstraintsWindow ? "►" : "◄", GUILayout.ExpandHeight(true), GUILayout.Width(20f)))
                {
                    toggleConstraintsWindow = !toggleConstraintsWindow;
                    constraintsWindowRect.size = toggleConstraintsWindow ? new Vector2(300f, constraintsWindowRect.size.y) : new Vector2(30f, constraintsWindowRect.size.y);
                }
                GUILayout.EndVertical();


                GUILayout.BeginVertical();

                if (toggleConstraintsWindow)
                {
                    // __________________________________________________________________________________________________ //

                    GUI.enabled = true;
                    if (GUILayout.Button($"Create FolderConstraints to IK Bones"))
                    {
                        if (firstChar == null)
                        {
                            Logger.LogMessage("First select a Character in the Workspace!");
                            return;
                        }

                        TreeNodeObject parent = firstChar.treeNodeObject.parent;
                        if (parent != null)
                            Singleton<TreeNodeCtrl>.Instance.SetParent(firstChar.treeNodeObject, null);

                        ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                        string charaName = KKAPI.Chara.CharacterExtensions.GetFancyCharacterName(chaCtrl.chaFile);
                        string armatureName = $"{charaName} | IK Armature";

                        ObjectCtrlInfo armature;
                        if (addHeigthCompensation.Value)
                        {
                            List<Transform> boneList = GetAllChildTransforms(firstChar.guideObject.transformTarget);

                            bool oldKeepTimeline = keepTimeline.Value;
                            keepTimeline.Value = false;
                            armature = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_n_height"), false, false, true, true, true);
                            keepTimeline.Value = oldKeepTimeline;
                            Singleton<TreeNodeCtrl>.Instance.DeleteNode(armature.treeNodeObject.child[0]);
                            armature.treeNodeObject.textName = armatureName;
                        }
                        else
                        {
                            armature = AddObjectFolder.Add();
                            ((OCIFolder)armature).name = armatureName;
                            armature.guideObject.transformTarget.name = armatureName;
                        }

                        armature.guideObject.changeAmount.Copy(firstChar.guideObject.changeAmount);

                        /* ADD HEAD AND NECK TO THE ARMATURE?
                        List<string> transformNames = new List<string> { "cf_j_head", "cf_j_neck" };

                        List<GuideObject> guideObjectFK = firstChar.listBones.Where(targetInfo => transformNames.Contains(targetInfo.guideObject.transformTarget.name)).Select(targetInfo => targetInfo.guideObject).ToList();

                        foreach (GuideObject guideObject in guideObjectFK)
                        {
                            ObjectCtrlInfo constraint = CreateConstraint(guideObject.transformTarget, false, true, false, false);
                            Singleton<TreeNodeCtrl>.Instance.SetParent(constraint.treeNodeObject, folder.treeNodeObject);
                        }*/

                        List<ObjectCtrlInfo> newConstraints = new List<ObjectCtrlInfo>();

                        foreach (OCIChar.IKInfo ik in firstChar.listIKTarget)
                        {           
                            ObjectCtrlInfo constraint = CreateConstraint(ik.targetObject, true, true, false, false);
                            Singleton<TreeNodeCtrl>.Instance.SetParent(constraint.treeNodeObject, armature.treeNodeObject);

                            newConstraints.Add(constraint);
                        }

                        if (parent != null)
                        {
                            Singleton<TreeNodeCtrl>.Instance.SetParent(firstChar.treeNodeObject, parent);
                            Singleton<TreeNodeCtrl>.Instance.SetParent(armature.treeNodeObject, parent);
                        }

                        armature.treeNodeObject.Select();

                        List<Interpolable> allInterpolables = _timeline._interpolables.Values.Where(interpolable => newConstraints.Any(oci => ReferenceEquals(oci, interpolable.oci))).ToList();

                        Color groupColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
                        if (allInterpolables.Count > 0)
                        {
                            foreach (Interpolable interpolable in allInterpolables)
                                interpolable.color = groupColor;


                            _timeline._interpolablesTree.GroupTogether(allInterpolables, new Timeline.Timeline.InterpolableGroup() { name = armatureName });
                        }

                        var ocis = Studio.Studio.Instance.dicInfo;

                        List<GuideObject> animGuideObjects = newConstraints.Select(oci => oci.guideObject).ToList();

                        List<GuideObject> constraintGuideObjects = new List<GuideObject>();
                        foreach (var oci in newConstraints)
                        {
                            if (oci.treeNodeObject != null && oci.treeNodeObject.child.Count > 0)
                            {
                                if (ocis.TryGetValue(oci.treeNodeObject.child[0], out ObjectCtrlInfo constraint))
                                {
                                    constraintGuideObjects.Add(constraint.guideObject);
                                }
                            }
                        }

                        Color[] buttonColors = { Color.magenta,  Color.red, Color.blue, Color.red, Color.red, Color.blue, Color.red, Color.red, Color.blue, Color.red, Color.red, Color.blue, Color.red };

                        List<PickerButton> animButtons = new List<PickerButton>();
                        List<PickerButton> constraintsButtons = new List<PickerButton>();

                        List<KeyValuePair<Vector2, Vector2>> anchorOffsets = new List<KeyValuePair<Vector2, Vector2>>
                        {
                            new KeyValuePair<Vector2, Vector2>(new Vector2(270.1357f, 1759.771f), new Vector2(370.1357f, 1809.771f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(352f, 1858.5f), new Vector2(452f, 1908.5f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(429f, 1809f), new Vector2(529f, 1859f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(457.6542f, 1762.008f), new Vector2(557.6542f, 1812.008f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(170.1357f, 1859.771f), new Vector2(270.1357f, 1909.771f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(122.4637f, 1814.104f), new Vector2(222.4637f, 1864.104f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(77.36797f, 1753.761f), new Vector2(177.368f, 1803.761f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(349.254f, 1657.124f), new Vector2(449.254f, 1707.124f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(379.254f, 1607.124f), new Vector2(479.254f, 1657.124f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(399.254f, 1557.124f), new Vector2(499.254f, 1607.124f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(199.2539f, 1657.124f), new Vector2(299.2539f, 1707.124f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(174.2539f, 1607.124f), new Vector2(274.2539f, 1657.124f)),
                            new KeyValuePair<Vector2, Vector2>(new Vector2(149.2539f, 1557.124f), new Vector2(249.2539f, 1607.124f))
                        };


                        for (int i = 0; i < animGuideObjects.Count; i++)
                        {
                            GuideObject guideObject = animGuideObjects[i];
                            GuideObject constraintGuideObject = constraintGuideObjects[i];

                            string pickerName = guideObject.transformTarget.name;
                            string constraintName = constraintGuideObject.transformTarget.name;

                            TreeNodeObject treeNode = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, guideObject)).Select(pair => pair.Key).FirstOrDefault();
                            if (treeNode != null)
                                pickerName = treeNode.textName;

                            TreeNodeObject constraintTreeNode = Singleton<Studio.Studio>.Instance.dicInfo.Where(pair => ReferenceEquals(pair.Value.guideObject, constraintGuideObject)).Select(pair => pair.Key).FirstOrDefault();
                            if (constraintTreeNode != null)
                                constraintName = constraintTreeNode.textName;

                            // Calculate anchorMin, anchorMax, offsetMin, and offsetMax
                            Vector2 anchorMin = new Vector2(0f, 0f);
                            Vector2 anchorMax = new Vector2(0f, 0f);
                            Vector2 offsetMin = anchorOffsets[i].Key;
                            Vector2 offsetMax = anchorOffsets[i].Value;

                            // Create the PickerButton and add it to the list
                            animButtons.Add(new PickerButton(pickerName, buttonColors[i], anchorMin, anchorMax, offsetMin, offsetMax, guideObject));
                            constraintsButtons.Add(new PickerButton(constraintName, buttonColors[i], anchorMin, anchorMax, offsetMin, offsetMax, constraintGuideObject));

                        }

                        PickerPage animPage = new PickerPage($"{charaName} | Animation", groupColor, animButtons, true);
                        goPickerPages.Add(animPage);

                        PickerPage constraintsPage = new PickerPage($"{charaName} | Constraints", UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f), constraintsButtons, false);
                        goPickerPages.Add(constraintsPage);

                        animPage.SelectPage();
                        UpdatePickerPages();
                    }

                    GUI.enabled = true;
                    if (GUILayout.Button($"Create FolderConstraints to Misc Bones"))
                    {
                        if (firstChar == null)
                        {
                            Logger.LogMessage("First select a Character in the Workspace!");
                            return;
                        }
                        TreeNodeObject parent = firstChar.treeNodeObject.parent;
                        if (parent != null)
                            Singleton<TreeNodeCtrl>.Instance.SetParent(firstChar.treeNodeObject, null);

                        OCIFolder folder = AddObjectFolder.Add();
                        folder.guideObject.changeAmount.Copy(firstChar.guideObject.changeAmount);

                        ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                        folder.name = $"{KKAPI.Chara.CharacterExtensions.GetFancyCharacterName(chaCtrl.chaFile)} | Misc Bones";
                        folder.guideObject.transformTarget.name = folder.name;

                        List<Transform> boneList = GetAllChildTransforms(firstChar.guideObject.transformTarget);

                        // BUST

                        ObjectCtrlInfo bustFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_bust00"), true, true, true, true, true);

                        ObjectCtrlInfo bustRFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_bust01_R"), true, true, true, true, false, "BUST R");
                        ObjectCtrlInfo bustLFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_bust01_L"), true, true, true, true, false, "BUST L");

                        Singleton<TreeNodeCtrl>.Instance.SetParent(bustRFolder.treeNodeObject, bustFolder.treeNodeObject.child[0]);
                        Singleton<TreeNodeCtrl>.Instance.SetParent(bustLFolder.treeNodeObject, bustFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(bustFolder.treeNodeObject, folder.treeNodeObject);

                        // SPINE

                        ObjectCtrlInfo spineFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_j_spine01"), true, true, false, false, true);

                        ObjectCtrlInfo spine01Folder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_s_spine01"), true, true, false, true);
                        ObjectCtrlInfo spine02Folder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_s_spine02"), true, true, false, true);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(spine01Folder.treeNodeObject, spineFolder.treeNodeObject.child[0]);
                        Singleton<TreeNodeCtrl>.Instance.SetParent(spine02Folder.treeNodeObject, spineFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(spineFolder.treeNodeObject, folder.treeNodeObject);

                        // SIRI

                        ObjectCtrlInfo siriFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_j_waist02"), true, true, true, false, true, "SIRI");

                        ObjectCtrlInfo siriRFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_siri01_R"), true, true, true, true, false, "SIRI R");
                        ObjectCtrlInfo siriLFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_siri01_L"), true, true, true, true, false, "SIRI L");

                        Singleton<TreeNodeCtrl>.Instance.SetParent(siriRFolder.treeNodeObject, siriFolder.treeNodeObject.child[0]);
                        Singleton<TreeNodeCtrl>.Instance.SetParent(siriLFolder.treeNodeObject, siriFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(siriFolder.treeNodeObject, folder.treeNodeObject);

                        // WAIST

                        ObjectCtrlInfo waistParentFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_j_waist02"), true, true, false, false, true, "WAIST");

                        ObjectCtrlInfo waistFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_s_waist02"), true, true, false, false, false, "WAIST");

                        Singleton<TreeNodeCtrl>.Instance.SetParent(waistFolder.treeNodeObject, waistParentFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(waistParentFolder.treeNodeObject, folder.treeNodeObject);

                        // LEFT THIGH

                        ObjectCtrlInfo thighLFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_j_thigh00_L"), true, true, true, false, true);

                        ObjectCtrlInfo thigh01LFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_s_thigh01_L"), true, true, true, true);
                        ObjectCtrlInfo thigh02LFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_thigh02_L"), true, true, true, true);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(thigh01LFolder.treeNodeObject, thighLFolder.treeNodeObject.child[0]);
                        Singleton<TreeNodeCtrl>.Instance.SetParent(thigh02LFolder.treeNodeObject, thighLFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(thighLFolder.treeNodeObject, folder.treeNodeObject);

                        // RIGHT THIGH

                        ObjectCtrlInfo thighRFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_j_thigh00_R"), true, true, true, false, true);

                        ObjectCtrlInfo thigh01RFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_s_thigh01_R"), true, true, true, true);
                        ObjectCtrlInfo thigh02RFolder = CreateConstraint(boneList.FirstOrDefault(bone => bone.name == "cf_d_thigh02_R"), true, true, true, true);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(thigh01RFolder.treeNodeObject, thighRFolder.treeNodeObject.child[0]);
                        Singleton<TreeNodeCtrl>.Instance.SetParent(thigh02RFolder.treeNodeObject, thighRFolder.treeNodeObject.child[0]);

                        Singleton<TreeNodeCtrl>.Instance.SetParent(thighRFolder.treeNodeObject, folder.treeNodeObject);

                        if (parent != null)
                        {
                            Singleton<TreeNodeCtrl>.Instance.SetParent(firstChar.treeNodeObject, parent);
                            Singleton<TreeNodeCtrl>.Instance.SetParent(folder.treeNodeObject, parent);
                        }

                        folder.treeNodeObject.Select();
                    }


                    GUILayout.Space(10f);

                    GUI.enabled = true;

                    string selectedItem = "(Nothing)";
                    if (selectedObjects != null)
                    {
                        if (selectedObjects.Count() > 1)
                        {
                            selectedItem = $"All Selected ({selectedObjects.Count()})";
                        }
                        else if (selectedObjects.Count() == 1)
                        {
                            selectedItem = selectedObjects.FirstOrDefault().treeNodeObject.textName;
                        }
                    }

                    if (GUILayout.Button($"Create Parent Folder to: " + selectedItem))
                    {
                        if (firstObject == null)
                        {
                            Logger.LogMessage("First select an Item in the Workspace!");
                            return;
                        }

                        OCIFolder folder = CreateParentFolder(new List<ObjectCtrlInfo>(selectedObjects), "Parent", true, true);
                    }

                    GUILayout.Space(10f);

                    GUILayout.BeginVertical(GUI.skin.box);

                    GUI.enabled = true;
                    if (GUILayout.Button($"Create FolderConstraint to: {(_nodeConstraints._selectedBone != null ? _nodeConstraints._selectedBone.name : "(Nothing)")}"))
                    {
                        if (_nodeConstraints._selectedBone == null)
                        {
                            Logger.LogMessage("First select a GuideObject in the Workspace, or a Bone in the NodeConstraints window!");
                            return;
                        }
   
                        ObjectCtrlInfo folder = CreateConstraint(_nodeConstraints._selectedBone, constraintPos, constraintRot, constraintScale, constraintSphere, constraintInverse, "", false);

                        if (constraintWithParent && _nodeConstraints._selectedBone.parent != null)
                        {
                            ObjectCtrlInfo parentFolder = CreateConstraint(_nodeConstraints._selectedBone.parent, true, true, true, false, true);

                            Singleton<TreeNodeCtrl>.Instance.SetParent(folder.treeNodeObject, parentFolder.treeNodeObject.child[0]);
                        }
                    }

                    GUILayout.Space(5f);

                    if (GUILayout.Button($"Child type: {(constraintSphere ? "Sphere" : "Folder")}"))
                    {
                        constraintSphere = !constraintSphere;
                        if (!constraintSphere) constraintScale = false;
                    }

                    GUILayout.BeginHorizontal();
                    constraintPos = GUILayout.Toggle(constraintPos, "Position.");
                    constraintRot = GUILayout.Toggle(constraintRot, "Rotation.");
                    GUI.enabled = constraintSphere;
                    constraintScale = GUILayout.Toggle(constraintScale, "Scale.");
                    GUILayout.EndHorizontal();

                    GUI.enabled = true;
                    GUILayout.BeginHorizontal();
                    constraintInverse = GUILayout.Toggle(constraintInverse, "Reversed FolderConstraint.");

                    GUI.enabled = !constraintInverse;
                    constraintWithParent = GUILayout.Toggle(constraintWithParent, "Create with Parent.");
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                   
                    GUI.enabled = true;
                }

                GUI.enabled = true;

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        public static string FancyBoneName(string name)
        {
            switch (name)
            {
                case "cf_t_hips(work)":
                    name = "HIPS";
                    break;

                case "cf_t_shoulder_L(work)":
                    name = "Left Shoulder";
                    break;

                case "cf_t_elbo_L(work)":
                    name = "Left Elbow";
                    break;

                case "cf_t_hand_L(work)":
                    name = "Left Hand";
                    break;

                case "cf_t_shoulder_R(work)":
                    name = "Right Shoulder";
                    break;

                case "cf_t_elbo_R(work)":
                    name = "Right Elbow";
                    break;

                case "cf_t_hand_R(work)":
                    name = "Right Hand";
                    break;

                case "cf_t_waist_L(work)":
                    name = "Left Thigh";
                    break;

                case "cf_t_knee_L(work)":
                    name = "Left Knee";
                    break;

                case "cf_t_leg_L(work)":
                    name = "Left Foot";
                    break;

                case "cf_t_waist_R(work)":
                    name = "Right Thigh";
                    break;

                case "cf_t_knee_R(work)":
                    name = "Right Knee";
                    break;

                case "cf_t_leg_R(work)":
                    name = "Right Foot";
                    break;

                case "cf_j_head":
                    name = "Head";
                    break;

                case "cf_j_neck":
                    name = "Neck";
                    break;

                case "cf_j_thumb01_R":
                    name = "Right Thumb 1";
                    break;

                case "cf_j_thumb02_R":
                    name = "Right Thumb 2";
                    break;

                case "cf_j_thumb03_R":
                    name = "Right Thumb 3";
                    break;

                case "cf_j_index01_R":
                    name = "Right Index 1";
                    break;

                case "cf_j_index02_R":
                    name = "Right Index 2";
                    break;

                case "cf_j_index03_R":
                    name = "Right Index 3";
                    break;

                case "cf_j_middle01_R":
                    name = "Right Middle 1";
                    break;

                case "cf_j_middle02_R":
                    name = "Right Middle 2";
                    break;

                case "cf_j_middle03_R":
                    name = "Right Middle 3";
                    break;

                case "cf_j_ring01_R":
                    name = "Right Ring 1";
                    break;

                case "cf_j_ring02_R":
                    name = "Right Ring 2";
                    break;

                case "cf_j_ring03_R":
                    name = "Right Ring 3";
                    break;

                case "cf_j_little01_R":
                    name = "Right Little 1";
                    break;

                case "cf_j_little02_R":
                    name = "Right Little 2";
                    break;

                case "cf_j_little03_R":
                    name = "Right Little 3";
                    break;

                case "cf_j_thumb01_L":
                    name = "Left Thumb 1";
                    break;

                case "cf_j_thumb02_L":
                    name = "Left Thumb 2";
                    break;

                case "cf_j_thumb03_L":
                    name = "Left Thumb 3";
                    break;

                case "cf_j_index01_L":
                    name = "Left Index 1";
                    break;

                case "cf_j_index02_L":
                    name = "Left Index 2";
                    break;

                case "cf_j_index03_L":
                    name = "Left Index 3";
                    break;

                case "cf_j_middle01_L":
                    name = "Left Middle 1";
                    break;

                case "cf_j_middle02_L":
                    name = "Left Middle 2";
                    break;

                case "cf_j_middle03_L":
                    name = "Left Middle 3";
                    break;

                case "cf_j_ring01_L":
                    name = "Left Ring 1";
                    break;

                case "cf_j_ring02_L":
                    name = "Left Ring 2";
                    break;

                case "cf_j_ring03_L":
                    name = "Left Ring 3";
                    break;

                case "cf_j_little01_L":
                    name = "Left Little 1";
                    break;

                case "cf_j_little02_L":
                    name = "Left Little 2";
                    break;

                case "cf_j_little03_L":
                    name = "Left Little 3";
                    break;

                case "cf_j_thigh00_R":
                    name = "Right THIGH";
                    break;

                case "cf_j_thigh00_L":
                    name = "Left THIGH";
                    break;

                case "cf_j_spine01":
                    name = "SPINE";
                    break;

                case "cf_d_bust00":
                    name = "BUST";
                    break;

                default:
                    break;
            }
            return name;
        }

        public static OCIFolder CreateParentFolder(List<ObjectCtrlInfo> childList, string name = "Parent", bool selectParent = true, bool centerParent = false)
        {
            if (childList.Count == 0) return null;

            // Create Parent folder and change the name.
            OCIFolder folder = AddObjectFolder.Add();
            folder.name = $"{childList[0].treeNodeObject.textName} | {name}";
            folder.guideObject.transformTarget.name = folder.name;

            // Copy the parent of the first Child to the Parent folder.
            if (childList[0].treeNodeObject.parent != null)
                Singleton<TreeNodeCtrl>.Instance.SetParent(folder.treeNodeObject, childList[0].treeNodeObject.parent);


            Vector3 centerPos = Vector3.zero;
            if (centerParent)
            {
                List<Vector3> posList = childList.Select(oci => oci.guideObject.changeAmount.pos).ToList();

                Vector3 sumPos = Vector3.zero;

                foreach (Vector3 vector in posList)
                    sumPos += vector;

                centerPos = sumPos / posList.Count;

                folder.guideObject.changeAmount.pos = Vector3.zero;
            }

            // Set the parent of all children to the Parent folder.
            foreach (ObjectCtrlInfo child in childList)
                Singleton<TreeNodeCtrl>.Instance.SetParent(child.treeNodeObject, folder.treeNodeObject);

            if (centerParent)
                GuideObjectPicker.TransformGuideObject(folder.guideObject, centerPos, true, true);

            // Select the Parent folder.
            if (selectParent)
            {
                folder.treeNodeObject.Select();
                folder.treeNodeObject.SetTreeState(TreeNodeObject.TreeState.Open);
            }

            //folder.guideObject.changeAmount.Copy(child.guideObject.changeAmount);

            return folder;
        }

        public static ObjectCtrlInfo CreateConstraint(Transform targetBone, bool pos, bool rot, bool scale, bool isSphere, bool inverse = false, string name = "", bool splitAxis = false, ObjectCtrlInfo parentOci = null)
        {
            if (parentOci == null && firstObject != null) parentOci = firstObject;
            if (name == "") name = targetBone.name;

            name = FancyBoneName(name);

            string alias = $"{name} | Constraint";

            if (firstChar != null)
            {
                ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                alias = $"{name} | {KKAPI.Chara.CharacterExtensions.GetFancyCharacterName(chaCtrl.chaFile)}";
            }

            ObjectCtrlInfo folder = null;

            if (!inverse)
                folder = AddObjectFolder.Add();
            else
            {
                if (isSphere)
                    folder = AddObjectItem.Add(0, 0, 0);
                else
                    folder = AddObjectFolder.Add();
            }

            ObjectCtrlInfo sphere = null;

            if (!isSphere)
                sphere = AddObjectFolder.Add();
            else
                sphere = AddObjectItem.Add(0, 0, 0);

            if (sphere == null) return null;

            sphere.treeNodeObject.SetVisible(false);

            Vector3 scaleOffset = Vector3.one;

            if (inverse)
                scaleOffset = new Vector3(sphere.guideObject.transformTarget.lossyScale.x / targetBone.lossyScale.x, sphere.guideObject.transformTarget.lossyScale.y / targetBone.lossyScale.y, sphere.guideObject.transformTarget.lossyScale.z / targetBone.lossyScale.z);
            else
                scaleOffset = new Vector3(targetBone.lossyScale.x / sphere.guideObject.transformTarget.lossyScale.x, targetBone.lossyScale.y / sphere.guideObject.transformTarget.lossyScale.y, targetBone.lossyScale.z / sphere.guideObject.transformTarget.lossyScale.z);


            sphere.guideObject.changeAmount.pos = targetBone.position;
            sphere.guideObject.changeAmount.rot = targetBone.rotation.eulerAngles;
            //sphere.guideObject.changeAmount.scale = targetBone.localScale;

            folder.guideObject.changeAmount.Copy(sphere.guideObject.changeAmount);

            if (folder is OCIFolder)
                ((OCIFolder)folder).name = $"{name} | Animation";
            else
                folder.treeNodeObject.textName = $"{name} | Animation";

            if (sphere is OCIFolder)
                ((OCIFolder)sphere).name = $"{name} | (Constraint)";
            else
                sphere.treeNodeObject.textName = $"{name} | (Constraint)";

            folder.guideObject.transformTarget.name = folder.treeNodeObject.textName;
            sphere.guideObject.transformTarget.name = folder.treeNodeObject.textName;

            OCIFolder folderX = null;
            OCIFolder folderY = null;
            OCIFolder folderZ = null;

            if (!inverse)
            {
                _nodeConstraints._selectedConstraint = _nodeConstraints.AddConstraint(true, sphere.guideObject.transformTarget, targetBone, pos, Vector3.zero, rot, Quaternion.identity, scale, scaleOffset, alias);
                Singleton<TreeNodeCtrl>.Instance.SetParent(sphere.treeNodeObject, folder.treeNodeObject);
               
                if (splitAxis)
                {
                    
                    
                    folderX = CreateParentFolder(new List<ObjectCtrlInfo>() { sphere }, "X", false, true);
                    folderY = CreateParentFolder(new List<ObjectCtrlInfo>() { sphere }, "Y", false, true);
                    folderZ = CreateParentFolder(new List<ObjectCtrlInfo>() { sphere }, "Z", false, true);

                    sphere.guideObject.changeAmount.Reset();
                    folder.guideObject.changeAmount.Reset();
                    
                    folderX.guideObject.changeAmount.Reset();
                    folderY.guideObject.changeAmount.Reset();
                    folderZ.guideObject.changeAmount.Reset();

                }
            }
            else
            {
                _nodeConstraints._selectedConstraint = _nodeConstraints.AddConstraint(true, targetBone, sphere.guideObject.transformTarget, pos, Vector3.zero, rot, Quaternion.identity, scale, scaleOffset, alias);
                Singleton<TreeNodeCtrl>.Instance.SetParent(folder.treeNodeObject, sphere.treeNodeObject);
            }

            

            Logger.LogInfo($"New constraint created to bone: {targetBone.name}");

            if (!keepTimeline.Value)
                return inverse ? sphere : folder;

            ReplaceTimelineInterpolables(folder, targetBone, parentOci, name, splitAxis, folderX, folderY, folderZ);

            return inverse ? sphere : folder;
        }

        public static void ReplaceTimelineInterpolables(ObjectCtrlInfo folder, Transform targetBone, ObjectCtrlInfo firstObject, string name, bool splitAxis = false, ObjectCtrlInfo folderX = null, ObjectCtrlInfo folderY = null, ObjectCtrlInfo folderZ = null)
        {
            List<Interpolable> itemsToRemove = new List<Interpolable>();
            List<Interpolable> itemsToAdd = new List<Interpolable>();

            List<Interpolable> allInterpolables = new List<Interpolable>(_timeline._interpolables.Values);

            if (splitAxis)
            {
                bool isPos = false;
                bool isRot = false;

                InterpolableModel modelGOPos = _timeline._interpolableModelsList.Find(i => i.id == "guideObjectPos");
                InterpolableModel modelGORot = _timeline._interpolableModelsList.Find(i => i.id == "guideObjectRot");

                InterpolableModel modelPos = _timeline._interpolableModelsList.Find(i => i.id == "bonePos");
                InterpolableModel modelRot = _timeline._interpolableModelsList.Find(i => i.id == "boneRot");

                foreach (Interpolable interpolable in allInterpolables)
                {
                    if (interpolable.oci != firstObject) continue;

                    if (interpolable.parameter is GuideObject)
                    {
                        GuideObject targetBoneGO;
                        if (_self._allGuideObjects.TryGetValue(targetBone, out targetBoneGO))
                        {
                            if (((GuideObject)interpolable.parameter) != targetBoneGO) continue;
                        }
                        else
                            continue;
                        
                    }
                    else if (interpolable.parameter is HashedPair<BonesEditor, Transform>)
                    {
                        if (((HashedPair<BonesEditor, Transform>)interpolable.parameter).value != targetBone) continue;
                    }
                    else
                        continue;

                    Interpolable newInterpolable = null;
                    switch (interpolable.id)
                    {
                        case "guideObjectXPos":
                            newInterpolable = new Interpolable(folderX, folderX.guideObject, modelGOPos);
                            isPos = true;
                            break;
                        case "guideObjectYPos":
                            newInterpolable = new Interpolable(folderY, folderY.guideObject, modelGOPos);
                            isPos = true;
                            break;
                        case "guideObjectZPos":
                            newInterpolable = new Interpolable(folderZ, folderZ.guideObject, modelGOPos);
                            isPos = true;
                            break;

                        case "guideObjectXRot":
                            newInterpolable = new Interpolable(folderX, folderX.guideObject, modelGORot);
                            isRot = true;
                            break;
                        case "guideObjectYRot":
                            newInterpolable = new Interpolable(folderY, folderY.guideObject, modelGORot);
                            isRot = true;
                            break;
                        case "guideObjectZRot":
                            newInterpolable = new Interpolable(folderZ, folderZ.guideObject, modelGORot);
                            isRot = true;
                            break;

                        case "boneXPos":
                        case "boneYPos":
                        case "boneZPos":
                            newInterpolable = new Interpolable(folder, folder.guideObject, modelPos);
                            break;

                        case "boneXRot":
                        case "boneYRot":
                        case "boneZRot":
                            newInterpolable = new Interpolable(folder, folder.guideObject, modelRot);
                            break;
                    }
                    if (newInterpolable == null) continue;

                    interpolable.enabled = false;

                    foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                    {
                        Keyframe keyframe = pair.Value;
                        float time = pair.Key;
                        float value = (float)keyframe.value;
                        Keyframe newKeyframe = null;

                        switch (interpolable.id)
                        {
                            case "guideObjectXPos":
                            case "boneXPos":
                                newKeyframe = new Keyframe(new Vector3(value, 0f, 0f), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "X Pos | " + interpolable.alias;
                                break;

                            case "guideObjectYPos":
                            case "boneYPos":
                                newKeyframe = new Keyframe(new Vector3(0f, value, 0f), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "Y Pos | " + interpolable.alias;
                                break;

                            case "guideObjectZPos":
                            case "boneZPos":
                                newKeyframe = new Keyframe(new Vector3(0f, 0f, value), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "Z Pos | " + interpolable.alias;
                                break;



                            case "guideObjectXRot":
                            case "boneXRot":
                                newKeyframe = new Keyframe(Quaternion.Euler(value, 0f, 0f), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "X Rot | " + interpolable.alias;
                                break;

                            case "guideObjectYRot":
                            case "boneYRot":
                                newKeyframe = new Keyframe(Quaternion.Euler(0f, value, 0f), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "Y Rot | " + interpolable.alias;
                                break;

                            case "guideObjectZRot":
                            case "boneZRot":
                                newKeyframe = new Keyframe(Quaternion.Euler(0f, 0f, value), newInterpolable, new AnimationCurve(keyframe.curve.keys));
                                newInterpolable.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolable.name : "Z Rot | " + interpolable.alias;
                                break;
                        }

                        if (newKeyframe != null)
                            newInterpolable.keyframes.Add(time, newKeyframe);

                    }

                    if (newInterpolable.keyframes.Count > 0)
                        itemsToAdd.Add(newInterpolable);
                }

                _nodeConstraints._selectedConstraint.position = isPos;
                _nodeConstraints._selectedConstraint.rotation = isRot;
            }
            else
            {
                foreach (Interpolable interpolable in allInterpolables)
                {
                    if (interpolable.oci == firstObject)
                    {
                        if (interpolable.id == "guideObjectPos" || interpolable.id == "guideObjectRot" || interpolable.id == "guideObjectScale" ||
                            interpolable.id == "guideObjectXPos" || interpolable.id == "guideObjectYPos" || interpolable.id == "guideObjectZPos" ||
                            interpolable.id == "guideObjectXRot" || interpolable.id == "guideObjectYRot" || interpolable.id == "guideObjectZRot")
                        {
                            GuideObject guideO = (GuideObject)interpolable.parameter;
                            if (guideO.transformTarget == targetBone)
                            {
                                Interpolable newInterpolable = new Interpolable(folder, folder.guideObject, interpolable);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = new Keyframe(pair.Value.value, newInterpolable, new AnimationCurve(pair.Value.curve.keys));

                                    newInterpolable.keyframes.Add(time, keyframe);
                                }
                                newInterpolable.alias = newInterpolable.name + " | " + name;
                                itemsToAdd.Add(newInterpolable);
                                itemsToRemove.Add(interpolable);
                            }
                        }
                        else if (interpolable.id == "bonePos" || interpolable.id == "boneRot" || interpolable.id == "boneScale"
                                || interpolable.id == "boneXPos" || interpolable.id == "boneYPos" || interpolable.id == "boneZPos"
                                || interpolable.id == "boneXRot" || interpolable.id == "boneYRot" || interpolable.id == "boneZRot")

                        {
                            string tName = "";

                            switch (interpolable.id)
                            {
                                case "bonePos":
                                    tName = $"B Position ({targetBone.name})";
                                    break;

                                case "boneRot":
                                    tName = $"B Rotation ({targetBone.name})";
                                    break;

                                case "boneScale":
                                    tName = $"B Scale ({targetBone.name})";
                                    break;
                                //
                                case "boneXPos":
                                    tName = $"(KKPE) X Pos ({targetBone.name})";
                                    break;

                                case "boneYPos":
                                    tName = $"(KKPE) Y Pos ({targetBone.name})";
                                    break;

                                case "boneZPos":
                                    tName = $"(KKPE) Z Pos ({targetBone.name})";
                                    break;
                                //
                                case "boneXRot":
                                    tName = $"(KKPE) X Rot ({targetBone.name})";

                                    break;

                                case "boneYRot":
                                    tName = $"(KKPE) Y Rot ({targetBone.name})";
                                    break;

                                case "boneZRot":
                                    tName = $"(KKPE) Z Rot ({targetBone.name})";
                                    break;
                            }

                            if (interpolable.name == tName)
                            {
                                InterpolableModel guideModel = null;

                                foreach (InterpolableModel model in _timeline._interpolableModelsList)
                                {
                                    if ((interpolable.id == "bonePos" && model.id == "guideObjectPos")
                                        || (interpolable.id == "boneRot" && model.id == "guideObjectRot")
                                        || (interpolable.id == "boneScale" && model.id == "guideObjectScale")
                                        || (interpolable.id == "boneXPos" && model.id == "guideObjectXPos")
                                        || (interpolable.id == "boneYPos" && model.id == "guideObjectYPos")
                                        || (interpolable.id == "boneZPos" && model.id == "guideObjectZPos")
                                        || (interpolable.id == "boneXRot" && model.id == "guideObjectXRot")
                                        || (interpolable.id == "boneYRot" && model.id == "guideObjectYRot")
                                        || (interpolable.id == "boneZRot" && model.id == "guideObjectZRot"))
                                        guideModel = model;
                                }

                                Interpolable newInterpolable = new Interpolable(folder, folder.guideObject, guideModel);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = new Keyframe(pair.Value.value, newInterpolable, new AnimationCurve(pair.Value.curve.keys));

                                    newInterpolable.keyframes.Add(time, keyframe);
                                }

                                newInterpolable.alias = newInterpolable.name + " | " + name;
                                itemsToAdd.Add(newInterpolable);
                                itemsToRemove.Add(interpolable);
                            }
                        }
                    }
                }
            }

            _timeline.RemoveInterpolables(itemsToRemove);

            foreach (Interpolable interpolable in itemsToAdd)
            {
                if (_timeline._interpolables.ContainsKey(interpolable.GetHashCode())) continue;

                _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                _timeline._interpolablesTree.AddLeaf(interpolable);
            }
        }

        private static void OnObjectsSelected(object sender, EventArgs e)
        {
            if (selectedObjects != KKAPI.Studio.StudioAPI.GetSelectedObjects())
            {
                selectedObjects = KKAPI.Studio.StudioAPI.GetSelectedObjects();

                if (selectedObjects != null && selectedObjects.Count() > 0)
                    firstObject = selectedObjects.First();
                else
                    firstObject = null; 

                if (firstObject != null)
                {
                    if (firstObject is OCIChar)
                    {
                        firstItem = null;
                        firstChar = (OCIChar)firstObject;
                    }
                    else if (firstObject is OCIItem)
                    {
                        firstChar = null;
                        firstItem = (OCIItem)firstObject;
                    }
                    else
                    {
                        firstChar = null;
                        firstItem = null;
                    }
                }
            }

            if (kkpeGuideObject != null)
                Singleton<GuideObjectManager>.Instance.Delete(kkpeGuideObject, true);

            if (PerformanceMode.performanceMode)
            {
                _timeline.UpdateInterpolablesView();
                _timeline.UpdateKeyframeWindow(false);
            }
        }

        private static void OnSceneLoad(object sender, EventArgs e)
        {
            if (resetTimelineAfterLoad.Value)
            {
                if (TimelineCompatibility.IsTimelineAvailable())
                {
                    Timeline.Timeline.Stop();
                }
            }

            //CreateTreeStateButton();
        }

        public static void CreateKKPEGuideObject()
        {
            if (kkpeGuideObject != null)
                Singleton<GuideObjectManager>.Instance.Delete(kkpeGuideObject, true);

            //if (kkpeUndoRedo.Value)
              //  UndoRedoManager.Instance.Clear();

            if (firstChar != null)
            {
                ChaControl chaCtrl = KKAPI.Studio.StudioObjectExtensions.GetChaControl(firstChar);
                firstKKPE = chaCtrl.GetComponent<PoseController>();
            }
            else if (firstItem != null)
            {
                firstKKPE = firstItem.objectItem.GetComponent<PoseController>();
            }

            kkpeTargetBone = firstKKPE._bonesEditor._boneTarget;

            /*
            if (_allGuideObjects.ContainsKey(kkpeTargetBone))
            {
                Singleton<GuideObjectManager>.Instance.selectObject = _allGuideObjects[kkpeTargetBone]; ;
                return;
            }*/
            
            ChangeAmount changeAmount = new ChangeAmount
            {
                pos = kkpeTargetBone.localPosition,
                rot = kkpeTargetBone.localRotation.eulerAngles,
                scale = kkpeTargetBone.localScale
            };

            //if (kkpeUndoRedo.Value)
              //  Studio.Studio.AddChangeAmount(kkpeGuideObjectDictKey, changeAmount);

            //kkpeGuideObject = Singleton<GuideObjectManager>.Instance.Add(kkpeTargetBone, kkpeGuideObjectDictKey);

            GameObject gameObject = Instantiate<GameObject>(Singleton<GuideObjectManager>.Instance.objectOriginal);
            gameObject.transform.SetParent(Singleton<GuideObjectManager>.Instance.transformWorkplace);
            kkpeGuideObject = gameObject.GetComponent<GuideObject>();
            kkpeGuideObject.transformTarget = kkpeTargetBone;
            kkpeGuideObject.dicKey = kkpeGuideObjectDictKey;

            kkpeGuideObject.enablePos = true;
            kkpeGuideObject.enableRot = true;
            kkpeGuideObject.enableScale = true;
            kkpeGuideObject.enableMaluti = false;
            kkpeGuideObject.calcScale = false;
            kkpeGuideObject.scaleRate = 0.5f;
            kkpeGuideObject.scaleRot = 0.025f;
            kkpeGuideObject.scaleSelect = 0.05f;
            kkpeGuideObject.parentGuide = firstObject.guideObject;
            kkpeGuideObject.SetActive(false, true);

            //if (!kkpeUndoRedo.Value)
            kkpeGuideObject.changeAmount = changeAmount;

            kkpeGuideObject.changeAmount.onChangePos += () => firstKKPE._bonesEditor.SetBoneTargetPosition(kkpeGuideObject.changeAmount.pos);
            kkpeGuideObject.changeAmount.onChangeRot += () => firstKKPE._bonesEditor.SetBoneTargetRotation(Quaternion.Euler(kkpeGuideObject.changeAmount.rot));
            kkpeGuideObject.changeAmount.onChangeScale += (newScale) => firstKKPE._bonesEditor.SetBoneTargetScale(newScale);

            Singleton<GuideObjectManager>.Instance.selectObject = kkpeGuideObject;
        }

        private static void RenameSelectedInterpolables()
        {
            if (_timeline._selectedInterpolables.Count > 0)
            {
                List<Timeline.Timeline.InterpolableDisplay> selectedDisplays = (from dsp in _timeline._displayedInterpolables
                                                                                join kvp in _timeline._selectedInterpolables on dsp.interpolable.obj equals kvp
                                                                                select dsp).ToList();

                Timeline.Timeline.InterpolableDisplay display = selectedDisplays[0];
                Interpolable selectedInterpolable = _timeline._selectedInterpolables[0];

                display.inputField.gameObject.SetActive(true);
                display.inputField.onEndEdit = new InputField.SubmitEvent();
                display.inputField.text = string.IsNullOrEmpty(selectedInterpolable.alias) ? selectedInterpolable.name : selectedInterpolable.alias;
                display.inputField.onEndEdit.AddListener(s =>
                {
                    for (int i = 0; i < _timeline._selectedInterpolables.Count; i++)
                    {
                        _timeline._selectedInterpolables[i].alias = display.inputField.text.Trim();
                    }
                    display.inputField.gameObject.SetActive(false);
                    _timeline.UpdateInterpolablesView();
                });
                display.inputField.ActivateInputField();
                display.inputField.Select();
            }
        }

        #region Timeline Colors

        public static void TimelineFirstColor(bool save = true)
        {
            Image[] allImages = _timeline._ui.GetComponentsInChildren<Image>(true);

            Image[] allImages2 = _timeline._interpolablePrefab.GetComponentsInChildren<Image>(true);

            Image[] allImages3 = _timeline._headerPrefab.GetComponentsInChildren<Image>(true);

            List<Image> imageList = new List<Image>();

            if (allImages.Length > 0)
            {
                foreach (Image image in allImages)
                {
                    if (image.sprite != null)
                    {
                        if (image.name == "StartLimitPanel" || image.name == "EndLimitPanel" || image.name == "goPickerSelection")
                            continue;

                        if (image.sprite.name == "Background" || image.name == "ShalltyUtilsButton")
                        {
                            if (image.name != "InterpolableModel(Clone)" && image.gameObject.transform.parent.name != "Interpolable(Clone)")
                                imageList.Add(image);
                        }
                        else if (image.sprite.name == "InputFieldBackground" && image.name == "Top")
                            imageList.Add(image);
                    }
                }
            }

            if (allImages2.Length > 0)
            {
                foreach (Image image in allImages2)
                {
                    if (image.sprite != null) imageList.Add(image);
                }
            }

            if (allImages3.Length > 0)
            {
                foreach (Image image in allImages3)
                {
                    if (image.sprite != null) imageList.Add(image);
                }
            }

            if (imageList.Count > 0)
            {
                if (save)
                {
                    Studio.Studio.Instance.colorPalette.visible = false;
                    Studio.Studio.Instance.colorPalette.Setup("Timeline First Color", imageList[0].color, (col) =>
                    {
                        foreach (Image image in imageList)
                            image.color = col;

                        timelinePrimaryColor.Value = col;
                        _self.Config.Save();
                    }, true);
                }
                else
                {
                    foreach (Image image in imageList)
                        image.color = timelinePrimaryColor.Value;
                }
            }
        }

        public static void TimelineSecondColor(bool save = true)
        {
            Image[] allImages = _timeline._ui.GetComponentsInChildren<Image>(true);
            Image[] allImages2 = _timeline._interpolableModelPrefab.GetComponentsInChildren<Image>(true);

            List<Image> imageList = new List<Image>();

            if (allImages.Length > 0)
            {
                foreach (Image image in allImages)
                {
                    if (image.sprite != null)
                    {
                        if (image.sprite.name == "InputFieldBackground" && image.name != "Top")
                        {
                            imageList.Add(image);
                        }
                        else if (image.name == "InterpolableModel(Clone)")
                            imageList.Add(image);
                    }
                }
            }

            if (allImages2.Length > 0)
            {
                foreach (Image image in allImages2)
                {
                    if (image.sprite != null) imageList.Add(image);
                }
            }

            if (imageList.Count > 0)
            {
                if (save)
                {
                    Studio.Studio.Instance.colorPalette.visible = false;
                    Studio.Studio.Instance.colorPalette.Setup("Timeline Second Color", imageList[0].color, (col) =>
                    {
                        foreach (Image image in imageList)
                            image.color = col;

                        timelineSecondaryColor.Value = col;
                        _self.Config.Save();
                        _self.Config.LoadWith(_self.Config);
                    }, true);
                }
                else
                {
                    foreach (Image image in imageList)
                        image.color = timelineSecondaryColor.Value;
                }
            }
        }

        public static void TimelineTextColor(bool save = true)
        {
            Text[] allText = _timeline._ui.GetComponentsInChildren<Text>(true);

            Text[] allText2 = _timeline._headerPrefab.GetComponentsInChildren<Text>(true);

            List<string> nameList = new List<string>
            {
                "Buttons",
                "Tooltip",
                "Main Fields",
                "Time",
                "Value",
                "Background",
                "Fields",
                "Curve Point Time",
                "Curve Point Value",
                "Curve Point InTangent",
                "Curve Point OutTangent",
                "Top Container",
                "Container",
                "Top",
                "All",
                "ShalltyUtilsButton",
                "InterpolableMoveUp",
                "InterpolableMoveDown",
                "ShalltyUtilsPanel"
            };

            Outline[] outlines = _timeline._ui.GetComponentsInChildren<Outline>(true);

            List<Text> textList = new List<Text>();

            if (allText.Length > 0)
            {
                foreach (Text text in allText)
                {
                    if (nameList.Contains(text.transform.parent.name))
                    {
                        if (text.transform.parent.parent != null && text.transform.parent.parent.name == "Interpolable(Clone)") 
                            continue;

                        if (!(text.transform.parent.name == "Time" && text.text != "Time"))
                            textList.Add(text);
                    }
                }
            }

            if (allText2.Length > 0)
                foreach (Text text in allText2)
                    textList.Add(text);

            if (save)
            {
                if (textList.Count > 0)
                {
                    Studio.Studio.Instance.colorPalette.visible = false;
                    Studio.Studio.Instance.colorPalette.Setup("Timeline Text Color", textList[0].color, (col) =>
                    {
                        foreach (Text text in textList)
                            text.color = col;

                        foreach (Outline outline in outlines)
                            if (outline.transform.parent.name == "Top Container") outline.enabled = col == Color.white;

                        timelineTextColor.Value = col;
                        _self.Config.Save();
                    }, true);
                }
            }
            else
            {
                foreach (Text text in textList)
                    text.color = timelineTextColor.Value;

                foreach (Outline outline in outlines)
                    if (outline.transform.parent.name == "Top Container") outline.enabled = timelineTextColor.Value == Color.white;
            }
        }

        public static void TimelineText2Color(bool save = true)
        {
            Text[] allText = _timeline._ui.GetComponentsInChildren<Text>(true);

            Text[] allText2 = _timeline._interpolableModelPrefab.GetComponentsInChildren<Text>(true);

            List<string> nameList = new List<string>
            {
                "Curve Fields",
                "InputField",
                "Duration",
                "Time",
                "Divisions",
                "Block Length",
                "Speed",
                "FrameRate",
                "InterpolableModel(Clone)"
            };

            List<Text> textList = new List<Text>();

            if (allText.Length > 0)
            {
                foreach (Text text in allText)
                {
                    if (nameList.Contains(text.transform.parent.name))
                    {
                        if ((text.transform.parent.name == "Time" && text.text != "Time"))
                            textList.Add(text);
                        else { textList.Add(text); }
                    }
                }
            }

            if (allText2.Length > 0)
            {
                foreach (Text text in allText2)
                    textList.Add(text);
            }

            if (save)
            {
                if (textList.Count > 0)
                {
                    Studio.Studio.Instance.colorPalette.visible = false;
                    Studio.Studio.Instance.colorPalette.Setup("Timeline Secondary Text Color", textList[0].color, (col) =>
                    {
                        foreach (Text text in textList)
                            text.color = col;

                        timelineText2Color.Value = col;
                        _self.Config.Save();
                    }, true);
                }
            }
            else
            {
                foreach (Text text in textList)
                    text.color = timelineText2Color.Value;
            }
        }

        #endregion

        #region TreeState Button

        private void SetTreeStateRecursive(TreeNodeObject treeNodeObject, TreeNodeObject.TreeState state)
        {
            treeNodeObject.SetTreeState(state);

            foreach (TreeNodeObject childTreeNodeObject in treeNodeObject.m_child)
            {
                SetTreeStateRecursive(childTreeNodeObject, state);
            }
        }

        IEnumerator ShowStateDelayed(TreeNodeObject treeNode, bool state)
        {
            yield return null;
            treeNode.SetStateVisible(state);
        }

        private void CreateTreeStateButton()
        {
            GameObject treeGameObject = Instantiate(Singleton<TreeNodeCtrl>.Instance.m_ObjectNode);
            treeGameObject.SetActive(true);
            treeGameObject.transform.SetParent(Singleton<WorkspaceCtrl>.Instance.buttonFolder.transform.parent, false);
            treeGameObject.transform.localPosition = new Vector3(-350f, -30f, 0f);
            TreeNodeObject treeNode = treeGameObject.GetComponent<TreeNodeObject>();
            if (treeNode != null)
            {
                treeNode.textName = "Expand All";
                //Singleton<TreeNodeCtrl>.Instance.m_TreeNodeObject.Insert(0, treeNode);

                treeNode.treeState = TreeNodeObject.TreeState.Close;
                treeNode.enableVisible = false;
                treeNode.visible = true;
                treeNode.baseColor = new Color(0f, 0f, 0f, 0f);
                treeNode.colorSelect = treeNode.baseColor;

                StartCoroutine(ShowStateDelayed(treeNode, true));

                treeNode.imageState.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                treeNode.m_ButtonSelect.gameObject.SetActive(true);
                treeNode.m_ButtonSelect.onClick.ActuallyRemoveAllListeners();
                treeNode.m_ButtonSelect.onClick.AddListener(() =>
                {
                    treeNode.treeState = treeNode.treeState == TreeNodeObject.TreeState.Open ? TreeNodeObject.TreeState.Close : TreeNodeObject.TreeState.Open;
                    treeNode.textName = treeNode.treeState != TreeNodeObject.TreeState.Open ? "Expand All" : "Collapse All";

                    foreach (TreeNodeObject treeNodeObject in Singleton<TreeNodeCtrl>.Instance.m_TreeNodeObject)
                    {
                        SetTreeStateRecursive(treeNodeObject, treeNode.treeState);
                    }
                });
                treeNode.m_ButtonState.onClick.AddListener(() =>
                {
                    treeNode.textName = treeNode.treeState != TreeNodeObject.TreeState.Open ? "Expand All" : "Collapse All";

                    foreach (TreeNodeObject treeNodeObject in Singleton<TreeNodeCtrl>.Instance.m_TreeNodeObject)
                    {
                        SetTreeStateRecursive(treeNodeObject, treeNode.treeState);
                    }
                });
            }
        }

        #endregion

        #region Interpolables Files
        
        public static void LoadInterpolablesFile()
        {
            string dir = _defaultDir;
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("LOAD INTERPOLABLES", dir, "XML files (*.xml)|*.xml", "xml", SingleFileFlags);
            if (file == null) return;

            if (File.Exists(file[0]))
            {
                List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();
                XmlDocument document = new XmlDocument();
                try
                {
                    document.Load(file[0]);
                    _timeline._selectedKeyframes.Clear();
                    ReadInterpolableTree(document.FirstChild, dic, firstObject);
                }
                catch (Exception e)
                {
                    Logger.LogError("Could not load data for OCI.\n" + document.FirstChild + "\n" + e);
                }

                try
                {
                    bool isPlaying = _timeline._isPlaying;
                    _timeline._isPlaying = true;
                    _timeline.UpdateCursor();
                    _timeline.Interpolate(true);
                    _timeline.Interpolate(false);
                    _timeline._isPlaying = isPlaying;
                    _timeline.UpdateInterpolablesView();
                }
                catch { }
            }
        }

        public static void SaveInterpolablesFile()
        {
            if (_timeline._selectedInterpolables.Count == 0)
            {
                Logger.LogMessage("First select at least one Interpolable!");
                return;
            }

            string dir = _defaultDir;
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("SAVE SELECTED INTERPOLABLES", dir, "XML files (*.xml)|*.xml", "xml", SingleFileFlags);
            if (file == null) return;


            using (XmlTextWriter writer = new XmlTextWriter(file[0], Encoding.UTF8))
            {
                List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();
                writer.WriteStartElement("root");

                foreach (INode node in _timeline._selectedInterpolables.Select(elem => (INode)_timeline._interpolablesTree.GetLeafNode(elem)))
                    _timeline.WriteInterpolableTree(node, writer, dic, leafNode => true);
                writer.WriteEndElement();
            }
        }
        public static void SaveInterpolablesToFile(List<Interpolable> interpolables)
        {
            if (interpolables.Count == 0) return;

            string dir = _defaultDir;
            OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
            OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
            string[] file = OpenFileDialog.ShowDialog("SAVE INTERPOLABLES", dir, "XML files (*.xml)|*.xml", "xml", SingleFileFlags);
            if (file == null) return;

            using (XmlTextWriter writer = new XmlTextWriter(file[0], Encoding.UTF8))
            {
                List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();
                writer.WriteStartElement("root");

                foreach (Interpolable interpolable in interpolables)
                    WriteInterpolable(interpolable, writer, dic, false);
                writer.WriteEndElement();
            }
        }

        private static void WriteInterpolable(Interpolable interpolable, XmlTextWriter writer, List<KeyValuePair<int, ObjectCtrlInfo>> dic, bool ignoreOci)
        {
            if (interpolable.keyframes.Count == 0)
                return;
            using (StringWriter stream = new StringWriter())
            {
                using (XmlTextWriter localWriter = new XmlTextWriter(stream))
                {
                    try
                    {
                        int objectIndex = -1;
                        if (interpolable.oci != null && !ignoreOci)
                        {
                            objectIndex = dic.FindIndex(e => e.Value == interpolable.oci);
                            if (objectIndex == -1)
                                return;
                        }

                        localWriter.WriteStartElement("interpolable");
                        localWriter.WriteAttributeString("enabled", XmlConvert.ToString(interpolable.enabled));
                        localWriter.WriteAttributeString("owner", interpolable.owner);
                        if (objectIndex != -1)
                            localWriter.WriteAttributeString("objectIndex", XmlConvert.ToString(objectIndex));
                        localWriter.WriteAttributeString("id", interpolable.id);

                        if (interpolable.writeParameterToXml != null)
                            interpolable.writeParameterToXml(interpolable.oci, localWriter, interpolable.parameter);
                        localWriter.WriteAttributeString("bgColorR", XmlConvert.ToString(interpolable.color.r));
                        localWriter.WriteAttributeString("bgColorG", XmlConvert.ToString(interpolable.color.g));
                        localWriter.WriteAttributeString("bgColorB", XmlConvert.ToString(interpolable.color.b));

                        localWriter.WriteAttributeString("alias", interpolable.alias);

                        foreach (KeyValuePair<float, Keyframe> keyframePair in interpolable.keyframes)
                        {
                            localWriter.WriteStartElement("keyframe");
                            localWriter.WriteAttributeString("time", XmlConvert.ToString(keyframePair.Key));

                            interpolable.WriteValueToXml(localWriter, keyframePair.Value.value);
                            foreach (UnityEngine.Keyframe curveKey in keyframePair.Value.curve.keys)
                            {
                                localWriter.WriteStartElement("curveKeyframe");
                                localWriter.WriteAttributeString("time", XmlConvert.ToString(curveKey.time));
                                localWriter.WriteAttributeString("value", XmlConvert.ToString(curveKey.value));
                                localWriter.WriteAttributeString("inTangent", XmlConvert.ToString(curveKey.inTangent));
                                localWriter.WriteAttributeString("outTangent", XmlConvert.ToString(curveKey.outTangent));
                                localWriter.WriteEndElement();
                            }

                            localWriter.WriteEndElement();
                        }

                        localWriter.WriteEndElement();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Couldn't save interpolable with the following value:\n" + interpolable + "\n" + e);
                        return;
                    }
                }
                writer.WriteRaw(stream.ToString());
            }
        }

        private static void ReadInterpolable(XmlNode interpolableNode, List<KeyValuePair<int, ObjectCtrlInfo>> dic, ObjectCtrlInfo overrideOci = null, GroupNode<Timeline.Timeline.InterpolableGroup> group = null)
        {
            List<KeyValuePair<float, Keyframe>> toSelect = new List<KeyValuePair<float, Keyframe>>();

            float currentTime = _timeline._playbackTime % _timeline._duration;
            if (currentTime == 0f && _timeline._playbackTime == _timeline._duration)
                currentTime = _timeline._duration;

            int skippedKeyframes = 0;
            bool added = false;
            Interpolable interpolable = null;
            try
            {
                if (interpolableNode.Name == "interpolable")
                {
                    string ownerId = interpolableNode.Attributes["owner"].Value;
                    ObjectCtrlInfo oci = null;
                    if (overrideOci != null)
                        oci = overrideOci;
                    else if (interpolableNode.Attributes["objectIndex"] != null)
                    {
                        int objectIndex = XmlConvert.ToInt32(interpolableNode.Attributes["objectIndex"].Value);
                        if (objectIndex >= dic.Count)
                            return;
                        oci = dic[objectIndex].Value;
                    }

                    string id = interpolableNode.Attributes["id"].Value;
                    InterpolableModel model = _timeline._interpolableModelsList.Find(i => i.owner == ownerId && i.id == id);
                    if (model == null)
                        return;
                    if (model.readParameterFromXml != null)
                        interpolable = new Interpolable(oci, model.readParameterFromXml(oci, interpolableNode), model);
                    else
                        interpolable = new Interpolable(oci, model);

                    interpolable.enabled = interpolableNode.Attributes["enabled"] == null || XmlConvert.ToBoolean(interpolableNode.Attributes["enabled"].Value);

                    if (interpolableNode.Attributes["bgColorR"] != null)
                    {
                        interpolable.color = new Color(
                                XmlConvert.ToSingle(interpolableNode.Attributes["bgColorR"].Value),
                                XmlConvert.ToSingle(interpolableNode.Attributes["bgColorG"].Value),
                                XmlConvert.ToSingle(interpolableNode.Attributes["bgColorB"].Value)
                        );
                    }

                    if (interpolableNode.Attributes["alias"] != null)
                        interpolable.alias = interpolableNode.Attributes["alias"].Value;

                    if (!_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                        _timeline._interpolablesTree.AddLeaf(interpolable, group);
                        added = true;
                    }
                    else
                    {
                        interpolable = _timeline._interpolables[interpolable.GetHashCode()];
                    }

                    foreach (XmlNode keyframeNode in interpolableNode.ChildNodes)
                    {
                        if (keyframeNode.Name == "keyframe")
                        {
                            float time = currentTime + XmlConvert.ToSingle(keyframeNode.Attributes["time"].Value);

                            object value = interpolable.ReadValueFromXml(keyframeNode);
                            List<UnityEngine.Keyframe> curveKeys = new List<UnityEngine.Keyframe>();
                            foreach (XmlNode curveKeyNode in keyframeNode.ChildNodes)
                            {
                                if (curveKeyNode.Name == "curveKeyframe")
                                {
                                    UnityEngine.Keyframe curveKey = new UnityEngine.Keyframe(
                                            XmlConvert.ToSingle(curveKeyNode.Attributes["time"].Value),
                                            XmlConvert.ToSingle(curveKeyNode.Attributes["value"].Value),
                                            XmlConvert.ToSingle(curveKeyNode.Attributes["inTangent"].Value),
                                            XmlConvert.ToSingle(curveKeyNode.Attributes["outTangent"].Value));
                                    curveKeys.Add(curveKey);
                                }
                            }

                            AnimationCurve curve;
                            if (curveKeys.Count == 0)
                                curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                            else
                                curve = new AnimationCurve(curveKeys.ToArray());

                            Keyframe keyframe = new Keyframe(value, interpolable, curve);

                            if (!interpolable.keyframes.ContainsKey(time))
                            {
                                interpolable.keyframes.Add(time, keyframe);
                                toSelect.Add(new KeyValuePair<float, Keyframe>(time, keyframe));
                            }
                            else skippedKeyframes++;
                        }
                    }
                }
                if (toSelect.Count > 0)
                    _timeline.SelectAddKeyframes(toSelect.ToArray());
                if (skippedKeyframes > 0)
                    Logger.LogMessage($"Skipped {skippedKeyframes} keyframes (Already exist)");
            }
            catch (Exception e)
            {
                Logger.LogWarning("Failed to load an Inteporlable: " + e.Message);
                if (added)
                    _timeline.RemoveInterpolable(interpolable);
            }
        }

        private static void ReadInterpolableTree(XmlNode groupNode, List<KeyValuePair<int, ObjectCtrlInfo>> dic, ObjectCtrlInfo overrideOci = null, GroupNode<Timeline.Timeline.InterpolableGroup> group = null)
        {
            foreach (XmlNode interpolableNode in groupNode.ChildNodes)
            {
                switch (interpolableNode.Name)
                {
                    case "interpolable":
                        ReadInterpolable(interpolableNode, dic, overrideOci, group);
                        break;

                    case "interpolableGroup":
                        string groupName = interpolableNode.Attributes["name"].Value;
                        GroupNode<Timeline.Timeline.InterpolableGroup> newGroup = _timeline._interpolablesTree.AddGroup(new Timeline.Timeline.InterpolableGroup { name = groupName }, group);
                        ReadInterpolableTree(interpolableNode, dic, overrideOci, newGroup);
                        break;
                }
            }
        }


        #endregion

        #region Main Menu Methods

        public static void CleanupKeyframes()
        {
            if (_timeline._interpolables.Count == 0) return;

            _timeline._selectedKeyframes.Clear();
            int totalRemovedKeyframes = 0;
            foreach (KeyValuePair<int, Interpolable> pair in _timeline._interpolables)
            {
                Interpolable interpolable = pair.Value;

                // REMOVE KEYFRAMES WITH SAME TIME

                Dictionary<float, int> seenTimes = new Dictionary<float, int>();
                List<int> indicesToDelete = new List<int>();

                int index = 0;
                foreach (var key in interpolable.keyframes)
                {
                    float currentKey = (float)Math.Round(key.Key, 6);

                    if (seenTimes.ContainsKey(currentKey))
                        indicesToDelete.Add(index);
                    else
                        seenTimes[currentKey] = index;

                    index++;
                }

                for (int i = indicesToDelete.Count - 1; i >= 0; i--)
                    interpolable.keyframes.RemoveAt(indicesToDelete[i]);
                
                totalRemovedKeyframes += indicesToDelete.Count;

                if (interpolable.keyframes.Count < 3) continue;

                List<KeyValuePair<float, Keyframe>> allKeyframes = interpolable.keyframes.Select(v => new KeyValuePair<float, Keyframe>(v.Key, v.Value)).ToList();
                List<KeyValuePair<float, Keyframe>> keyframesToDelete = new List<KeyValuePair<float, Keyframe>>();

                // REMOVE KEYFRAMES IN SEQUENCE

                int inSequence = 0;
                for (int i = 1; i < allKeyframes.Count; i++)
                {
                    if (allKeyframes[i - 1].Key == allKeyframes[i].Key)
                    {
                        keyframesToDelete.Add(allKeyframes[i - 1]);
                        continue;
                    }

                    if (Equals(allKeyframes[i - 1].Value.value, allKeyframes[i].Value.value))
                    {
                        inSequence++;

                        if (inSequence > 1)
                            keyframesToDelete.Add(allKeyframes[i - 1]);
                    }
                    else
                    {
                        inSequence = 0;
                    }
                }

                totalRemovedKeyframes += keyframesToDelete.Count;
                _timeline.DeleteKeyframes(keyframesToDelete);
            }

            Logger.LogMessage($"({totalRemovedKeyframes}) Keyframes removed.");
        }

        public static void CleanupAllTimeline()
        {
            aliasTimeline.UILib.UIUtility.DisplayConfirmationDialog(result =>
            {
                if (result)
                {
                    Timeline.Timeline.Stop();
                    _timeline._interpolables.Clear();
                    _timeline._interpolablesTree.Clear();
                    _timeline._selectedKeyframes.Clear();
                    _timeline._selectedOCI = null;
                    _timeline._selectedKeyframeCurvePointIndex = -1;
                    _timeline._duration = 10f;
                    _timeline._blockLength = 10f;
                    _timeline._divisions = 10;
                    _timeline._desiredFrameRate = 60;
                    _timeline.UpdateKeyframeWindow();
                    _timeline.UpdateInterpolablesView();

                    Logger.LogMessage("All Timeline data cleared from scene.");
                }

            }, "Are you sure you want to clean ALL timeline data from this scene?");
        }

        public static void SplitInterpolablesXYZ()
        {
            if (_timeline._selectedInterpolables.Count > 0)
            {
                List<Interpolable> itemsToRemove = new List<Interpolable>();
                List<Interpolable> itemsToAdd = new List<Interpolable>();

                InterpolableModel modelGOPosX = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectXPos");
                InterpolableModel modelGORotX = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectXRot");
                InterpolableModel modelPosX = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneXPos");
                InterpolableModel modelRotX = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneXRot");

                InterpolableModel modelGOPosY = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectYPos");
                InterpolableModel modelGORotY = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectYRot");
                InterpolableModel modelPosY = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneYPos");
                InterpolableModel modelRotY = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneYRot");

                InterpolableModel modelGOPosZ = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectZPos");
                InterpolableModel modelGORotZ = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "guideObjectZRot");
                InterpolableModel modelPosZ = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneZPos");
                InterpolableModel modelRotZ = _timeline._interpolableModelsList.Find(i => i.owner == "ShalltyUtils" && i.id == "boneZRot");

                List<Interpolable> selectedInterpolables = new List<Interpolable>(_timeline._selectedInterpolables);

                foreach (Interpolable interpolable in selectedInterpolables)
                {
                    switch (interpolable.id)
                    {
                        case "guideObjectPos":
                            {
                                Interpolable newInterpolableX = new Interpolable(interpolable.oci, interpolable.parameter, modelGOPosX);
                                Interpolable newInterpolableY = new Interpolable(interpolable.oci, interpolable.parameter, modelGOPosY);
                                Interpolable newInterpolableZ = new Interpolable(interpolable.oci, interpolable.parameter, modelGOPosZ);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = pair.Value;

                                    Keyframe keyframeX = new Keyframe(((Vector3)pair.Value.value).x, newInterpolableX, pair.Value.curve);
                                    Keyframe keyframeY = new Keyframe(((Vector3)pair.Value.value).y, newInterpolableY, pair.Value.curve);
                                    Keyframe keyframeZ = new Keyframe(((Vector3)pair.Value.value).z, newInterpolableZ, pair.Value.curve);

                                    newInterpolableX.keyframes.Add(time, keyframeX);
                                    newInterpolableY.keyframes.Add(time, keyframeY);
                                    newInterpolableZ.keyframes.Add(time, keyframeZ);
                                }

                                newInterpolableX.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableX.name : "X Pos | " + interpolable.alias;
                                newInterpolableY.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableY.name : "Y Pos | " + interpolable.alias;
                                newInterpolableZ.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableZ.name : "Z Pos | " + interpolable.alias;

                                newInterpolableX.color = interpolable.color;
                                newInterpolableY.color = interpolable.color;
                                newInterpolableZ.color = interpolable.color;

                                newInterpolableX.enabled = interpolable.enabled;
                                newInterpolableY.enabled = interpolable.enabled;
                                newInterpolableZ.enabled = interpolable.enabled;

                                itemsToAdd.Add(newInterpolableX);
                                itemsToAdd.Add(newInterpolableY);
                                itemsToAdd.Add(newInterpolableZ);

                                itemsToRemove.Add(interpolable);
                            }
                            break;

                        case "guideObjectRot":
                            {
                                Interpolable newInterpolableX = new Interpolable(interpolable.oci, interpolable.parameter, modelGORotX);
                                Interpolable newInterpolableY = new Interpolable(interpolable.oci, interpolable.parameter, modelGORotY);
                                Interpolable newInterpolableZ = new Interpolable(interpolable.oci, interpolable.parameter, modelGORotZ);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = pair.Value;

                                    Vector3 euler = ((Quaternion)keyframe.value).eulerAngles;

                                    Keyframe keyframeX = new Keyframe(euler.x, newInterpolableX, pair.Value.curve);
                                    Keyframe keyframeY = new Keyframe(euler.y, newInterpolableY, pair.Value.curve);
                                    Keyframe keyframeZ = new Keyframe(euler.z, newInterpolableZ, pair.Value.curve);

                                    newInterpolableX.keyframes.Add(time, keyframeX);
                                    newInterpolableY.keyframes.Add(time, keyframeY);
                                    newInterpolableZ.keyframes.Add(time, keyframeZ);
                                }

                                newInterpolableX.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableX.name : "X Rot | " + interpolable.alias;
                                newInterpolableY.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableY.name : "Y Rot | " + interpolable.alias;
                                newInterpolableZ.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableZ.name : "Z Rot | " + interpolable.alias;

                                newInterpolableX.color = interpolable.color;
                                newInterpolableY.color = interpolable.color;
                                newInterpolableZ.color = interpolable.color;

                                newInterpolableX.enabled = interpolable.enabled;
                                newInterpolableY.enabled = interpolable.enabled;
                                newInterpolableZ.enabled = interpolable.enabled;

                                itemsToAdd.Add(newInterpolableX);
                                itemsToAdd.Add(newInterpolableY);
                                itemsToAdd.Add(newInterpolableZ);

                                itemsToRemove.Add(interpolable);
                            }
                            break;

                        case "bonePos":
                            {
                                Interpolable newInterpolableX = new Interpolable(interpolable.oci, interpolable.parameter, modelPosX);
                                Interpolable newInterpolableY = new Interpolable(interpolable.oci, interpolable.parameter, modelPosY);
                                Interpolable newInterpolableZ = new Interpolable(interpolable.oci, interpolable.parameter, modelPosZ);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = pair.Value;

                                    Keyframe keyframeX = new Keyframe(((Vector3)pair.Value.value).x, newInterpolableX, pair.Value.curve);
                                    Keyframe keyframeY = new Keyframe(((Vector3)pair.Value.value).y, newInterpolableY, pair.Value.curve);
                                    Keyframe keyframeZ = new Keyframe(((Vector3)pair.Value.value).z, newInterpolableZ, pair.Value.curve);

                                    newInterpolableX.keyframes.Add(time, keyframeX);
                                    newInterpolableY.keyframes.Add(time, keyframeY);
                                    newInterpolableZ.keyframes.Add(time, keyframeZ);
                                }

                                newInterpolableX.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableX.name : "(KKPE) X Pos | " + interpolable.alias;
                                newInterpolableY.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableY.name : "(KKPE) Y Pos | " + interpolable.alias;
                                newInterpolableZ.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableZ.name : "(KKPE) Z Pos | " + interpolable.alias;

                                newInterpolableX.color = interpolable.color;
                                newInterpolableY.color = interpolable.color;
                                newInterpolableZ.color = interpolable.color;

                                newInterpolableX.enabled = interpolable.enabled;
                                newInterpolableY.enabled = interpolable.enabled;
                                newInterpolableZ.enabled = interpolable.enabled;

                                itemsToAdd.Add(newInterpolableX);
                                itemsToAdd.Add(newInterpolableY);
                                itemsToAdd.Add(newInterpolableZ);
                                itemsToRemove.Add(interpolable);
                            }
                            break;

                        case "boneRot":
                            {
                                Interpolable newInterpolableX = new Interpolable(interpolable.oci, interpolable.parameter, modelRotX);
                                Interpolable newInterpolableY = new Interpolable(interpolable.oci, interpolable.parameter, modelRotY);
                                Interpolable newInterpolableZ = new Interpolable(interpolable.oci, interpolable.parameter, modelRotZ);

                                foreach (KeyValuePair<float, Keyframe> pair in interpolable.keyframes)
                                {
                                    float time = pair.Key;
                                    Keyframe keyframe = pair.Value;

                                    Vector3 euler = ((Quaternion)keyframe.value).eulerAngles;

                                    Keyframe keyframeX = new Keyframe(euler.x, newInterpolableX, pair.Value.curve);
                                    Keyframe keyframeY = new Keyframe(euler.y, newInterpolableY, pair.Value.curve);
                                    Keyframe keyframeZ = new Keyframe(euler.z, newInterpolableZ, pair.Value.curve);

                                    newInterpolableX.keyframes.Add(time, keyframeX);
                                    newInterpolableY.keyframes.Add(time, keyframeY);
                                    newInterpolableZ.keyframes.Add(time, keyframeZ);
                                }

                                newInterpolableX.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableX.name : "(KKPE) X Rot | " + interpolable.alias;
                                newInterpolableY.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableY.name : "(KKPE) Y Rot | " + interpolable.alias;
                                newInterpolableZ.alias = interpolable.alias.IsNullOrEmpty() ? newInterpolableZ.name : "(KKPE) Z Rot | " + interpolable.alias;

                                newInterpolableX.color = interpolable.color;
                                newInterpolableY.color = interpolable.color;
                                newInterpolableZ.color = interpolable.color;

                                newInterpolableX.enabled = interpolable.enabled;
                                newInterpolableY.enabled = interpolable.enabled;
                                newInterpolableZ.enabled = interpolable.enabled;

                                itemsToAdd.Add(newInterpolableX);
                                itemsToAdd.Add(newInterpolableY);
                                itemsToAdd.Add(newInterpolableZ);

                                itemsToRemove.Add(interpolable);
                            }
                            break;

                        default: continue;
                    }
                }


                if (itemsToAdd.Count == 0)
                {
                    Logger.LogMessage("There are no (X/Y/Z) Interpolables!");
                    return;
                }

                foreach (Interpolable interpolable in itemsToRemove)
                {
                    if (_timeline._interpolables.ContainsKey(interpolable.GetHashCode()) == false) continue;

                    _timeline._interpolables.Remove(interpolable.GetHashCode());
                    _timeline._interpolablesTree.RemoveLeaf(interpolable);
                }

                foreach (Interpolable interpolable in itemsToAdd)
                {
                    if (_timeline._interpolables.ContainsKey(interpolable.GetHashCode())) continue;
                    
                    _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                    _timeline._interpolablesTree.AddLeaf(interpolable);
                }

                TimelineCompatibility.RefreshInterpolablesList();
            }
            else
                Logger.LogMessage("First select an Interpolable!");
        }

        public static void ConvertInterpolablesXYZToFolders()
        {
            if (_timeline._interpolables.Count == 0)
            {
                Logger.LogMessage("There are no Interpolables!");
                return;
            }

            List<OCIFolder> childFolders = new List<OCIFolder>();
            HashSet<GuideObject> guideObjectsDic = new HashSet<GuideObject>();

            List<Interpolable> allInterpolables = new List<Interpolable>(_timeline._interpolables.Values);

            foreach (Interpolable interpolable in allInterpolables)
            {
                switch (interpolable.id)
                {
                    case "guideObjectXPos":
                    case "guideObjectYPos":
                    case "guideObjectZPos":
                    case "guideObjectXRot":
                    case "guideObjectYRot":
                    case "guideObjectZRot":
                    break;

                    default: continue;
                }

                if (!(interpolable.parameter is GuideObject)) continue;

                GuideObject parameter = (GuideObject)interpolable.parameter;

                if (guideObjectsDic.Contains(parameter)) continue;
                
                guideObjectsDic.Add(parameter);

                ObjectCtrlInfo folder = CreateConstraint(parameter.transformTarget, false, false, false, false, false, "", true, interpolable.oci);
                
                if (parameter.transformTarget.parent != null)
                    _nodeConstraints.AddConstraint(true, parameter.transformTarget.parent, folder.guideObject.transformTarget, true, Vector3.zero, true, Quaternion.identity, true, Vector3.one, $"{parameter.transformTarget.parent.name} -> {folder.guideObject.transformTarget.name}");

                childFolders.Add((OCIFolder)folder);
            }

            if (childFolders.Count > 0)
            {
                OCIFolder parentFolder = AddObjectFolder.Add();
                parentFolder.name = "Baked XYZ Interpolables | Timeline";
                parentFolder.guideObject.transformTarget.name = parentFolder.name;

                foreach (OCIFolder child in childFolders)
                    Singleton<TreeNodeCtrl>.Instance.SetParent(child.treeNodeObject, parentFolder.treeNodeObject);

                parentFolder.guideObject.changeAmount.Reset();
                TimelineCompatibility.RefreshInterpolablesList();
            }
            else
                Logger.LogMessage("There are no (X/Y/Z) Interpolables!");


        }

        public static void ConvertInterpolablesToCurves()
        {
            if (_timeline._selectedInterpolables.Count == 0)
            {
                Logger.LogMessage("First select at least one (GO POS/ROT XYZ) Interpolable!");
                return;
            }

            AnimationCurve _stairsPreset = new AnimationCurve(new UnityEngine.Keyframe(0f, 0f, 0f, 0f), new UnityEngine.Keyframe(1f, 1f, float.PositiveInfinity, 0f));

            foreach (Interpolable interpolable in _timeline._selectedInterpolables)
            {
                switch (interpolable.id)
                {
                    case "guideObjectXPos":
                    case "guideObjectYPos":
                    case "guideObjectZPos":
                    case "guideObjectXRot":
                    case "guideObjectYRot":
                    case "guideObjectZRot":
                    break;

                    default: continue;
                }

                if (interpolable.keyframes.Count < 2) continue;

                float startTime = interpolable.keyframes.Keys.First();
                float endTime = interpolable.keyframes.Keys.Last();

                float startValue = (float)interpolable.keyframes.Values.First().value;
                float endValue = (float)interpolable.keyframes.Values.Last().value;


                AnimationCurve interpolableCurve = new AnimationCurve();

                List<UnityEngine.Keyframe> interpolableCurveKeyframes = new List<UnityEngine.Keyframe>();

                foreach (var pair in interpolable.keyframes)
                {
                    float time = pair.Key;
                    float value = (float)pair.Value.value;

                    float normalizedTime = Mathf.InverseLerp(startTime, endTime, time);

                    interpolableCurveKeyframes.Add(new UnityEngine.Keyframe(normalizedTime, value));
                }

                interpolableCurve.keys = interpolableCurveKeyframes.ToArray();

                SetCurveLinear(interpolableCurve);

                // Smooth all the keyframes of the curve
                for (int i = 0; i < interpolableCurve.length; i++)
                    interpolableCurve.SmoothTangents(i, 0f);
                

                interpolable.keyframes.Clear();

                //interpolable.keyframes.Add(startTime - 0.00001f, new Keyframe(startValue, interpolable, new AnimationCurve(_stairsPreset.keys)));

                interpolable.keyframes.Add(startTime, new Keyframe(0f, interpolable, interpolableCurve));
                interpolable.keyframes.Add(endTime, new Keyframe(1f, interpolable, new AnimationCurve(_stairsPreset.keys)));

                //interpolable.keyframes.Add(endTime + 0.00001f, new Keyframe(endValue, interpolable, new AnimationCurve(_stairsPreset.keys)));
            }

            _timeline.UpdateGrid();
        }

        public static void SetCurveLinear(AnimationCurve curveAnimation)
        {
            List<UnityEngine.Keyframe> keyframes = new List<UnityEngine.Keyframe>();

            for (int i = 0; i < curveAnimation.length; i++)
            {
                UnityEngine.Keyframe frame = curveAnimation[i];
                if (i > 0 && i != curveAnimation.length - 1)
                {
                    var nextFrame = curveAnimation[i + 1];
                    var prefFrame = curveAnimation[i - 1];
                    float inTangent = (float)(((double)frame.value - (double)prefFrame.value) / ((double)frame.time - (double)prefFrame.time));
                    float outTangent = (float)(((double)nextFrame.value - (double)frame.value) / ((double)nextFrame.time - (double)frame.time));
                    frame.inTangent = inTangent;
                    frame.outTangent = outTangent;
                }
                else
                {
                    if (i == 0)
                    {
                        var nextFrame = curveAnimation[i + 1];
                        float outTangent = (float)(((double)nextFrame.value - (double)frame.value) / ((double)nextFrame.time - (double)frame.time));
                        frame.outTangent = outTangent;
                    }
                    else if (i == curveAnimation.length - 1)
                    {
                        var prefFrame = curveAnimation[i - 1];
                        float inTangent = (float)(((double)frame.value - (double)prefFrame.value) / ((double)frame.time - (double)prefFrame.time));
                        frame.inTangent = inTangent;
                    }
                }
                keyframes.Add(frame);
            }
            curveAnimation.keys = keyframes.ToArray();
        }

        #endregion

        public static void NextFrame(int fps)
        {
            float beat = 1f / fps;
            float time = _timeline._playbackTime % _timeline._duration;
            float mod = time % beat;
            if (mod / beat < 0.5f)
                time -= mod;
            else
                time += beat - mod;
            time += beat;
            if (time > _timeline._duration)
                time = _timeline._duration;
            _timeline.SeekPlaybackTime(time);
        }

        public static void SelectLinkedGuideObject(PointerEventData e)
        {
            if (linkedGameObjectTimeline.Value == false) return;

            if (_timeline._selectedInterpolables.Any(x => x.oci?.guideObject != null|| x.parameter is GuideObject) == false) return;


            _self.ExecuteDelayed2(() =>
            {
                foreach (TreeNodeObject _node in Singleton<TreeNodeCtrl>.Instance.hashSelectNode)
                    _node.OnDeselect();

                Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Clear();

                foreach (GuideObject go in new HashSet<GuideObject>(Singleton<GuideObjectManager>.Instance.hashSelectObject))
                    Singleton<GuideObjectManager>.Instance.SetDeselectObject(go);

                Dictionary<TreeNodeObject, ObjectCtrlInfo> ocis = Singleton<Studio.Studio>.Instance.dicInfo;

                foreach (Interpolable interpolable in _timeline._selectedInterpolables)
                {
                    if (interpolable == null) continue;

                    if (interpolable.parameter is GuideObject linkedGuideObject && linkedGuideObject != null)
                    {
                        bool hasParent = linkedGuideObject.parentGuide != null;
                        TreeNodeObject node = ocis.Where(pair => ReferenceEquals(pair.Value.guideObject, !hasParent ? linkedGuideObject : linkedGuideObject.parentGuide)).Select(pair => pair.Key).FirstOrDefault();
                        if (node == null) continue;

                        if (hasParent)
                        {
                            if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node) == false)
                            {
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
                                    node.colorSelect = ((Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Count == 1) ? Studio.Utility.ConvertColor(91, 164, 82) : Studio.Utility.ConvertColor(94, 139, 100));
                                }
                            }

                            Singleton<GuideObjectManager>.Instance.AddSelectMultiple(linkedGuideObject);
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
                                Singleton<TreeNodeCtrl>.Instance.AddSelectNode(node, true);
                                if (Singleton<TreeNodeCtrl>.Instance.hashSelectNode.Contains(node))
                                {
                                    Singleton<GuideObjectManager>.Instance.StopSelectObject();
                                    linkedGuideObject.SetActive(true);
                                }
                            }
                        }

                    }
                    else if (interpolable.oci != null)
                    {
                        TreeNodeObject node = interpolable.oci.treeNodeObject;
                        if (node == null) continue;

                        Singleton<TreeNodeCtrl>.Instance.AddSelectNode(node, true);
                    }
                }
            });
        }

        private static List<Transform> GetAllChildTransforms(Transform parent)
        {
            List<Transform> childTransforms = new List<Transform>();

            foreach (Transform child in parent)
            {
                childTransforms.Add(child);

                childTransforms.AddRange(GetAllChildTransforms(child));
            }

            return childTransforms;
        }

        private static Texture2D CreateKeyframeTexture()
        {
            int textureSize = 32;
            Texture2D texture = new Texture2D(textureSize, textureSize);

            Color32[] pixels = new Color32[textureSize * textureSize];

            int halfSize = textureSize / 2;
            int index = 0;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    int distanceX = Mathf.Abs(x - halfSize);
                    int distanceY = Mathf.Abs(y - halfSize);
                    int maxDistance = halfSize;

                    if (distanceX + distanceY <= maxDistance)
                    {
                        if (distanceX + distanceY >= maxDistance - 4)
                        {
                            pixels[index] = Color.black; // Outline color
                        }
                        else
                        {
                            pixels[index] = Color.white; // Diamond color
                        }
                    }
                    else
                    {
                        pixels[index] = Color.clear; // Transparent
                    }

                    index++;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}