using BattleTech;
using BattleTech.Data;
using Harmony;
using HBS;
using HBS.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable InconsistentNaming

namespace AbilityRealizer
{
    public static class Main
    {
        internal static ModSettings Settings;
        internal static ILog HBSLog;
        internal static string modDirectory;

        private static bool IsSetup;
        private static SimGameConstants constants;
        private static DataManager dataManager;
        private static List<string> progressionAbilities;


        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.AbilityRealizer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            HBSLog = Logger.GetLogger("AbilityRealizer");
            Settings = ModSettings.Parse(modSettings);
            modDirectory = modDir;
        }


        // UTIL
        private static bool IsFirstLevelAbility(AbilityDef ability)
        {
            return ability.IsPrimaryAbility && ability.ReqSkillLevel < 8;
        }

        private static AbilityDef GetAbilityDef(DataManager dm, string abilityName)
        {
            var hasAbility = dm.AbilityDefs.TryGet(abilityName, out var abilityDef);
            return hasAbility ? abilityDef : null;
        }

        private static bool HasAbilityDef(DataManager dm, string abilityName)
        {
            return dm.AbilityDefs.TryGet(abilityName, out var _);
        }

        private static List<string> GetPrimaryAbilitiesForPilot(DataManager dm, PilotDef pilotDef)
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

        private static bool CanLearnAbility(DataManager dm, PilotDef pilotDef, string abilityName)
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
            {
                if (IsFirstLevelAbility(GetAbilityDef(dm, ability)))
                    firstLevelAbilities++;
            }

            return firstLevelAbilities < 2;
        }

        private static void CheckAbilitiesFromProgression(List<string> pilotAbilityNames, string[][] progressionTable, int skillLevel, List<string> missingAbilities, List<string> matchingAbilities)
        {
            for (var i = 0; i < progressionTable.Length && i < skillLevel; i++)
            {
                for (var j = 0; j < progressionTable[i].Length; j++)
                {
                    var abilityName = progressionTable[i][j];

                    if (pilotAbilityNames.Contains(abilityName))
                        matchingAbilities.Add(abilityName);
                    else
                        missingAbilities.Add(abilityName);
                }
            }
        }


        // SETUP
        internal static void Setup()
        {
            if (IsSetup)
                return;

            constants = SimGameConstants.GetInstance(LazySingletonBehavior<UnityGameInstance>.Instance.Game);
            dataManager = LazySingletonBehavior<UnityGameInstance>.Instance.Game.DataManager;

            // make sure that datamanager has gotten all of the abilities
            var loadRequest = dataManager.CreateLoadRequest(x =>
            {
                HBSLog.Log("AbilityDefs Loaded");

                if (!Settings.DumpFixedPilotDefMerges)
                    return;

                HBSLog.Log("PilotDefs loaded");
                DumpFixedPilotDefMerges();
            });

            loadRequest.AddAllOfTypeBlindLoadRequest(BattleTechResourceType.AbilityDef);

            // if dumping pilotDefs, request them
            if (Settings.DumpFixedPilotDefMerges)
                loadRequest.AddAllOfTypeBlindLoadRequest(BattleTechResourceType.PilotDef);

            loadRequest.ProcessRequests();

            progressionAbilities = new List<string>();

            // read in progression tables
            var progressionTables = new List<string[][]> { constants.Progression.GunnerySkills, constants.Progression.PilotingSkills, constants.Progression.GutsSkills, constants.Progression.TacticsSkills };
            foreach (var progressionTable in progressionTables)
            {
                foreach (var abilityTable in progressionTable)
                {
                    foreach (var abilityName in abilityTable)
                        progressionAbilities.Add(abilityName);
                }
            }

            IsSetup = true;
        }


        // MEAT
        private static void DumpFixedPilotDefMerges()
        {
            if (dataManager == null)
                return;

            var directory = Path.Combine(modDirectory, "PilotDefDump");
            Directory.CreateDirectory(directory);
            HBSLog.Log($"Dumping fixed PilotDef merges to {directory}");

            foreach (var pilotID in dataManager.PilotDefs.Keys)
            {
                var pilotDef = dataManager.PilotDefs.Get(pilotID);
                var pilotDefCopy = new PilotDef(pilotDef, pilotDef.ExperienceSpent, pilotDef.ExperienceUnspent,
                    pilotDef.Injuries, pilotDef.LethalInjury, pilotDef.LifetimeInjuries, pilotDef.MechKills,
                    pilotDef.OtherKills, pilotDef.PilotTags);

                if (!UpdateAbilitiesFromTree(pilotDefCopy))
                    continue;

                // pilotDef updated, dump it out to dir
                pilotDefCopy.abilityDefNames.Sort();

                var pilotDefJObject = new JObject {{"abilityDefNames", new JArray(pilotDefCopy.abilityDefNames)}};
                using (var writer = File.CreateText(Path.Combine(directory, pilotID + ".json")))
                {
                    var jsonWriter = new JsonTextWriter(writer) {Formatting = Formatting.Indented};
                    pilotDefJObject.WriteTo(jsonWriter);
                    jsonWriter.Close();
                }
            }
        }

        internal static void TryUpdateAbilities(Pilot pilot)
        {
            // skip pilots with specified pilot tags
            foreach (var tag in pilot.pilotDef.PilotTags)
            {
                if (Settings.IgnorePilotsWithTags.Exists(x => tag.StartsWith(x)))
                    return;
            }

            if (dataManager.PilotDefs.Exists(pilot.pilotDef.Description.Id)
                && pilot.pilotDef == dataManager.PilotDefs.Get(pilot.pilotDef.Description.Id))
            {
                // the pilot is set to use the actual pilotdef object in datamanager!
                // need to make sure that this pilot has it's own unique pilot def before we modify it
                pilot.ForceRefreshDef();
            }

            var pilotDef = pilot.pilotDef;
            var reloadAbilities = false;

            reloadAbilities |= UpdateAbilitiesFromTree(pilotDef);
            reloadAbilities |= UpdateAbilitiesFromTags(pilotDef);

            if (pilot.Team != null)
            {
                reloadAbilities = UpdateAbilitiesFromFaction(pilotDef, pilot.Team.FactionValue) | reloadAbilities;

                if (pilot.Team.TeamController == TeamController.Computer)
                    reloadAbilities |= SwapAIAbilities(pilotDef);
            }

            if (reloadAbilities)
            {
                if (pilotDef.AbilityDefs != null)
                    pilotDef.AbilityDefs.Clear();

                if (pilotDef.DataManager == null)
                    pilotDef.DataManager = dataManager;

                pilotDef.ForceRefreshAbilityDefs();
            }
        }

        private static bool UpdateAbilitiesFromTree(PilotDef pilotDef)
        {
            if (pilotDef.abilityDefNames == null)
                return false;

            var matchingAbilities = new List<string>();
            var missingAbilities = new List<string>();

            CheckAbilitiesFromProgression(pilotDef.abilityDefNames, constants.Progression.GunnerySkills, pilotDef.SkillGunnery, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgression(pilotDef.abilityDefNames, constants.Progression.PilotingSkills, pilotDef.SkillPiloting, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgression(pilotDef.abilityDefNames, constants.Progression.GutsSkills, pilotDef.SkillGuts, missingAbilities, matchingAbilities);
            CheckAbilitiesFromProgression(pilotDef.abilityDefNames, constants.Progression.TacticsSkills, pilotDef.SkillTactics, missingAbilities, matchingAbilities);

            var reloadAbilities = false;
            var extraAbilities = pilotDef.abilityDefNames.Except(matchingAbilities).ToList();

            if (GetPrimaryAbilitiesForPilot(dataManager, pilotDef).Count > 3)
                HBSLog.Log($"{pilotDef.Description.Id}: Has too many primary abilities -- not doing anything about it");

            // remove abilities that don't exist anymore
            foreach (var abilityName in extraAbilities)
            {
                if (!Settings.IgnoreAbilities.Exists(x => abilityName.StartsWith(x)) &&
                    ((Settings.RemoveNonTreeAbilities && !progressionAbilities.Contains(abilityName))
                        || !HasAbilityDef(dataManager, abilityName)))
                {
                    HBSLog.Log($"{pilotDef.Description.Id}: Removing '{abilityName}'");
                    pilotDef.abilityDefNames.RemoveAll(x => x == abilityName);
                    reloadAbilities = true;
                }
            }

            // add the missing abilities
            foreach (var abilityName in missingAbilities)
            {
                if (!Settings.IgnoreAbilities.Exists(x => abilityName.StartsWith(x)) &&
                    Settings.AddTreeAbilities && CanLearnAbility(dataManager, pilotDef, abilityName))
                {
                    HBSLog.Log($"{pilotDef.Description.Id}: Adding '{abilityName}' from tree");
                    pilotDef.abilityDefNames.Add(abilityName);
                    reloadAbilities = true;
                }
            }

            // find duplicates and remove them
            var duplicateAbilities = pilotDef.abilityDefNames.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key);
            foreach (var abilityName in duplicateAbilities)
            {
                if (!Settings.IgnoreAbilities.Exists(x => abilityName.StartsWith(x)) &&
                    Settings.RemoveDuplicateAbilities)
                {
                    HBSLog.Log($"{pilotDef.Description.Id}: Removing duplicate '{abilityName}'s");
                    pilotDef.abilityDefNames.RemoveAll(x => x == abilityName);
                    pilotDef.abilityDefNames.Add(abilityName);
                    reloadAbilities = true;
                }
            }

            return reloadAbilities;
        }

        private static bool UpdateAbilitiesFromTags(PilotDef pilotDef)
        {
            var reloadAbilities = false;

            foreach (var tag in pilotDef.PilotTags)
            {
                if (!Settings.TagAbilities.ContainsKey(tag))
                    continue;

                foreach (var abilityName in Settings.TagAbilities[tag])
                {
                    if (!HasAbilityDef(dataManager, abilityName))
                    {
                        HBSLog.LogWarning($"Tried to add {abilityName} from tag {tag}, but ability not found!");
                        continue;
                    }

                    if (!pilotDef.abilityDefNames.Contains(abilityName))
                    {
                        HBSLog.Log($"{pilotDef.Description.Id}: Adding '{abilityName}' from tag '{tag}'");
                        pilotDef.abilityDefNames.Add(abilityName);
                        reloadAbilities = true;
                    }
                }
            }

            return reloadAbilities;
        }

        private static bool UpdateAbilitiesFromFaction(PilotDef pilotDef, FactionValue faction)
        {
            var reloadAbilities = false;

            if (!Settings.FactionAbilities.ContainsKey(faction.Name))
                return false;

            foreach (var abilityName in Settings.FactionAbilities[faction.Name])
            {
                if (!HasAbilityDef(dataManager, abilityName))
                {
                    HBSLog.LogWarning($"Tried to add {abilityName} from faction {faction}, but ability not found!");
                    continue;
                }

                if (!pilotDef.abilityDefNames.Contains(abilityName))
                {
                    HBSLog.Log($"{pilotDef.Description.Id}: Adding '{abilityName}' from faction '{faction}'");
                    pilotDef.abilityDefNames.Add(abilityName);
                    reloadAbilities = true;
                }
            }

            return reloadAbilities;
        }

        private static bool SwapAIAbilities(PilotDef pilotDef)
        {
            var reloadAbilities = false;

            var addAbilities = new List<string>();
            var removeAbilities = new List<string>();

            foreach (var abilityName in pilotDef.abilityDefNames)
            {
                if (!Settings.SwapAIAbilities.ContainsKey(abilityName))
                    continue;

                var swappedAbilityName = Settings.SwapAIAbilities[abilityName];

                if (!HasAbilityDef(dataManager, swappedAbilityName))
                {
                    HBSLog.LogWarning($"Tried to swap {swappedAbilityName} for {abilityName} for AI, but ability not found!");
                    continue;
                }

                if (!pilotDef.abilityDefNames.Contains(swappedAbilityName))
                {
                    HBSLog.Log($"{pilotDef.Description.Id}: Swapping '{swappedAbilityName}' for '{abilityName}' for AI");
                    removeAbilities.Add(abilityName);
                    addAbilities.Add(swappedAbilityName);
                    reloadAbilities = true;
                }
            }

            foreach (var abilityName in removeAbilities)
                pilotDef.abilityDefNames.Remove(abilityName);

            foreach (var abilityName in addAbilities)
                pilotDef.abilityDefNames.Add(abilityName);

            return reloadAbilities;
        }
    }
}
