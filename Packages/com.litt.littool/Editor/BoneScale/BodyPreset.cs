using System;
using System.Collections.Generic;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// A saved body shape ("체형"): bone scales by preset key ("head", "breast.L", "path:Armature/.../Ear_R").
    /// Stored in ProjectSettings/LITTOOLSettings.asset.
    /// </summary>
    [Serializable]
    internal sealed class BodyPreset
    {
        public string Name = string.Empty;
        public List<string> Keys = new List<string>();
        public List<Vector3> Values = new List<Vector3>();

        public int Count => Mathf.Min(Keys != null ? Keys.Count : 0, Values != null ? Values.Count : 0);

        public void Set(string key, Vector3 value)
        {
            if (Keys == null) Keys = new List<string>();
            if (Values == null) Values = new List<Vector3>();
            int index = Keys.IndexOf(key);
            if (index >= 0 && index < Values.Count)
            {
                Values[index] = value;
                return;
            }
            Keys.Add(key);
            Values.Add(value);
        }
    }
}
