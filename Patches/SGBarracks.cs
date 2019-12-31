using System.Linq;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using Harmony;
using HBS;
using HBS.Extensions;
using SVGImporter;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace AbilityRealizer.Patches
{
    [HarmonyPatch(typeof(SGBarracksSkillPip), "Initialize")]
    public static class SGBarracksSkillPip_Initialize_Patch
    {
        public static void Postfix(SGBarracksSkillPip __instance, string type, int index, bool hasPassives, AbilityDef ability)
        {
            if (!hasPassives)
                return;

            var simGame = LazySingletonBehavior<UnityGameInstance>.Instance.Game.Simulation;
            if (simGame == null)
                return;

            // get the abilities that are not primary
            var abilities = simGame.GetAbilityDefFromTree(type, index).Where(x => !x.IsPrimaryAbility).ToList();

            // gets the first ability that has a tooltip
            var passiveAbility = abilities.Find(x => !(string.IsNullOrEmpty(x.Description.Name) || string.IsNullOrEmpty(x.Description.Details)));

            // clear the dot on tooltip-less dots
            if (passiveAbility == null)
                Traverse.Create(__instance).Field("skillPassiveTraitDot").GetValue<SVGImage>().gameObject.SetActive(false);

            if (passiveAbility != null)
                __instance.gameObject.FindFirstChildNamed("obj-pip").GetComponent<HBSTooltip>()
                    .SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(passiveAbility.Description));
        }
    }
}