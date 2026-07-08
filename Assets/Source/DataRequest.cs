public static class DataRequest
{
    public static string Pending;

    public static bool Has => !string.IsNullOrEmpty(Pending);

    public static string Consume()
    {
        string p = Pending;
        Pending = null;
        return p;
    }
}
