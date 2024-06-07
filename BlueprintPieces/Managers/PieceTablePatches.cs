using HarmonyLib;
using UnityEngine;

namespace BlueprintPieces.Managers;

public static class PieceTablePatches
{
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
            GameObject ghost = __instance.m_placementGhost;
            if (!ghost) return;
            if (!ghost.GetComponentInChildren<GhostBlueprint>()) return;
            Blueprints.PlaceBlueprint(__instance.m_placementGhost, __instance);
            Blueprints.Deselect();
            Blueprints.ResetStep();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
    private static class Player_UpdateGhost_Postfix
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance) return;
            var ghost = __instance.m_placementGhost;
            if (ghost == null) return;

            if (!Blueprints.SelectedBlueprint()) return;
            if (Blueprints.m_steps == Vector3.zero) return;
            ghost.transform.position += Blueprints.m_steps;
        }
    }
    
}