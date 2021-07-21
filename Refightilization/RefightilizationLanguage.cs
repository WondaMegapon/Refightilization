using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using SimpleJSON;

namespace Wonda
{
    class RefightilizationLanguage
    {
        public List<string> ReviveMessages { get; set; }
        public List<string> RevengeMessages { get; set; }

        public RefightilizationLanguage()
        {
            ReviveMessages = new List<string>();
            RevengeMessages = new List<string>();

            string currLang = Language.currentLanguageName;
            JSONNode lang = JSON.Parse(System.IO.File.ReadAllText(System.Reflection.Assembly.GetExecutingAssembly().Location.Replace("Refightilization.dll", "") + "LanguageResource.json"));

            if(lang[currLang] == null) currLang = "en";

            ConvertArrayToStringList(lang[currLang]["reviveMessages"].AsArray, ReviveMessages);
            ConvertArrayToStringList(lang[currLang]["revengeMessages"].AsArray, RevengeMessages);
        }

        private void ConvertArrayToStringList(JSONArray input, List<string> output)
        {
            for (var i = 0; i < input.Count; i++)
            {
                output.Add(input[i].Value);
            }
        }
    }
}
