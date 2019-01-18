using BattleTech;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AbilityRealizer
{
    internal class ModSettings
    {
        public bool AddTreeAbilities = true;
        public bool RemoveNonTreeAbilities = false;
        public List<string> IgnoreAbilities = new List<string>();
        public List<string> IgnorePilotsWithTags = new List<string>();
        public Dictionary<Faction, List<string>> FactionAbilities = new Dictionary<Faction, List<string>>();
        public Dictionary<string, List<string>> TagAbilities = new Dictionary<string, List<string>>();
        public Dictionary<string, string> SwapAIAbilities = new Dictionary<string, string>();

        public static ModSettings Parse(string json)
        {
            ModSettings settings;

            try
            {
                settings = JsonConvert.DeserializeObject<ModSettings>(json);
            }
            catch (Exception e)
            {
                Main.HBSLog.Log($"Reading settings failed: {e.Message}");
                settings = new ModSettings();
            }

            return settings;
        }
    }
}
