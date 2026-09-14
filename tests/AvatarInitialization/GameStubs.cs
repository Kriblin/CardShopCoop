// Behavioral boundary only: no Unity rendering, prefab lifecycle, or network transport.
// Initialize deliberately models the game's public flag/private slot-list mismatch.
namespace UnityEngine
{
    public class Object
    {
        public static object SceneObject;
        public static T FindFirstObjectByType<T>() where T : class => SceneObject as T;
    }
    public class GameObject
    {
    }
    public class SkinnedMeshRenderer
    {
        public bool enabled;
    }
}

namespace CC
{
    public class CC_Property
    {
        public string propertyName;
        public float floatValue;
        public string stringValue;
    }

    public class CC_CharacterData
    {
        public string CharacterName;
        public List<string> HairNames;
        public List<CC_Property> HairColor;
        public List<string> ApparelNames;
        public List<int> ApparelMaterials;
        public List<CC_Property> Blendshapes, TextureProperties, FloatProperties, ColorProperties;
    }

    public class scrObj_Presets
    {
        public List<CC_CharacterData> Presets = new();
    }

    public class CharacterCustomization
    {
        public List<int> HairTables = new() { 1, 2 };
        public List<int> ApparelTables = new() { 1 };
        public List<UnityEngine.SkinnedMeshRenderer> CharacterMeshes = new() { new() };
        private List<UnityEngine.GameObject> HairObjects = new();
        private List<UnityEngine.GameObject> ApparelObjects = new();
        public bool m_HasInit = true, Autoload = true;
        public UnityEngine.GameObject UI = new();
        public string CharacterName = "Male0";
        public scrObj_Presets Presets = new();
        public SharedMaterials MaterialsStorage;
        public CC_CharacterData StoredCharacterData;
        public bool ThrowOnInitialize, ThrowOnApply;
        public int Applies;

        public void Initialize()
        {
            if (ThrowOnInitialize)
                throw new InvalidOperationException("injected initialization failure");
            if (!m_HasInit)
            {
                m_HasInit = true;
                HairObjects.AddRange(HairTables.Select(_ => new UnityEngine.GameObject()));
                ApparelObjects.AddRange(ApparelTables.Select(_ => new UnityEngine.GameObject()));
            }
            if (CharacterName.Length > 1)
                ApplyCharacterVars(Presets.Presets[0]);
            if (UI != null)
                throw new InvalidOperationException("editor bootstrap must not initialize game UI");
        }

        public void ApplyCharacterVars(CC_CharacterData data)
        {
            if (ThrowOnApply)
                throw new InvalidOperationException("injected appearance failure");
            for (int i = 0; i < data.HairNames.Count; i++)
            {
                _ = HairTables[i];
                _ = HairObjects[i];
                _ = data.HairColor[i].stringValue;
            }
            for (int i = 0; i < data.ApparelNames.Count; i++)
            {
                _ = ApparelTables[i];
                _ = ApparelObjects[i];
                _ = data.ApparelMaterials[i];
            }
            Applies++;
        }
    }
}

public class SharedMaterials { }
