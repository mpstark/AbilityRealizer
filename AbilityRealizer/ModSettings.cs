using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AbilityRealizer
{
    internal class ModSettings
    {
        public List<string> IgnoreAbilities = new List<string>();
        public bool AddTreeAbilities = true;
        public bool RemoveNonTreeAbilities = false;

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
