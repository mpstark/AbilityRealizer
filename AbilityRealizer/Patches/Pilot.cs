using BattleTech;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace AbilityRealizer
{
    [HarmonyPatch(typeof(Pilot), "InitAbilities")]
    public static class Pilot_InitAbilities_Patch
    {
        public static void Prefix(Pilot __instance)
        {
            Main.TryUpdateAbilities(__instance);
        }
    }

    [HarmonyPatch(typeof(Pilot), "CombatInitFromSave")]
    public static class Pilot_CombatInitFromSave_Patch
    {
        public static void Prefix(Pilot __instance)
        {
            Main.TryUpdateAbilities(__instance);
        }
    }

    [HarmonyPatch(typeof(Pilot), "AddToTeam")]
    public static class Pilot_AddToTeam_Patch
    {
        public static void Postfix(Pilot __instance)
        {
            Main.TryUpdateAbilities(__instance);
        }
    }
}
