using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SimpleJSON;

namespace Wonda
{
    class RefightilizationLanguage
    {
        public List<string> ReviveMessages { get; set; }
        public List<string> RevengeMessages { get; set; }
        public string ItemBlacklistWarning { get; set; }
        public string RiskOfOptionsDescription { get; set; }

        public RefightilizationLanguage()
        {
            // Initializing our lists
            ReviveMessages = new List<string>();
            RevengeMessages = new List<string>();
            ItemBlacklistWarning = "{0}'s {1} will be returned, later.";

            string currLang = Language.currentLanguageName;

            // Grabbing the file
            FileStream stream = File.Open(System.Reflection.Assembly.GetExecutingAssembly().Location.Replace("Refightilization.dll", "") + "LanguageResource.json", FileMode.Open, FileAccess.Read);
            StreamReader streamReader = new StreamReader(stream, Encoding.Unicode); // THE FILE MUST BE SAVED WITH ENCODING CODEPAGE 1200 OR EVERYTHING BREAKS.
            string streamRead = streamReader.ReadToEnd();

            // Parsing the text
            JSONNode lang = JSON.Parse(streamRead);

            // Defaulting to English if the language doesn't have characters.
            if(lang[currLang] == null) currLang = "en";

            // Adding the arrays to our lists.
            ConvertArrayToStringList(lang[currLang]["reviveMessages"].AsArray, ReviveMessages);
            ConvertArrayToStringList(lang[currLang]["revengeMessages"].AsArray, RevengeMessages);
            ItemBlacklistWarning = lang[currLang]["itemBlacklistWarning"];

            // Closing our stream.
            streamReader.Close();
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
