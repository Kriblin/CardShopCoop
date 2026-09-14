using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace CardShopCoop.Sync
{
    // Only used on co-op-owned clones. Never modify the game's shared preset assets.
    internal static class CharacterTemplate
    {
        private static readonly FieldInfo HairObjects = typeof(CC.CharacterCustomization).GetField("HairObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ApparelObjects = typeof(CC.CharacterCustomization).GetField("ApparelObjects", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void InitializeFresh(CC.CharacterCustomization custom, bool female, string characterName = null)
        {
            if (HairObjects == null || ApparelObjects == null)
                throw new MissingFieldException("Character customization runtime slots are unavailable");
            // Unity can copy the public initialized flag without copying private runtime lists.
            // Replace lists on this fresh inactive clone, never on a live character.
            HairObjects.SetValue(custom, new List<GameObject>());
            ApparelObjects.SetValue(custom, new List<GameObject>());
            custom.m_HasInit = false;
            custom.Autoload = false;
            custom.CharacterName = ""; // LoadFromJSON returns before applying any preset.
            var ui = custom.UI;
            custom.UI = null;
            try
            {
                custom.Initialize();
            }
            finally
            {
                custom.UI = ui;
            }
            ApplyDefault(custom, string.IsNullOrEmpty(characterName) ? (female ? "Female" : "Male") + "0" : characterName);
        }

        internal static void ApplyDefault(CC.CharacterCustomization custom, string name)
        {
            string prefix = name.StartsWith("Female", StringComparison.OrdinalIgnoreCase) ? "Female" : "Male";
            var presets = custom.Presets != null ? custom.Presets.Presets : null;
            var preset = presets?.Find(p => p != null && p.CharacterName == name)
                ?? presets?.Find(p => p != null && (p.CharacterName ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (preset == null)
                throw new InvalidOperationException("No " + prefix + " character preset is available");
            var data = JsonConvert.DeserializeObject<CC.CC_CharacterData>(JsonConvert.SerializeObject(preset));
            NormalizeCharacterData(custom, data);
            custom.CharacterName = data.CharacterName;
            custom.StoredCharacterData = data;
            // Match Initialize's ResetParts step without reloading an unsanitized preset.
            if (custom.CharacterMeshes != null)
                foreach (var mesh in custom.CharacterMeshes)
                    if (mesh != null)
                        mesh.enabled = true;
            custom.ApplyCharacterVars(data);
        }
        internal static string Describe(CC.CharacterCustomization custom)
        {
            if (custom == null)
                return "customization=missing";
            var data = custom.StoredCharacterData;
            return $"preset={custom.CharacterName}, initialized={custom.m_HasInit}, "
                + $"hair tables/objects/names/colors={custom.HairTables?.Count}/{(HairObjects?.GetValue(custom) as List<GameObject>)?.Count}/{data?.HairNames?.Count}/{data?.HairColor?.Count}, "
                + $"apparel tables/objects/names/materials={custom.ApparelTables?.Count}/{(ApparelObjects?.GetValue(custom) as List<GameObject>)?.Count}/{data?.ApparelNames?.Count}/{data?.ApparelMaterials?.Count}";
        }

        internal static void NormalizeCharacterData(CC.CharacterCustomization custom, CC.CC_CharacterData data)
        {
            if (data.Blendshapes == null)
                data.Blendshapes = new List<CC.CC_Property>();
            if (data.TextureProperties == null)
                data.TextureProperties = new List<CC.CC_Property>();
            if (data.FloatProperties == null)
                data.FloatProperties = new List<CC.CC_Property>();
            if (data.ColorProperties == null)
                data.ColorProperties = new List<CC.CC_Property>();

            int hairSlots = custom.HairTables != null ? custom.HairTables.Count : 0;
            int apparelSlots = custom.ApparelTables != null ? custom.ApparelTables.Count : 0;
            NormalizeList(data.HairNames, hairSlots, "", value => data.HairNames = value);
            NormalizeList(data.HairColor, hairSlots, () => new CC.CC_Property(), value => data.HairColor = value);
            NormalizeList(data.ApparelNames, apparelSlots, "", value => data.ApparelNames = value);
            NormalizeList(data.ApparelMaterials, apparelSlots, 0, value => data.ApparelMaterials = value);
        }

        private static void NormalizeList<T>(List<T> source, int count, T fill, System.Action<List<T>> assign)
        {
            var list = source ?? new List<T>();
            if (list.Count > count)
                list.RemoveRange(count, list.Count - count);
            while (list.Count < count)
                list.Add(fill);
            assign(list);
        }

        private static void NormalizeList<T>(List<T> source, int count, System.Func<T> fill, System.Action<List<T>> assign)
        {
            var list = source ?? new List<T>();
            if (list.Count > count)
                list.RemoveRange(count, list.Count - count);
            while (list.Count < count)
                list.Add(fill());
            assign(list);
        }

    }
}
