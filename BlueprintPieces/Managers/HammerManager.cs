using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BlueprintPieces.Managers;

public static class HammerManager
{
    private static readonly LayerMask m_mask = LayerMask.GetMask("Default", "piece", "piece_nonsolid");

    public static void CreateSelectTool()
    {
        GameObject hammer = ZNetScene.instance.GetPrefab("Hammer");
        if (!hammer) return;
        if (!hammer.TryGetComponent(out ItemDrop component)) return;
        var icon = component.m_itemData.GetIcon();

        GameObject pavedRoad = ZNetScene.instance.GetPrefab("paved_road");
        if (!pavedRoad) return;
        GameObject _GhostOnly = pavedRoad.transform.GetChild(0).gameObject;
        if (!_GhostOnly) return;

        GameObject tool = Object.Instantiate(new GameObject("select"), BlueprintPiecesPlugin._Root.transform, false);
        tool.name = "RustyBlueprintSelection";

        Piece piece = tool.AddComponent<Piece>();
        piece.m_icon = icon;
        piece.m_name = "$piece_blueprint_select";
        piece.m_category = Piece.PieceCategory.All;
        piece.m_repairPiece = true;

        GameObject marker = Object.Instantiate(_GhostOnly, tool.transform, false);
        marker.SetActive(true);
        
        component.m_itemData.m_shared.m_buildPieces.m_pieces.Add(tool);
    }

    private static bool IsSelectionTool(string sharedName) => sharedName == "$piece_blueprint_select";

    [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
    private static class Player_Repair_Save_Blueprint
    {
        private static bool Prefix(Player __instance, Piece repairPiece)
        {
            if (!IsSelectionTool(repairPiece.m_name)) return true;
            SelectPieces(__instance);
            return false;
        }

        private static void SelectPieces(Player __instance)
        {
            if (!__instance.InPlaceMode()) return;
            Piece hoveringPiece = __instance.GetHoveringPiece();
            if (!hoveringPiece) return;
            if (!GetConnectedPieces(hoveringPiece)) return;
        }

        private static bool GetConnectedPieces(Piece piece)
        {
            int count = 0;
            var start = piece.GetComponentInChildren<Collider>();
            if (!start) return false;


            Dictionary<ZDOID, Collider> all = new();
            GetConnectedObjects(start, ref count, ref all);
            
            Blueprints.Blueprint blueprint = new();
            blueprint.m_name = $"blueprint_Multiple({count}";
            blueprint.m_creator = Player.m_localPlayer.GetHoverName();

            foreach (var collider in all.Values)
            {
                if (collider.TryGetComponent(out Piece pieceComponent))
                {
                    var transform = pieceComponent.transform;
                    pieceComponent.m_placeEffect.Create(transform.position, transform.rotation);

                    var transform1 = collider.transform;
                    blueprint.m_objects.Add(new Blueprints.PlanPiece()
                    {
                        m_position = transform1.localPosition,
                        m_rotation = transform1.localRotation,
                        m_prefab = pieceComponent.name.Replace("(Clone)",string.Empty),
                        m_scale = transform1.localScale,
                    });
                }
            }

            if (count == 0) return false;
            
            Debug.LogWarning("got " + count + " connected objects");
            Blueprints.GetAssets(ZNetScene.instance, out CraftingStation craftingStation, out EffectList placeEffects, out PieceTable table, out Sprite icon);
            Piece result = Blueprints.CreateGhostBlueprint(blueprint, placeEffects, icon, craftingStation, ZNetScene.instance, table);
            return true;
        }

        private static void GetConnectedObjects(Collider collider, ref int count, ref Dictionary<ZDOID, Collider> output)
        {
            foreach (var connection in Physics.OverlapSphere(collider.transform.position, 5f, m_mask))
            {
                if (count > 1000) return;
                if (connection.GetComponent<Piece>())
                {
                    if (!connection.TryGetComponent(out ZNetView znv)) continue;
                    if (!znv.IsValid()) continue;
                    var id = znv.GetZDO().m_uid;
                    if (output.ContainsKey(id)) continue;
                    ++count;
                    output[id] = connection;
                    GetConnectedObjects(connection, ref count, ref output);
                }
            }
        }
    }
}