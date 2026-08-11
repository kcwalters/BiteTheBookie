using System.Text.RegularExpressions;

namespace BiteTheBookie.Services
{
    /// <summary>
    /// Helpers for extracting a canonical 11-character YouTube video ID from the
    /// various URL shapes admins might paste (watch, youtu.be, embed, shorts, live)
    /// or a bare ID.
    /// </summary>
    public static class YouTubeHelper
    {
        private static readonly Regex IdPattern = new(
            @"(?:youtu\.be/|youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/|v/))([A-Za-z0-9_-]{11})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BareIdPattern = new(
            @"^[A-Za-z0-9_-]{11}$",
            RegexOptions.Compiled);

        /// <summary>
        /// Returns the 11-character YouTube video ID, or null if one cannot be parsed.
        /// </summary>
        public static string? ExtractVideoId(string? urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
            {
                return null;
            }

            var input = urlOrId.Trim();

            if (BareIdPattern.IsMatch(input))
            {
                return input;
            }

            var match = IdPattern.Match(input);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        public static string EmbedUrl(string videoId) => $"https://www.youtube.com/embed/{videoId}";

        public static string WatchUrl(string videoId) => $"https://www.youtube.com/watch?v={videoId}";

        public static string ThumbnailUrl(string videoId) => $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";
    }
}
