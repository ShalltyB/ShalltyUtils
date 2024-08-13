extern alias aliasTimeline;
using Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ExtensibleSaveFormat;

namespace ShalltyUtils
{
    class ShalltyUtilsSceneData : SceneCustomFunctionController
    {
        const int saveVersion = 0;

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            data.version = saveVersion;

            // Save GuideObject Picker Pages
            string guideObjectPickerData = GuideObjectPicker.SaveSceneData();
            if (!guideObjectPickerData.IsNullOrEmpty())
                data.data.Add("guideObjectPickerData", guideObjectPickerData);

            // Save KeyframesGroups
            string keyframesGroupsData = KeyframesGroups.SaveSceneData();
            if (!keyframesGroupsData.IsNullOrEmpty())
                data.data.Add("keyframesGroupsData", keyframesGroupsData);
           
            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            var data = GetExtendedData();

            if (operation == SceneOperationKind.Clear)
            {
                GuideObjectPicker.ClearSceneData();
                KeyframesGroups.ClearSceneData();
            }
            else if (operation == SceneOperationKind.Load)
            {
                GuideObjectPicker.LoadSceneData(data, loadedItems);
                KeyframesGroups.LoadSceneData(data);
            }
            else if (operation == SceneOperationKind.Import)
            {
                GuideObjectPicker.ImportSceneData(data, loadedItems);
            }

        }
    }
}
