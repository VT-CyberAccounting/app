public static class BackendAuth
{
    private static volatile string _token;

    public static bool Has => !string.IsNullOrEmpty(_token);

    public static string Token => _token;

    public static void SetToken(string token) => _token = token;

    public static void Clear() => _token = null;
}
