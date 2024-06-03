using HarmonyLib;

namespace BlueprintPieces.Managers;

public static class WearNTearPatches
{
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.HaveSupport))]
    private static class WearNTear_HaveSupport_Postfix
    {
        private static void Postfix(ref bool __result)
        {
            if (Blueprints.IsBuilding()) __result = true;
        }
    }
}