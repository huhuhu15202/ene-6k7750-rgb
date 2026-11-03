// ENE 6K7750 Windows implementation based on TeamLocaBit/ene-6k7750-rgb.
// MIT license: see ../LICENSE. No fan-speed registers are accessed.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MaxsunRgb {
    internal sealed class EneDevice : IDisposable {
        private static readonly Guid DeviceInterface = new Guid("6987B675-8CBC-4BD3-A557-C627CEA223DC");
        private const string HardwareId = "vid_0cf2&pid_7750&mi_00";
        private const int LedBase = 0xE300;
        private const int Chunk = 96;
        private SafeFileHandle file;
        private IntPtr usb;
        public int[] Counts { get; private set; }

        [StructLayout(LayoutKind.Sequential, Pack=1)]
        private struct SetupPacket { public byte Type, Request; public ushort Value, Index, Length; }
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_List_SizeW(out uint length, ref Guid guid, string deviceId, uint flags);
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_ListW(ref Guid guid, string deviceId, [Out] char[] buffer, uint length, uint flags);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("winusb.dll", SetLastError=true)]
        private static extern bool WinUsb_Initialize(SafeFileHandle handle, out IntPtr usbHandle);
        [DllImport("winusb.dll", SetLastError=true)]
        private static extern bool WinUsb_Free(IntPtr usbHandle);
        [DllImport("winusb.dll", SetLastError=true)]
        private static extern bool WinUsb_ControlTransfer(IntPtr handle, SetupPacket setup, byte[] data, uint length, out uint transferred, IntPtr overlapped);

        public static string FindPath() {
            Guid guid = DeviceInterface; uint length;
            uint cr = CM_Get_Device_Interface_List_SizeW(out length, ref guid, null, 0);
            if (cr != 0) throw new IOException("无法枚举 WinUSB 接口，错误 " + cr);
            char[] buffer = new char[Math.Max(2, (int)length)];
            cr = CM_Get_Device_Interface_ListW(ref guid, null, buffer, (uint)buffer.Length, 0);
            if (cr != 0) throw new IOException("无法读取 WinUSB 接口，错误 " + cr);
            List<string> paths = new List<string>();
            foreach (string s in new string(buffer).Split('\0'))
                if (s.IndexOf(HardwareId, StringComparison.OrdinalIgnoreCase) >= 0) paths.Add(s);
            if (paths.Count != 1) throw new IOException("需要恰好一个 ENE 0CF2:7750 MI_00 灯控设备，实际找到 " + paths.Count);
            return paths[0];
        }
        public EneDevice() {
            string path = FindPath();
            file = CreateFileW(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000080, IntPtr.Zero);
            if (file.IsInvalid) { int e = Marshal.GetLastWin32Error(); file.Dispose(); throw new Win32Exception(e, "无法打开 MS-ARGB；请关闭其他灯控程序"); }
            if (!WinUsb_Initialize(file, out usb)) { int e = Marshal.GetLastWin32Error(); file.Dispose(); throw new Win32Exception(e, "WinUSB 初始化失败"); }
            try {
                int count = Read(0xE0A1, 1)[0];
                if (count < 1 || count > 8) throw new IOException("灯控分组数异常：" + count);
                Counts = new int[count]; int total = 0;
                for (int g = 0; g < count; g++) {
                    byte[] v = Read(0xE0A2 + 2*g, 2);
                    Counts[g] = v[0]*256+v[1];
                    if (Counts[g] < 1 || Counts[g] > 512) throw new IOException("灯控组长度异常：" + Counts[g]);
                    total += Counts[g];
                }
                if (total > 1024) throw new IOException("灯珠总数异常：" + total);
            } catch { Dispose(); throw; }
        }
        private byte[] Transfer(bool read, int reg, byte[] data) {
            if (reg < 0 || reg > 0xFFFF || data.Length < 1 || data.Length > Chunk) throw new ArgumentOutOfRangeException("transfer");
            SetupPacket packet = new SetupPacket { Type=(byte)(read ? 0xC0 : 0x40), Request=(byte)(read ? 0x81 : 0x80), Value=(ushort)(reg >> 16), Index=(ushort)reg, Length=(ushort)data.Length };
            uint transferred;
            if (!WinUsb_ControlTransfer(usb, packet, data, (uint)data.Length, out transferred, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "USB 控制传输失败，寄存器 " + reg.ToString("X4"));
            if (transferred != data.Length) throw new IOException("USB 短传输：" + transferred + "/" + data.Length);
            return data;
        }
        private byte[] Read(int reg, int n) { return Transfer(true, reg, new byte[n]); }
        private void Write(int reg, byte[] data) { Transfer(false, reg, data); }
        private int Base(int g) {
            if (g < 0 || g >= Counts.Length) throw new ArgumentOutOfRangeException("group");
            int n=0; for (int i=0;i<g;i++) n+=Counts[i]; return LedBase + n*3;
        }
        public byte[] ReadGroup(int g) {
            int len=Counts[g]*3; byte[] result=new byte[len]; int b=Base(g);
            for(int i=0;i<len;i+=Chunk) { byte[] part=Read(b+i, Math.Min(Chunk,len-i)); Array.Copy(part,0,result,i,part.Length); }
            return result;
        }
        public byte[] ReadEffect(int g) {
            if (g<0 || g>=Counts.Length) throw new ArgumentOutOfRangeException("group");
            byte[] a=Read(0xE021+16*g,2), b=Read(0xE02F+16*g,1);
            return new byte[] { a[0],a[1],b[0] };
        }
        public bool VerifySameBytes(int g) {
            int address = Base(g); byte[] before = Read(address, 3);
            Write(address, before); byte[] after = Read(address, 3);
            return before[0] == after[0] && before[1] == after[1] && before[2] == after[2];
        }
        public void WriteGroup(int g, byte[] raw) {
            if(raw == null || raw.Length != Counts[g]*3) throw new ArgumentException("灯效数据长度不匹配");
            int b=Base(g);
            for(int i=0;i<raw.Length;i+=Chunk) { int len=Math.Min(Chunk,raw.Length-i); byte[] part=new byte[len]; Array.Copy(raw,i,part,0,len); Write(b+i,part); }
            Write(0xE021+16*g,new byte[] {1}); Write(0xE022+16*g,new byte[] {0x1b}); Write(0xE02F+16*g,new byte[] {1});
        }
        public void Dispose() { if(usb != IntPtr.Zero) { WinUsb_Free(usb); usb=IntPtr.Zero; } if(file != null) { file.Dispose(); file=null; } }
    }
}
