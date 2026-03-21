using System.Runtime.InteropServices;
using AOT;
using UnityEngine.Events;

public static class voip {
    
    public static UnityAction<byte[]> turret;
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void fnPtr(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        byte[] data,
        int numFrames
    );

    [MonoPInvokeCallback(typeof(fnPtr))]
    private static void callbackDef(byte[] data, int numFrames) {
        turret(data);
    }
    
    // GC bypass
    private static fnPtr callback = callbackDef;
    
    [DllImport("voip", EntryPoint = "init")]
    private static extern void init(fnPtr callbackParam);

    public static void init() {
        init(callback);
    }
    
    [DllImport("voip", EntryPoint = "destroy")]
    public static extern void destroy();
    
    [DllImport("voip", EntryPoint = "start")]
    public static extern void start();
    
    [DllImport("voip", EntryPoint = "stop")]
    public static extern void stop();
    
}