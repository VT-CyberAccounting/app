using System.Runtime.InteropServices;

public static class spkr {
    
    [DllImport("spkr", EntryPoint = "init")]
    public static extern void init();
    
    [DllImport("spkr", EntryPoint = "destroy")]
    public static extern void destroy();
    
    [DllImport("spkr", EntryPoint = "start")]
    public static extern void start();
    
    [DllImport("spkr", EntryPoint = "stop")]
    public static extern void stop();
    
    [DllImport("spkr", EntryPoint = "play")]
    private static extern void play(
        [MarshalAs(UnmanagedType.LPArray)]
        byte[] data,
        int arrLength
    );
    
    public static void write(byte[] data) {
        play(data, data.Length);
    }
    
}