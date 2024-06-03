using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlueprintPieces.Managers;

public static class Blueprints
{
    private static readonly string m_folderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "BlueprintPieces";
    public static readonly Material m_ghostMaterial = BlueprintPiecesPlugin._AssetBundle.LoadAsset<Material>("GhostMaterial");
    private static GameObject m_crate = null!;

    private static readonly List<Blueprint> m_blueprints = new();
    private static Blueprint? m_selectedBlueprint;
    
    public static bool HasSelectedBlueprint() => m_selectedBlueprint != null;
    public static Blueprint? GetSelectedBlueprint() => m_selectedBlueprint;

    public static void PlaceBlueprint(GameObject ghost, Player player)
    {
        if (ghost == null) return;
        foreach (Transform child in ghost.transform)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(child.name.Replace("(Clone)", string.Empty).Trim());
            GameObject place = Object.Instantiate(prefab, child.position, child.rotation);
            if (!place.TryGetComponent(out Piece component)) continue;
            component.m_creator = player.GetPlayerID();
        }
    }
    
    public static void Deselect() => m_selectedBlueprint = null;

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class Register_Blueprints
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            GameObject hammer = ZNetScene.instance.GetPrefab("Hammer");
            if (!hammer) return;
            if (!hammer.TryGetComponent(out ItemDrop component)) return;

            m_crate = Object.Instantiate(BlueprintPiecesPlugin._AssetBundle.LoadAsset<GameObject>("Blueprint_Crate"), BlueprintPiecesPlugin._Root.transform, false);
            
            RegisterBlueprints(component.m_itemData.m_shared.m_buildPieces, component.m_itemData.GetIcon());
        }
    }

    private static void AddZNetView(GameObject prefab)
    {
        ZNetView znv = prefab.AddComponent<ZNetView>();
        znv.m_persistent = false;
    }

    private static void AddPiece(GameObject prefab, Blueprint blueprint, EffectList placeEffects, 
        Sprite icon, string name, CraftingStation artisanStation, ZNetScene instance)
    {
        Piece piece = prefab.AddComponent<Piece>();
        piece.enabled = true;

        ConfigEntry<string> nameConfig =
            BlueprintPiecesPlugin._Plugin.config(name, "DisplayName", name,
                "Set the display name for the blueprint");
        nameConfig.SettingChanged += (sender, args) => piece.m_name = nameConfig.Value;
        piece.m_name = nameConfig.Value;
        piece.m_description = $"<color=orange>Blueprint created by {blueprint.m_creator}</color>\n{blueprint.m_description}";
        piece.m_icon = icon;
        piece.m_placeEffect = placeEffects;
        
        ConfigEntry<string> stationConfig = BlueprintPiecesPlugin._Plugin.config(name, "Crafting Station", artisanStation.name,
            "Set the crafting station, if invalid, defaults to artisan table");
        stationConfig.SettingChanged += (sender, args) =>
        {
            GameObject CraftTable = instance.GetPrefab(stationConfig.Value);
            if (!CraftTable) return;
            piece.m_craftingStation = !CraftTable.TryGetComponent(out CraftingStation CraftComponent) ? artisanStation : CraftComponent;
        };
        GameObject CraftingTable = instance.GetPrefab(stationConfig.Value);
        if (!CraftingTable)
        {
            piece.m_craftingStation = artisanStation;
        }
        else
        {
            piece.m_craftingStation = !CraftingTable.TryGetComponent(out CraftingStation StationComponent)
                ? StationComponent
                : artisanStation;
        }

        ConfigEntry<Piece.PieceCategory> categoryConfig =
            BlueprintPiecesPlugin._Plugin.config(name, "Category", Piece.PieceCategory.Misc, "Set category of piece");
        piece.m_category = categoryConfig.Value;
        categoryConfig.SettingChanged += (sender, args) => piece.m_category = categoryConfig.Value;

        AddRecipe(piece, name, blueprint, artisanStation, instance);
    }

    private static void AddRecipe(Piece piece, string name, Blueprint blueprint, CraftingStation artisanStation, ZNetScene instance)
    {
        GameObject crate = Object.Instantiate(m_crate, BlueprintPiecesPlugin._Root.transform, false);
        crate.name = name + "_crate";

        if (!crate.TryGetComponent(out ItemDrop component)) return;
        component.gameObject.name = crate.name;
        component.name = crate.name;
        component.enabled = true;
        component.m_itemData.m_shared.m_name = name + " crate";
        component.m_itemData.m_shared.m_description = "Resource for the blueprint: " + name;
        component.m_itemData.m_dropPrefab = crate;

        Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
        recipe.name = name + "_recipe";
        recipe.m_item = component;
        recipe.m_amount = 1;
        recipe.m_enabled = true;
        ConfigEntry<string> stationConfig = BlueprintPiecesPlugin._Plugin.config(name, "Recipe Station", artisanStation.name,
            "Set the station to craft the resource crate");
        stationConfig.SettingChanged += (sender, args) =>
        {
            GameObject CraftTable = instance.GetPrefab(stationConfig.Value);
            if (!CraftTable) return;
            if (!CraftTable.TryGetComponent(out CraftingStation CraftComponent))
            {
                recipe.m_craftingStation = artisanStation;
                recipe.m_repairStation = artisanStation;
            }
            else
            {
                recipe.m_craftingStation = CraftComponent;
                recipe.m_repairStation = CraftComponent;
            }
        };
        
        GameObject CraftTable = instance.GetPrefab(stationConfig.Value);
        if (!CraftTable)
        {
            recipe.m_craftingStation = artisanStation;
            recipe.m_repairStation = artisanStation;
        }
        else
        {
            if (!CraftTable.TryGetComponent(out CraftingStation CraftStation))
            {
                recipe.m_craftingStation = artisanStation;
                recipe.m_repairStation = artisanStation;
            }
            else
            {
                recipe.m_craftingStation = CraftStation;
                recipe.m_repairStation = CraftStation;
            }
        }

        ConfigEntry<int> stationLevel = BlueprintPiecesPlugin._Plugin.config(name, "Recipe Station Level", 1,
            "Set the minimum station level required to access this recipe");
        recipe.m_minStationLevel = stationLevel.Value;
        stationLevel.SettingChanged += (sender, args) => recipe.m_minStationLevel = stationLevel.Value;

        Dictionary<string, Piece.Requirement> requirements = new();
        
        foreach (PlanPiece obj in blueprint.m_objects)
        {
            GameObject planObject = instance.GetPrefab(obj.m_prefab);
            if (!planObject) continue;
            if (!planObject.TryGetComponent(out Piece objPiece)) continue;
            
            foreach (Piece.Requirement requirement in objPiece.m_resources)
            {
                if (requirements.TryGetValue(requirement.m_resItem.m_itemData.m_shared.m_name, out Piece.Requirement match))
                {
                    match.m_amount += requirement.m_amount;
                }
                else
                {
                    requirements[requirement.m_resItem.m_itemData.m_shared.m_name] = new Piece.Requirement
                    {
                        m_resItem = requirement.m_resItem,
                        m_amount = requirement.m_amount
                    };
                }
            }
        }
        
        recipe.m_resources = requirements.Values.ToArray();
        
        RegisterRecipe(recipe);
        RegisterToZNetScene(crate);
        RegisterToObjectDB(crate);

        piece.m_resources = new[]
        {
            new Piece.Requirement()
            {
                m_resItem = component,
                m_amount = 1
            }
        };
    }

    private static void RegisterRecipe(Recipe recipe)
    {
        if (!ObjectDB.instance.m_recipes.Contains(recipe))
            ObjectDB.instance.m_recipes.Add(recipe);
    }

    private static void RegisterBlueprints(PieceTable table, Sprite icon)
    {
        ZNetScene instance = ZNetScene.instance;
        if (!instance) return;
        GameObject artisanTable = instance.GetPrefab("piece_artisanstation");
        if (!artisanTable) return;
        if (!artisanTable.TryGetComponent(out Piece artisanPiece)) return;
        if (!artisanTable.TryGetComponent(out CraftingStation craftingStation)) return;
        EffectList placeEffects = artisanPiece.m_placeEffect;
        
        foreach (Blueprint blueprint in m_blueprints)
        {
            GameObject prefab = Object.Instantiate(new GameObject("mock"), BlueprintPiecesPlugin._Root.transform, false);
            prefab.name = blueprint.m_name;
            string name = blueprint.m_name.Replace("blueprint_", string.Empty);

            AddZNetView(prefab);
            
            prefab.AddComponent<Transform>();
            
            AddPiece(prefab, blueprint, placeEffects, icon, name, craftingStation, instance);
            
            blueprint.m_ghost = prefab;
            
            GhostBlueprint ghost = prefab.AddComponent<GhostBlueprint>();
            ghost.m_blueprint = blueprint;
            
            table.m_pieces.Add(prefab);
            RegisterToZNetScene(prefab);
        }
    }

    private static void RegisterToZNetScene(GameObject prefab)
    {
        if (!ZNetScene.instance) return;
        if (!ZNetScene.instance.m_prefabs.Contains(prefab))
        {
            ZNetScene.instance.m_prefabs.Add(prefab);
        }

        ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
    }

    private static void RegisterToObjectDB(GameObject prefab)
    {
        if (!ObjectDB.instance) return;
        if (!ObjectDB.instance.m_items.Contains(prefab))
        {
            ObjectDB.instance.m_items.Add(prefab);
        }

        ObjectDB.instance.m_itemByHash[prefab.name.GetStableHashCode()] = prefab;
    }
    
    public static void ReadFiles()
    {
        if (!Directory.Exists(m_folderPath)) Directory.CreateDirectory(m_folderPath);
        string[] files = Directory.GetFiles(m_folderPath, "*.blueprint");
        foreach (var file in files)
        {
            try
            {
                string[] texts = File.ReadAllLines(file);
                string fileName = Path.GetFileName(file);
                Blueprint blueprint = ParseFile(texts, fileName);
                m_blueprints.Add(blueprint);
            }
            catch
            {
                BlueprintPiecesPlugin.BlueprintPiecesLogger.LogWarning("Failed to parse file:");
                BlueprintPiecesPlugin.BlueprintPiecesLogger.LogInfo(file);
            }
        }
    }

    private static Blueprint ParseFile(string[] texts, string fileName)
    {
        Blueprint blueprint = new();
        bool isPiece = true;
        for (int index = 0; index < texts.Length; index++)
        {
            string text = texts[index];
            if (text.StartsWith("#Name"))
            {
                blueprint.m_name = "blueprint_" + fileName.Replace(".blueprint", string.Empty);
            }
            else if (text.StartsWith("#Creator"))
            {
                blueprint.m_creator = ParseData(text);
            }
            else if (text.StartsWith("#Description"))
            {
                blueprint.m_description = ParseData(text);
            }
            else if (text.StartsWith("#Center"))
            {
                blueprint.m_center = ParseData(text);
            }
            else if (text.StartsWith("#Coordinates"))
            {
                blueprint.m_coordinates = ParseVector3(text) ?? new();
            }
            else if (text.StartsWith("#SnapPoints"))
            {
                isPiece = false;
            }
            else if (text.StartsWith("#Terrain"))
            {
                isPiece = false;
            }
            else if (text.StartsWith("#Pieces"))
            {
                isPiece = true;
            }
            else if (text.StartsWith("#"))
            {
            }
            else if (isPiece)
            {
                PlanPiece planPiece = ParsePiece(text);
                blueprint.m_objects.Add(planPiece);
            }
            else
            {
                SnapPoint snapPoint = ParseSnapPoint(text, index);
                blueprint.m_snapPoints.Add(snapPoint);
            }
        }

        return blueprint;
    }

    private static SnapPoint ParseSnapPoint(string text, int index)
    {
        SnapPoint snapPoint = new();
        string[] data = text.Split(';');
        if (float.TryParse(data[0], out float x)) return snapPoint;
        if (float.TryParse(data[1], out float y)) return snapPoint;
        if (float.TryParse(data[2], out float z)) return snapPoint;
        snapPoint.m_name = $"snappoint_{index}";
        snapPoint.m_coordinates = new Vector3(x, y, z);
        return snapPoint;
    }

    private static PlanPiece ParsePiece(string text)
    {
        PlanPiece planPiece = new();
        string[] data = text.Split(';');
        
        string prefab = data[0];
        string category = data[1];
        string unknown = data[9];
        string center = $"{data[10]}:{data[11]}:{data[12]}";
        
        planPiece.m_prefab = prefab;
        planPiece.m_coordinates = ParsePieceVector3(data[2], data[3], data[4]) ?? new();
        planPiece.m_rotation = ParsePieceRotation(data[5], data[6], data[7], data[8]) ?? new();
        planPiece.m_center = center;

        try
        {
            planPiece.m_data = data[13];
        }
        catch
        {
            planPiece.m_data = "";
        }
        
        return planPiece;
    }

    private static string ParseData(string text) => text.Split(':')[1];

    private static Vector3? ParsePieceVector3(string strX, string strY, string strZ)
    {
        if (!float.TryParse(strX, out float x)) return null;
        if (!float.TryParse(strY, out float y)) return null;
        if (!float.TryParse(strZ, out float z)) return null;
        return new Vector3(x, y, z);
    }

    private static Quaternion? ParsePieceRotation(string strX, string strY, string strZ, string strW)
    {
        if (!float.TryParse(strX, out float x)) return null;
        if (!float.TryParse(strY, out float y)) return null;
        if (!float.TryParse(strZ, out float z)) return null;
        if (!float.TryParse(strW, out float w)) return null;
        return new Quaternion(x, y, z, w);
    }

    private static Vector3? ParseVector3(string text)
    {
        var data = text.Split(':')[1];
        string[] values = data.Split(',');
        if (!float.TryParse(values[0], out float x)) return null;
        if (!float.TryParse(values[1], out float y)) return null;
        if (!float.TryParse(values[2], out float z)) return null;
        return new Vector3(x, y, z);
    }
    

    public class Blueprint
    {
        public string m_name = null!;
        public string m_creator = "";
        public string m_description = "";
        public string m_center = "";
        public Vector3 m_coordinates = new();
        public Vector3 m_rotation = new();
        public readonly List<PlanPiece> m_objects = new();
        public readonly List<SnapPoint> m_snapPoints = new();
        public GameObject m_ghost = new();
        public void Select() => m_selectedBlueprint = this;
    }

    public class PlanPiece
    {
        public string m_prefab = "";
        public Vector3 m_coordinates = Vector3.zero;
        public Quaternion m_rotation = Quaternion.identity;
        public string m_center = "";
        public string m_data = "";

        public ZPackage Deserialize()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(m_data);
            return pkg;
        }
    }

    public class SnapPoint
    {
        public string m_name = "";
        public Vector3 m_coordinates = new();
    }
}

public class GhostBlueprint : MonoBehaviour
{
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    public Blueprints.Blueprint? m_blueprint { get; set; }
    
    public void Select()
    {
        m_blueprint?.Select();
    }

    public void Awake()
    {
        SetupGhosts();
    }

    private void SetupGhosts()
    {
        if (m_blueprint == null)
        {
            m_blueprint = Blueprints.GetSelectedBlueprint();
            if (m_blueprint == null) return;
        }
        foreach (Blueprints.PlanPiece piece in m_blueprint.m_objects)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(piece.m_prefab);
            if (!prefab) continue;
            GameObject ghost = Instantiate(prefab, piece.m_coordinates, piece.m_rotation, transform);
            CreateGhostMaterials(ghost);
        }
    }

    private void CreateGhostMaterials(GameObject prefab)
    {
        if (BlueprintPiecesPlugin._UseGhostMaterial.Value is BlueprintPiecesPlugin.Toggle.Off) return;

        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
        {
            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int index = 0; index < renderer.sharedMaterials.Length; index++)
            {
                Texture tex = sharedMaterials[index].mainTexture;
                Material key = new Material(Blueprints.m_ghostMaterial)
                {
                    mainTexture = tex,
                    color = new Color(1f, 1f, 1f, 0.5f)
                };
                if (sharedMaterials[index].HasProperty(BumpMap))
                {
                    Texture normal = sharedMaterials[index].GetTexture(BumpMap);
                    key.SetTexture(BumpMap, normal);
                }
                sharedMaterials[index] = key;
            }
            
            renderer.sharedMaterials = sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }  
    }
}