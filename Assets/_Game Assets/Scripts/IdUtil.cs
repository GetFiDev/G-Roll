public static class IdUtil
{
    public static string NormalizeId(string id)
        => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim().ToLowerInvariant();
}