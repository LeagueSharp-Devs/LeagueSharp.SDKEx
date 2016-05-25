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
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Security.Permissions;
    using System.Threading;

    using LeagueSharp.SDK.UI;
    using LeagueSharp.SDK.UI.Skins;
    using LeagueSharp.SDK.Utils;

    using NLog;
    using NLog.Config;
    using NLog.Targets;

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
        ///     Initializes the whole SDK. It is safe to call in your code at any point.
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

            // Setup logging
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget
                                 {
                                     FileName = Constants.LogDirectory + "\\${shortdate}.log",
                                     Layout = "${longdate} ${uppercase:${level}} ${message}",
                                     ReplaceFileContentsOnEachWrite = true
                                 };

            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

            var coloredConsoleTarget = new ColoredConsoleTarget
                                           {
                                               UseDefaultRowHighlightingRules = false,
                                               Layout =
                                                   "${longdate}|${pad:padding=5:inner=${level:uppercase=true}}| ${callsite:className=true:fileName=false:includeSourcePath=false:methodName=false:cleanNamesOfAnonymousDelegates=false:skipFrames=-1}: ${message}",
                                           };

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Trace", ForegroundColor = ConsoleOutputColor.DarkGray });

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Debug", ForegroundColor = ConsoleOutputColor.Gray });

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Info", ForegroundColor = ConsoleOutputColor.White });

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Warn", ForegroundColor = ConsoleOutputColor.Yellow });

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Error", ForegroundColor = ConsoleOutputColor.Red });

            coloredConsoleTarget.RowHighlightingRules.Add(
                new ConsoleRowHighlightingRule()
                    { Condition = "Level == LogLevel.Fatal", ForegroundColor = ConsoleOutputColor.Red });

            config.AddTarget("coloredConsole", coloredConsoleTarget);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, coloredConsoleTarget);

            LogManager.Configuration = config;

            var logger = LogManager.GetCurrentClassLogger();

            // Log unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    var exception = eventArgs.ExceptionObject as Exception;

                    // Check if exception came from us
                    if (exception != null && exception.Source.Equals(Assembly.GetExecutingAssembly().FullName))
                    {
                        // Get the logger from the class that threw the exception and log it
                        LogManager.GetCurrentClassLogger(new StackTrace().GetFrame(1).GetMethod().DeclaringType)
                            .Fatal(exception);
                    }
                };

            // Initial notification.
            logger.Info("SDKEx Loading");

            // Load Resource Content.
            ResourceLoader.Initialize();
            logger.Info("Resources Initialized.");

            // Load GameObjects.
            GameObjects.Initialize();
            logger.Info("GameObjects Initialized.");

            // Create L# menu
            Variables.LeagueSharpMenu = new Menu("LeagueSharp", "LeagueSharp", true).Attach();
            MenuCustomizer.Initialize(Variables.LeagueSharpMenu);
            logger.Info("LeagueSharp Menu Created.");

            // Load the Orbwalker
            Variables.Orbwalker = new Orbwalker(Variables.LeagueSharpMenu);
            logger.Info("Orbwalker Initialized.");

            // Load the TargetSelector.
            Variables.TargetSelector = new TargetSelector(Variables.LeagueSharpMenu);
            logger.Info("TargetSelector Initialized.");

            // Load the Notifications
            Notifications.Initialize(Variables.LeagueSharpMenu);
            logger.Info("Notifications Initialized.");

            // Load the ThemeManager
            ThemeManager.Initialize(Variables.LeagueSharpMenu);
            logger.Info("ThemeManager Initialized.");

            // Load Damages.
            Damage.Initialize();
            logger.Info("Damage Library Initialized.");

            // Load Language
            MultiLanguage.LoadTranslation();
            logger.Info("Translations Initialized.");

            // Final notification.
            logger.Info($"SDKEx Version {Variables.KitVersion} Loaded!");

            // Tell the developer everything succeeded
            return initialized;
        }

        #endregion
    }
}