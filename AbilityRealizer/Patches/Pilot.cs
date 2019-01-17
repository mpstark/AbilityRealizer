using BattleTech;
using Harmony;

namespace AbilityRealizer
{
    [HarmonyPatch(typeof(Pilot), "InitAbilities")]
    public static class Pilot_InitAbilities_Patch
    {
        public static void Prefix(Pilot __instance)
        {
            Main.TryUpdateAbilitiesFromTree(__instance.pilotDef);
        }
    }

    [HarmonyPatch(typeof(Pilot), "CombatInitFromSave")]
    public static class Pilot_CombatInitFromSave_Patch
    {
        public static void Prefix(Pilot __instance)
        {
            Main.TryUpdateAbilitiesFromTree(__instance.pilotDef);
        }
    }
}
