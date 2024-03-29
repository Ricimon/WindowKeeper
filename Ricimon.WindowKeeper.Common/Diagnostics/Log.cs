﻿using NLog;
using NLog.Config;
using NLog.Targets;
using System;

namespace Ricimon.WindowKeeper.Common.Diagnostics
{
    // adapted from https://archive.codeplex.com/?p=persistentwindows

    public class Log
    {
        static Log()
        {
            var config = new LoggingConfiguration();

            // Create targets and add them to the configuration
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            // Set target properties
            consoleTarget.Layout = @"${date:format=HH\\:mm\\:ss} ${logger} ${message}";

            // Define rules
            var rule1 = new LoggingRule("*", LogLevel.Trace, consoleTarget);
            config.LoggingRules.Add(rule1);

#if DEBUG
            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            fileTarget.FileName = "${basedir}/WindowKeeper.Log";
            fileTarget.Layout = "${date:format=HH\\:mm\\:ss} ${logger} ${message}";

            var rule2 = new LoggingRule("*", LogLevel.Trace, fileTarget);
            config.LoggingRules.Add(rule2);
#endif

            // Activate the configuration
            LogManager.Configuration = config;
        }

        /// <summary>
        /// Occurs when something is logged. STATIC EVENT!
        /// </summary>
        public static event Action<LogLevel, string> LogEvent;

        private static Logger _logger;
        private static Logger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = LogManager.GetLogger("Logger");
                }
                return _logger;
            }
        }

        private static void RaiseLogEvent(LogLevel level, string message)
        {
            // something about how this should write a new logging target?
            LogEvent?.Invoke(level, message);
        }

        public static void Trace(string format, params object[] args)
        {
            var message = Format(format, args);
            Logger.Trace(message);
            RaiseLogEvent(LogLevel.Trace, message);
        }

        public static void Info(string format, params object[] args)
        {
            var message = Format(format, args);
            Logger.Info(message);
            RaiseLogEvent(LogLevel.Info, message);
        }

        public static void Error(string format, params object[] args)
        {
            var message = Format(format, args);
            Logger.Error(message);
            RaiseLogEvent(LogLevel.Error, message);
        }

        /// <summary>
        /// Since string.Format doesn't like args being null or having no entries
        /// </summary>
        /// <param name="format">The format</param>
        /// <param name="args">The args</param>
        /// <returns></returns>
        private static string Format(string format, params object[] args)
        {
            return args == null || args.Length == 0 ? format : string.Format(format, args);
        }
    }
}
