// <copyright file="Bootstrap.cs" company="LeagueSharp">
//    Copyright (c) 2015 LeagueSharp.
// 
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
// 
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
// 
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/
// </copyright>

namespace LeagueSharp.SDK
{
    using System.Globalization;
    using System.Security.Permissions;
    using System.Threading;

    using LeagueSharp.SDK.Enumerations;
    using LeagueSharp.SDK.UI;
    using LeagueSharp.SDK.UI.Skins;
    using LeagueSharp.SDK.Utils;

    /// <summary>
    ///     Bootstrap is an initialization pointer for the AppDomainManager to initialize the library correctly once loaded in
    ///     game.
    /// </summary>
    public class Bootstrap
    {
        #region Static Fields

        /// <summary>
        ///     Indicates whether the bootstrap has been initialized.
        /// </summary>
        private static bool initialized;

        #endregion

        #region Public Methods and Operators

        
        /// <summary>
        /// Initializes the whole SDK. It is safe to call in your code at any point.
        /// </summary>
        /// <param name="args">Not currently used or needed.</param>
        /// <returns>true if SDK is loaded, false if it is not</returns>
        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public static bool Init(string[] args = null)
        {
            if (initialized)
            {
                return true;
            }

            initialized = true;

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Initial notification.
            Logging.Write()(LogLevel.Info, "[-- SDKEx Bootstrap Loading --]");

            // Load Resource Content.
            ResourceLoader.Initialize();
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] Resources Initialized.");

            // Load GameObjects.
            GameObjects.Initialize();
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] GameObjects Initialized.");

            // Create L# menu
            Variables.LeagueSharpMenu = new Menu("LeagueSharp", "LeagueSharp", true).Attach();
            MenuCustomizer.Initialize(Variables.LeagueSharpMenu);
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] LeagueSharp Menu Created.");

            // Load the Orbwalker
            Variables.Orbwalker = new Orbwalker(Variables.LeagueSharpMenu);
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] Orbwalker Initialized.");

            // Load the TargetSelector.
            Variables.TargetSelector = new TargetSelector(Variables.LeagueSharpMenu);
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] TargetSelector Initialized.");

            // Load the Notifications
            Notifications.Initialize(Variables.LeagueSharpMenu);
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] Notifications Initialized.");

            // Load the ThemeManager
            ThemeManager.Initialize(Variables.LeagueSharpMenu);
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] ThemeManager Initialized.");

            // Load Damages.
            Damage.Initialize();
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] Damage Library Initialized.");

            // Load Language
            MultiLanguage.LoadTranslation();
            Logging.Write()(LogLevel.Info, "[SDKEx Bootstrap] MultiLanguage Initialized");

            // Final notification.
            Logging.Write()(LogLevel.Info, "[-- SDKEx Bootstrap Loading --]");

            // Tell the developer everything succeeded
            return initialized;
        }

        #endregion
    }
}