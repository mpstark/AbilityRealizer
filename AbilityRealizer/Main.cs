using BattleTech;
using BattleTech.Data;
using Harmony;
using HBS;
using HBS.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityRealizer
{
    public static class Main
    {
        internal static List<string> ignoreAbilities = new List<string> { "TraitDefWeaponHit", "TraitDefMeleeHit" };
        internal static ILog HBSLog;

        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.AbilityRealizer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            HBSLog = Logger.GetLogger("AbilityRealizer");
        }


        // UTIL
        public static bool IsFirstLevelAbility(AbilityDef ability)
        {
            if (ability.IsPrimaryAbility && ability.ReqSkillLevel < 8)
                return true;

            return false;
        }

        public static AbilityDef GetAbilityDef(DataManager dm, string abilityName)
        {
            var hasAbility = dm.AbilityDefs.TryGet(abilityName, out var abilityDef);

            if (hasAbility)
                return abilityDef;

            return null;
        }

        public static List<string> GetPrimaryAbilitiesForPilot(DataManager dm, PilotDef pilotDef)
        {
            var primaryAbilities = new List<string>();
            foreach (var abilityName in pilotDef.abilityDefNames)
            {
                var abilityDef = GetAbilityDef(dm, abilityName);
                if (abilityDef != null && abilityDef.IsPrimaryAbility)
                    primaryAbilities.Add(abilityName);
            }
            return primaryAbilities;
        }

        public static bool CanLearnAbility(DataManager dm, PilotDef pilotDef, string abilityName)
        {
            var hasAbility = dm.AbilityDefs.TryGet(abilityName, out var abilityDef);

            if (!hasAbility)
            {
                HBSLog.Log($"\tCANNOT FIND {abilityName}");
                return false;
            }

            // can always learn non-primary
            if (!abilityDef.IsPrimaryAbility)
                return true;

            // can only have 3 primary abilities
            var primaryAbilities = GetPrimaryAbilitiesForPilot(dm, pilotDef);
            if (primaryAbilities.Count >= 3)
                return false;

            // can only have 2 first level abilities
            var firstLevelAbilities = 0;
            foreach (var ability in primaryAbilities)
                if (IsFirstLevelAbility(GetAbilityDef(dm, ability)))
                    firstLevelAbilities++;

            return firstLevelAbilities < 2;
        }

        public static void CheckAbilitiesFromProgession(List<string> pilotAbilityNames, string[][] progressionTable, int skillLevel, List<string> missingAbilities, List<string> matchingAbilities)
        {
            for (int i = 0; i < progressionTable.Length && i < skillLevel; i++)
            {
                for (int j = 0; j < progressionTable[i].Length; j++)
                {
                    var abilityName = progressionTable[i][j];

                    if (pilotAbilityNames.Contains(abilityName))
                        matchingAbilities.Add(abilityName);
                    else if (!ignoreAbilities.Exists(x => x.StartsWith(abilityName)))
                        missingAbilities.Add(abilityName);
                }
            }
        }


        // SETUP
        private static bool IsSetup = false;
        private static SimGameConstants constants;
        private static DataManager dataManager;
        private static List<string> progressionAbilities;

        private static void Setup()
        {
            if (IsSetup)
                return;

            constants = SimGameConstants.GetInstance(LazySingletonBehavior<UnityGameInstance>.Instance.Game);
            dataManager = LazySingletonBehavior<UnityGameInstance>.Instance.Game.DataManager;

            // make sure that datamanager has gotten all of the abilities
            dataManager.RequestAllResourcesOfType(BattleTechResourceType.AbilityDef);
            dataManager.ProcessRequests();

            progressionAbilities = new List<string>();

            // read in progression tables
            var progressionTables = new List<string[][]> { constants.Progression.GunnerySkills, constants.Progression.PilotingSkills, constants.Progression.GutsSkills, constants.Progression.TacticsSkills };
            foreach (var progressionTable in progressionTables)
            {
                for (int i = 0; i < progressionTable.Length; i++)
                {
                    for (int j = 0; j < progressionTable[i].Length; j++)
                        progressionAbilities.Add(progressionTable[i][j]);
                }
            }

            IsSetup = true;
        }


        // MEAT
        internal static void TryUpdateAbilities(PilotDef pilotDef)
        {
            Setup();

            if (pilotDef.abilityDefNames == null)
                return;

            var matchingAbilities = new List<string>();
            var missingAbilities = new List<string>();

            CheckAbilitiesFromProgession(pilotDef.abilityDefNames, constants.Progression.GunnerySkills, pilotDef.SkillGunnery, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgession(pilotDef.abilityDefNames, constants.Progression.PilotingSkills, pilotDef.SkillPiloting, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgession(pilotDef.abilityDefNames, constants.Progression.GutsSkills, pilotDef.SkillGuts, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgession(pilotDef.abilityDefNames, constants.Progression.TacticsSkills, pilotDef.SkillTactics, missingAbilities, matchingAbilities);

            var reloadAbilities = false;
            var extraAbilities = pilotDef.abilityDefNames.Except(matchingAbilities).ToList();

            if (extraAbilities.Count > 0 || missingAbilities.Count > 0)
                HBSLog.Log($"{pilotDef.Description.Id}");

            // remove abilities that don't exist anymore
            foreach (var abilityName in extraAbilities)
            {
                HBSLog.Log($"\tMAYBE EXTRA ABILITY {abilityName}");

                if (!progressionAbilities.Contains(abilityName) || GetAbilityDef(dataManager, abilityName) == null)
                {
                    HBSLog.Log($"\t\tForgetting ability {abilityName}");
                    pilotDef.abilityDefNames.RemoveAll(x => x == abilityName);
                    reloadAbilities = true;
                }
            }

            // add the missing abilities
            foreach (var abilityName in missingAbilities)
            {
                HBSLog.Log($"\tMAYBE MISSING ABILITY {abilityName}");

                if (CanLearnAbility(dataManager, pilotDef, abilityName))
                {
                    HBSLog.Log($"\t\tLearning {abilityName}");
                    pilotDef.abilityDefNames.Add(abilityName);
                    reloadAbilities = true;
                }
            }

            if (reloadAbilities)
            {
                if (pilotDef.AbilityDefs != null)
                    pilotDef.AbilityDefs.Clear();

                if (pilotDef.DataManager == null)
                    pilotDef.DataManager = dataManager;

                pilotDef.ForceRefreshAbilityDefs();
                HBSLog.Log($"\tForced refresh abilities");
            }
        }
    }
}
