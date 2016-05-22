namespace LeagueSharp.SDK.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Resources;

    using LeagueSharp.Sandbox;
    using LeagueSharp.SDK.Properties;

    using Newtonsoft.Json;

    using NLog;

    using LogLevel = LeagueSharp.SDK.Enumerations.LogLevel;

    /// <summary>
    ///     Provides multi-lingual strings.
    /// </summary>
    public static class MultiLanguage
    {
        #region Static Fields

        /// <summary>
        ///     The translations
        /// </summary>
        private static Dictionary<string, string> translations = new Dictionary<string, string>();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Loads the translation.
        /// </summary>
        /// <param name="languageName">Name of the language.</param>
        /// <returns><c>true</c> if the operation succeeded, <c>false</c> otherwise false.</returns>
        public static bool LoadLanguage(string languageName)
        {
            try
            {
                var languageStrings =
                    new ResourceManager("LeagueSharp.SDK.Properties.Translations", typeof(Resources).Assembly).GetString(
                        languageName + "Json");

                if (string.IsNullOrEmpty(languageStrings))
                {
                    return false;
                }

                translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(languageStrings);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Fatal(ex);
                return false;
            }
        }

        /// <summary>
        ///     judge the select language
        /// </summary>
        public static void LoadTranslation()
        {
            try
            {
                var selectLanguage = SandboxConfig.SelectedLanguage;

                if (selectLanguage == "Chinese")
                {
                    LoadLanguage("Chinese");
                }
                else if (selectLanguage == "Traditional-Chinese")
                {
                    LoadLanguage("TraditionalChinese");
                }
                else
                {
                    // ignore
                }
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Fatal(ex);
            }
        }

        /// <summary>
        ///     Translates the text into the loaded language.
        /// </summary>
        /// <param name="textToTranslate">The text to translate.</param>
        /// <returns>System.String.</returns>
        public static string Translation(string textToTranslate)
        {
            var textToTranslateToLower = textToTranslate.ToLower();

            return translations.ContainsKey(textToTranslateToLower)
                       ? translations[textToTranslateToLower]
                       : (translations.ContainsKey(textToTranslate) ? translations[textToTranslate] : textToTranslate);
        }

        #endregion
    }
}
