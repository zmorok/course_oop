namespace DAL.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string? s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length <= maxLength) return s;
            return s.Substring(0, maxLength) + "…";
        }
    }
}
