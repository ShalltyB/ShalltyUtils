using UnityEngine;
using System;
using Studio;
using static ShalltyUtils.ShalltyUtils;
using System.Collections.Generic;
using Keyframe = Timeline.Keyframe;
using System.Linq;
using Timeline;
using ADV.Commands.Object;

namespace ShalltyUtils
{
    public class UndoRedoCommands
    {

        public class AddKeyframeCommand : ICommand
        {
            private int keyframesCount;
            private Timeline.Interpolable interpolable;
            private float time;
            private Keyframe keyframe;

            public AddKeyframeCommand(Timeline.Interpolable interpolable, float time, int keyframesCount)
            {
                this.keyframesCount = keyframesCount;
                this.interpolable = interpolable;
                this.time = time;
            }

            public void Do()
            {
                if (keyframesCount != interpolable.keyframes.Count)
                {
                    if (interpolable.keyframes.ContainsKey(time))
                    {
                        keyframe = interpolable.keyframes[time];
                    }
                }
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                if (interpolable != null && interpolable.keyframes.ContainsKey(time) && _timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                {
                    interpolable.keyframes.Remove(time);
                    if (interpolable.keyframes.Count == 0)
                        _timeline.RemoveInterpolable(interpolable);
                    _timeline.UpdateGrid();
                }
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                if (interpolable != null && !interpolable.keyframes.ContainsKey(time))
                {
                    interpolable.keyframes.Add(time, keyframe);
                    _timeline.UpdateGrid();

                    if (!_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                        _timeline._interpolablesTree.AddLeaf(interpolable);
                        _timeline.UpdateInterpolablesView();
                    }
                }
            }
        }

        public class AddMultipleKeyframeCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> keyframes;

            public AddMultipleKeyframeCommand(List<KeyValuePair<float, Keyframe>> keyframes)
            {
                this.keyframes = keyframes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                foreach (KeyValuePair<float, Keyframe> pair in keyframes)
                {
                    if (pair.Value == null)
                        continue;
                    try
                    {
                        pair.Value.parent.keyframes.Remove(pair.Key);
                        if (pair.Value.parent.keyframes.Count == 0)
                            _timeline.RemoveInterpolable(pair.Value.parent);
                    }
                    catch (Exception e)
                    { }
                }
                _timeline.UpdateInterpolablesView();
                _timeline.UpdateGrid();
                _timeline.UpdateKeyframeWindow(false);
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                foreach (KeyValuePair<float, Keyframe> pair in keyframes)
                {
                    if (pair.Value == null)
                        continue;

                    Interpolable interpolable = pair.Value.parent;

                    if (interpolable != null && !interpolable.keyframes.ContainsKey(pair.Key))
                    {
                        interpolable.keyframes.Add(pair.Key, pair.Value);
                        
                        if (!_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                        {
                            _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                            _timeline._interpolablesTree.AddLeaf(interpolable);
                        }
                    }
                }
                _timeline.UpdateInterpolablesView();
                _timeline.UpdateGrid();
                _timeline.UpdateKeyframeWindow(false);
            }
        }

        public class MoveKeyframeCommand : ICommand
        {
            private float oldTime;
            private float destinationTime;
            private Keyframe keyframe;

            public MoveKeyframeCommand(Keyframe keyframe, float destinationTime, float oldTime)
            {
                this.keyframe = keyframe;
                this.destinationTime = destinationTime;
                this.oldTime = oldTime;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                int index = keyframe.parent.keyframes.IndexOfValue(keyframe);
                if (index != -1)
                {
                    keyframe.parent.keyframes.RemoveAt(keyframe.parent.keyframes.IndexOfValue(keyframe));
                    keyframe.parent.keyframes.Add(oldTime, keyframe);
                    int i = _timeline._selectedKeyframes.FindIndex(k => k.Value == keyframe);
                    if (i != -1)
                        _timeline._selectedKeyframes[i] = new KeyValuePair<float, Keyframe>(oldTime, keyframe);
                    _timeline.UpdateGrid();
                }
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                int index = keyframe.parent.keyframes.IndexOfValue(keyframe);
                if (index != -1)
                {
                    keyframe.parent.keyframes.RemoveAt(index);
                    keyframe.parent.keyframes.Add(destinationTime, keyframe);
                    int y = _timeline._selectedKeyframes.FindIndex(k => k.Value == keyframe);
                    if (y != -1)
                        _timeline._selectedKeyframes[y] = new KeyValuePair<float, Keyframe>(destinationTime, keyframe);
                    _timeline.UpdateGrid();
                }
            }
        }

        public class MoveMultipleKeyframeCommand : ICommand
        {
            private List<float> oldTimes;
            private List<float> destinationTimes;
            private List<Keyframe> keyframes;

            public MoveMultipleKeyframeCommand(List<Keyframe> keyframes, List<float> destinationTimes, List<float> oldTimes)
            {
                this.keyframes = keyframes;
                this.destinationTimes = destinationTimes;
                this.oldTimes = oldTimes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int x = 0; x < keyframes.Count; x++)
                {
                    Keyframe keyframe = keyframes[x];
                    float oldTime = oldTimes[x];
                    float destinationTime = destinationTimes[x];

                    int index = keyframe.parent.keyframes.IndexOfValue(keyframe);
                    if (index != -1)
                    {
                        keyframe.parent.keyframes.RemoveAt(keyframe.parent.keyframes.IndexOfValue(keyframe));
                        keyframe.parent.keyframes.Add(oldTime, keyframe);
                        int i = _timeline._selectedKeyframes.FindIndex(k => k.Value == keyframe);
                        if (i != -1)
                            _timeline._selectedKeyframes[i] = new KeyValuePair<float, Keyframe>(oldTime, keyframe);
                    }
                }
                _timeline.UpdateGrid();
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int x = 0; x < keyframes.Count; x++)
                {
                    Keyframe keyframe = keyframes[x];
                    float oldTime = oldTimes[x];
                    float destinationTime = destinationTimes[x];

                    int index = keyframe.parent.keyframes.IndexOfValue(keyframe);
                    if (index != -1)
                    {
                        keyframe.parent.keyframes.RemoveAt(index);
                        keyframe.parent.keyframes.Add(destinationTime, keyframe);
                        int y = _timeline._selectedKeyframes.FindIndex(k => k.Value == keyframe);
                        if (y != -1)
                            _timeline._selectedKeyframes[y] = new KeyValuePair<float, Keyframe>(destinationTime, keyframe);
                    }
                }
                _timeline.UpdateGrid();
            }
        }

        public class DeleteKeyframeCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> deletedKeyframes;
            private bool removeInterpolables;
            private IEnumerable<KeyValuePair<float, Keyframe>> keyframes;

            public DeleteKeyframeCommand(IEnumerable<KeyValuePair<float, Keyframe>> keyframes, bool removeInterpolables, List<KeyValuePair<float, Keyframe>> deletedKeyframes)
            {
                this.keyframes = keyframes;
                this.removeInterpolables = removeInterpolables;
                this.deletedKeyframes = deletedKeyframes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < deletedKeyframes.Count; i++)
                {
                    KeyValuePair<float, Keyframe> pair = deletedKeyframes[i];
                    Timeline.Interpolable interpolable = pair.Value.parent;
                    float time = pair.Key;
                    Keyframe keyframe = pair.Value;

                    if (interpolable != null)
                    {
                        if (_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                        {
                            if (!_timeline._interpolables[interpolable.GetHashCode()].keyframes.ContainsKey(time))
                                _timeline._interpolables[interpolable.GetHashCode()].keyframes.Add(time, keyframe);
                        }
                        else
                        {
                            _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                            _timeline._interpolablesTree.AddLeaf(interpolable);

                            if (!_timeline._interpolables[interpolable.GetHashCode()].keyframes.ContainsKey(time))
                                interpolable.keyframes.Add(time, keyframe);
                        }
                    }
                }

                _timeline.UpdateGrid();
                _timeline.UpdateInterpolablesView();
            }

            public void Redo()
            {
                if (deletedKeyframes.Count > 0)
                {
                    _timeline._selectedKeyframes.Clear();
                    _timeline.DeleteKeyframes(deletedKeyframes, removeInterpolables);
                }
            }
        }

        public class RemoveInterpolablesCommand : ICommand
        {
            private IEnumerable<Interpolable> interpolables;

            public RemoveInterpolablesCommand(IEnumerable<Interpolable> interpolables)
            {
                this.interpolables = interpolables;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                foreach (Interpolable interpolable in interpolables)
                {
                    if (!_timeline._interpolables.ContainsKey(interpolable.GetHashCode()))
                    {
                        _timeline._interpolables.Add(interpolable.GetHashCode(), interpolable);
                        _timeline._interpolablesTree.AddLeaf(interpolable);
                    }
                }
                _timeline.UpdateInterpolablesView();
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                _timeline.RemoveInterpolables(interpolables);
            }
        }

        public class UseCurrentValueCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> keyframes;
            private List<object> oldValues;

            public UseCurrentValueCommand(List<object> oldValues, List<KeyValuePair<float, Keyframe>> keyframes)
            {
                this.keyframes = keyframes;
                this.oldValues = oldValues;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    object oldValue = keyframes[i].Value.value;
                    keyframes[i].Value.value = oldValues[i];
                    oldValues[i] = oldValue;
                }
                _timeline.UpdateKeyframeValueText();
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    object oldValue = keyframes[i].Value.value;
                    keyframes[i].Value.value = oldValues[i];
                    oldValues[i] = oldValue;
                }
                _timeline.UpdateKeyframeValueText();
            }
        }

        public class SaveKeyframeTimeCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> keyframes;
            private float time;
            private List<float> oldTime;

            public SaveKeyframeTimeCommand(float time, List<float> oldTime, List<KeyValuePair<float, Keyframe>> keyframes)
            {
                this.time = time;
                this.oldTime = oldTime;
                this.keyframes = keyframes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    KeyValuePair<float, Keyframe> pair = keyframes[i];
                    float time = oldTime[i];
                    Keyframe potentialDuplicateKeyframe;
                    if (pair.Value.parent.keyframes.TryGetValue(time, out potentialDuplicateKeyframe) && potentialDuplicateKeyframe != pair.Value)
                        continue;
                    pair.Value.parent.keyframes.Remove(pair.Key);
                    pair.Value.parent.keyframes.Add(time, pair.Value);
                }
                _timeline.UpdateKeyframeTimeTextField();
                _timeline.UpdateGrid();
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    KeyValuePair<float, Keyframe> pair = keyframes[i];
                    Keyframe potentialDuplicateKeyframe;
                    if (pair.Value.parent.keyframes.TryGetValue(time, out potentialDuplicateKeyframe) && potentialDuplicateKeyframe != pair.Value)
                        continue;
                    pair.Value.parent.keyframes.Remove(pair.Key);
                    pair.Value.parent.keyframes.Add(time, pair.Value);
                    oldTime[i] = time;
                }
                _timeline.UpdateKeyframeTimeTextField();
                _timeline.UpdateGrid();
            }
        }

        public class DragAtCurrentTimeCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> keyframes;
            private List<float> newTime;
            private List<float> oldTime;

            public DragAtCurrentTimeCommand(List<float> newTime, List<float> oldTime, List<KeyValuePair<float, Keyframe>> keyframes)
            {
                this.newTime = newTime;
                this.oldTime = oldTime;
                this.keyframes = keyframes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    KeyValuePair<float, Keyframe> pair = keyframes[i];
                    float time = oldTime[i];
                    Keyframe potentialDuplicateKeyframe;
                    if (pair.Value.parent.keyframes.TryGetValue(time, out potentialDuplicateKeyframe) && potentialDuplicateKeyframe != pair.Value)
                        continue;
                    pair.Value.parent.keyframes.Remove(pair.Key);
                    pair.Value.parent.keyframes.Add(time, pair.Value);
                }

                //_timeline.UpdateKeyframeTimeTextField();
                _timeline.UpdateGrid();
            }

            public void Redo()
            {
                _timeline._selectedKeyframes.Clear();
                for (int i = 0; i < keyframes.Count; i++)
                {
                    KeyValuePair<float, Keyframe> pair = keyframes[i];
                    float time = newTime[i];
                    Keyframe potentialDuplicateKeyframe;
                    if (pair.Value.parent.keyframes.TryGetValue(time, out potentialDuplicateKeyframe) && potentialDuplicateKeyframe != pair.Value)
                        continue;
                    pair.Value.parent.keyframes.Remove(oldTime[i]);
                    pair.Value.parent.keyframes.Add(time, pair.Value);
                }

                //_timeline.UpdateKeyframeTimeTextField();
                _timeline.UpdateGrid();
            }
        }

        /* UNDO/REDO Selection: Generates a lot of commands, annoying but can be useful sometimes     
         * 
        public class SelectAddKeyframesCommand : ICommand
        {
            private List<KeyValuePair<float, Keyframe>> oldKeyframes;
            private List<KeyValuePair<float, Keyframe>> newKeyframes;
            public SelectAddKeyframesCommand(List<KeyValuePair<float, Keyframe>> oldKeyframes, List<KeyValuePair<float, Keyframe>> newKeyframes)
            {
                this.oldKeyframes = oldKeyframes;
                this.newKeyframes = newKeyframes;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                if (oldKeyframes.Count > 0)
                {
                    _timeline._selectedKeyframes.Clear();
                    _timeline._selectedKeyframes.AddRange(oldKeyframes);

                    var cacheList = new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes);

                    _timeline._keyframeSelectionSize = cacheList.Max(k => k.Key) - cacheList.Min(k => k.Key);
                    _timeline.UpdateKeyframeSelection();
                    _timeline.UpdateKeyframeWindow();
                }
            }

            public void Redo()
            {
                if (newKeyframes.Count > 0)
                {
                    _timeline._selectedKeyframes.Clear();
                    _timeline._selectedKeyframes.AddRange(newKeyframes);

                    var cacheList = new List<KeyValuePair<float, Keyframe>>(_timeline._selectedKeyframes);

                    _timeline._keyframeSelectionSize = cacheList.Max(k => k.Key) - cacheList.Min(k => k.Key);
                    _timeline.UpdateKeyframeSelection();
                    _timeline.UpdateKeyframeWindow();
                }
            }
        }
        */

        // KKPE BONES *Unused*

        public class SetBoneTargetPositionCommand : ICommand
        {
            private HSPE.AMModules.BonesEditor bonesEditor;
            private Transform bone;
            private Vector3 newPosition;
            private Vector3 oldPosition;

            public SetBoneTargetPositionCommand(Transform bone, Vector3 newPosition, Vector3 oldPosition, HSPE.AMModules.BonesEditor bonesEditor)
            {
                this.bone = bone;
                this.newPosition = newPosition;
                this.oldPosition = oldPosition;
                this.bonesEditor = bonesEditor;
            }

            public void Do()
            {    
            }

            public void Undo()
            {
                if (bone != null && bonesEditor != null)
                {
                    Vector3 position = oldPosition;

                    bonesEditor.SetBonePosition(bone, position);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                    {
                        position.x *= -1f;
                        bonesEditor.SetBonePosition(bonesEditor._twinBoneTarget, position);
                    }
                }
            }

            public void Redo()
            {
                if (bone != null && bonesEditor != null)
                {
                    Vector3 position = newPosition;

                    bonesEditor.SetBonePosition(bone, position);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                    {
                        position.x *= -1f;
                        bonesEditor.SetBonePosition(bonesEditor._twinBoneTarget, position);
                    }
                }
            }
        }

        public class SetBoneTargetRotationCommand : ICommand
        {
            private HSPE.AMModules.BonesEditor bonesEditor;
            private Transform bone;
            private Quaternion newRotation;
            private Quaternion oldRotation;

            public SetBoneTargetRotationCommand(Transform bone, Quaternion newRotation, Quaternion oldRotation, HSPE.AMModules.BonesEditor bonesEditor)
            {
                this.bone = bone;
                this.newRotation = newRotation;
                this.oldRotation = oldRotation;
                this.bonesEditor = bonesEditor;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                if (bone != null && bonesEditor != null)
                {
                    bonesEditor.SetBoneRotation(bone, oldRotation);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                        bonesEditor.SetBoneRotation(bonesEditor._twinBoneTarget, new Quaternion(-oldRotation.x, oldRotation.y, oldRotation.z, -oldRotation.w));
                }
            }

            public void Redo()
            {
                if (bone != null && bonesEditor != null)
                {
                    bonesEditor.SetBoneRotation(bone, newRotation);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                        bonesEditor.SetBoneRotation(bonesEditor._twinBoneTarget, new Quaternion(-newRotation.x, newRotation.y, newRotation.z, -newRotation.w));
                }
            }
        }

        public class SetBoneTargetScaleCommand : ICommand
        {
            private HSPE.AMModules.BonesEditor bonesEditor;
            private Transform bone;
            private Vector3 newScale;
            private Vector3 oldScale;

            public SetBoneTargetScaleCommand(Transform bone, Vector3 newScale, Vector3 oldScale, HSPE.AMModules.BonesEditor bonesEditor)
            {
                this.bone = bone;
                this.newScale = newScale;
                this.oldScale = oldScale;
                this.bonesEditor = bonesEditor;
            }

            public void Do()
            {
            }

            public void Undo()
            {
                if (bone != null && bonesEditor != null)
                {
                    bonesEditor.SetBoneScale(bone, oldScale);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                        bonesEditor.SetBoneScale(bonesEditor._twinBoneTarget, oldScale);
                }
            }

            public void Redo()
            {
                if (bone != null && bonesEditor != null)
                {
                    bonesEditor.SetBoneScale(bone, newScale);

                    if (bonesEditor._symmetricalEdition && bonesEditor._twinBoneTarget != null)
                        bonesEditor.SetBoneScale(bonesEditor._twinBoneTarget, newScale);
                }
            }
        }

    }
}
