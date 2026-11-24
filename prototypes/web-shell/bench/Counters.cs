using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Read type counts on x64 from processes in the launched lab tree. Never query object names.
public static class Counters {
    private delegate bool WindowVisitor(IntPtr window, IntPtr unused);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr unused);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    public static long[] VisibleWindows(int pid) {
        var windows = new List<long>();
        EnumWindows((window, unused) => {
            GetWindowThreadProcessId(window, out uint owner);
            // Count named user-facing surfaces. Tao/WebView2 can also expose an untitled helper.
            if (owner == pid && IsWindowVisible(window) && GetWindowTextLength(window) > 0)
                windows.Add(window.ToInt64());
            return true;
        }, IntPtr.Zero);
        windows.Sort();
        return windows.ToArray();
    }
    [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr process,int flags);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process,int kind,IntPtr data,int bytes,out int used);
    [DllImport("ntdll.dll")] static extern int NtQueryObject(IntPtr h,int kind,IntPtr data,int bytes,out int used);
    [DllImport("kernel32.dll")] static extern bool DuplicateHandle(IntPtr source,IntPtr original,IntPtr destination,out IntPtr copy,uint rights,bool inherit,uint options);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr copy);
    static readonly Dictionary<int,string> Types = new Dictionary<int,string>();
    public static int[] Count(IntPtr process){
        int length=1024*1024;
        IntPtr buffer=Marshal.AllocHGlobal(length), description=Marshal.AllocHGlobal(4096);
        try{
            int used;
            if(IntPtr.Size!=8 || NtQueryInformationProcess(process,51,buffer,length,out used)<0)return new[]{-1,-1};
            long count=Marshal.ReadInt64(buffer);
            if(count<0 || count>(length-16)/40)return new[]{-1,-1};
            int composition=0,events=0;
            for(int i=0;i<count;i++){
                IntPtr entry=IntPtr.Add(buffer,16+i*40);
                int type=Marshal.ReadInt32(entry,28);
                string name;
                if(!Types.TryGetValue(type,out name)){
                    IntPtr copy;
                    if(!DuplicateHandle(process,Marshal.ReadIntPtr(entry),new IntPtr(-1),out copy,0,false,0))continue;
                    try{
                        if(NtQueryObject(copy,2,description,4096,out used)<0)continue;
                        name=Marshal.PtrToStringUni(Marshal.ReadIntPtr(description,8), (ushort)Marshal.ReadInt16(description)/2);
                        Types[type]=name;
                    }finally{CloseHandle(copy);}
                }
                if(name=="Composition")composition++;
                if(name=="Event")events++;
            }
            return new[]{composition,events};
        }finally{Marshal.FreeHGlobal(buffer);Marshal.FreeHGlobal(description);}
    }
}
