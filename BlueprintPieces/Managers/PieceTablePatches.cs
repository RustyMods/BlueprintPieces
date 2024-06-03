using HarmonyLib;
using UnityEngine;

namespace BlueprintPieces.Managers;

public static class PieceTablePatches
{
    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetSelectedPrefab))]
    private static class PieceTable_GetSelectedPrefab_Postfix
    {
        private static void Postfix(ref GameObject __result)
        {
            if (!Blueprints.HasSelectedBlueprint()) return;
            Blueprints.Blueprint? blueprint = Blueprints.GetSelectedBlueprint();
            if (blueprint == null) return;
            __result = blueprint.m_ghost;
        }
    }

    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetSelectedPiece))]
    private static class PieceTable_GetSelectedPiece
    {
        private static void Postfix(Piece __result)
        {
            if (!__result) return;
            if (__result.TryGetComponent(out GhostBlueprint component))
            {
                component.Select();
            }
            else
            {
                Blueprints.Deselect();
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    private static class Player_PlacePiece_Postfix
    {
        private static void Postfix(Player __instance)
        {
            Blueprints.PlaceBlueprint(__instance.m_placementGhost, __instance);
            Blueprints.Deselect();
        }
    }
    
}