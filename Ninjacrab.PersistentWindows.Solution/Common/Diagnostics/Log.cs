using System;
using System.IO;
using System.Diagnostics;

namespace PersistentWindows.Common.Diagnostics
{
    public class Log
    {
        static EventLog eventLog;
        public static bool silent = false;
        static bool registered = false;
        public static void Init()
        {
            eventLog = new EventLog();
            string app_name = System.Windows.Forms.Application.ProductName;
            try
            {
                if (!EventLog.SourceExists(app_name))
                {
                    // CreateEventSource requires administrative privileges
                    EventLog.CreateEventSource(app_name, "Application");
                    Console.WriteLine($"Created Event Source '{app_name}'. Please restart the application for changes to take full effect.");
                    // Note: If you create a new source for a custom log, you might need to restart the computer for changes to take full effect in the Event Viewer.
                }
                registered = true;
                eventLog.Source = app_name;
            }
            catch (Exception)
            {
                eventLog.Source = "Application";
            }
        }

        public static void Exit()
        {
            if (eventLog != null)
                eventLog.Close();
        }

        /// <summary>
        /// 寫入 Windows 事件記錄。
        ///
        /// 記錄本身絕不能讓程式中斷：Init() 未被呼叫、事件來源未註冊或事件記錄已滿時，
        /// 這裡一律安靜略過，否則 catch 區塊中的記錄呼叫會再拋一次例外，
        /// 反而蓋掉原始錯誤並導致行程結束。
        /// </summary>
        private static void WriteEntrySafe(string message, int eventId)
        {
            var log = eventLog;
            if (log == null)
                return;

            try
            {
                if (!registered)
                {
                    int index = message.IndexOf("::");
                    if (index >= 0)
                        message = message.Substring(index + 3);
                    message = System.Windows.Forms.Application.ProductName + ": " + message;
                }

                log.WriteEntry(message, EventLogEntryType.Information, eventId, 0);
            }
            catch (Exception)
            {
                // 無法寫入事件記錄時安靜略過
            }
        }

        /// <summary>
        /// Occurs when something is logged. STATIC EVENT!
        /// </summary>

        public static void Trace(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Info(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Error(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
            if (message.Contains("Cannot create a file when that file already exists"))
            {
                // ignore trivial error
                return;
            }

            if (message.Contains("Access is denied"))
            {
                // ignore window move failure due to lack of admin privilege
                return;
            }

#if DEBUG
            Console.Write(message);
#endif
            WriteEntrySafe(message, 9999);
        }

        public static void Event(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
#if DEBUG
            Console.Write(message);
#endif
            WriteEntrySafe(message, 9990);
        }

        /// <summary>
        /// Since string.Format doesn't like args being null or having no entries.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        private static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return "\n";
            }

            bool arg_null = args.Length == 0;
            if (!registered)
            return arg_null ? $"{DateTime.Now} :: " + format + "\n":
                $"{DateTime.Now} :: " + string.Format(format, args) + "\n";

            return arg_null ? format + "\n":
                string.Format(format, args) + "\n";
        }

    }
}
