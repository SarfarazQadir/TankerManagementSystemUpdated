// Created by AntiGravity on 2026-07-18 08:15 Pakistan Standard Time
namespace TankerManagementSystem.Helpers
{
    /// <summary>
    /// Centralized helper for consistent DateTime handling across the application.
    /// All timestamps should use Pakistan Standard Time (PST, UTC+5).
    /// </summary>
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo PakistanTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

        /// <summary>
        /// Returns the current date and time in Pakistan Standard Time.
        /// Use this instead of DateTime.Now to avoid server-timezone mismatches.
        /// </summary>
        public static DateTime GetPakistanTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PakistanTimeZone);
        }

        /// <summary>
        /// Returns today's date in Pakistan Standard Time.
        /// </summary>
        public static DateTime GetPakistanToday()
        {
            return GetPakistanTime().Date;
        }
    }
}
