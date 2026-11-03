using System;
using System.IO;
using System.Web.Script.Serialization;
namespace MaxsunRgb {
    internal sealed class Snapshot {
        public string Device {get;set;}
        public string CapturedAt {get;set;}
        public int[] Counts {get;set;}
        public string[] LedRamHex {get;set;}
        public string[] EffectHex {get;set;}
    }
    internal static class Snapshots {
        public static Snapshot Capture(EneDevice dev) {
            var s=new Snapshot { Device="0CF2:7750:MI_00", CapturedAt=DateTime.Now.ToString("o"), Counts=dev.Counts, LedRamHex=new string[dev.Counts.Length], EffectHex=new string[dev.Counts.Length] };
            for(int g=0;g<dev.Counts.Length;g++) { s.LedRamHex[g]=Hex(dev.ReadGroup(g)); s.EffectHex[g]=Hex(dev.ReadEffect(g)); }
            return s;
        }
        public static string Hex(byte[] b) { return BitConverter.ToString(b).Replace("-", ""); }
        public static byte[] Unhex(string text) {
            if(text==null || text.Length%2!=0) throw new InvalidDataException("备份数据无效");
            byte[] b=new byte[text.Length/2]; for(int i=0;i<b.Length;i++) b[i]=Convert.ToByte(text.Substring(i*2,2),16); return b;
        }
        public static void SaveNew(string path, Snapshot s) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string data=new JavaScriptSerializer().Serialize(s);
            using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) using(var w=new StreamWriter(f,new System.Text.UTF8Encoding(false))) w.Write(data);
        }
        public static Snapshot Load(string path) { return new JavaScriptSerializer().Deserialize<Snapshot>(File.ReadAllText(path)); }
        public static void Validate(EneDevice dev, Snapshot s) {
            if(s==null || s.Device!="0CF2:7750:MI_00" || s.Counts==null || s.LedRamHex==null || s.Counts.Length!=dev.Counts.Length || s.LedRamHex.Length!=dev.Counts.Length) throw new InvalidDataException("备份与设备不匹配");
            for(int g=0;g<s.Counts.Length;g++) if(s.Counts[g]!=dev.Counts[g] || Unhex(s.LedRamHex[g]).Length!=dev.Counts[g]*3) throw new InvalidDataException("备份分组长度不匹配");
        }
    }
    internal static class Probe {
        public static int Main(string[] args) {
            try {
                if(args.Length<1 || (args[0]!="info" && args[0]!="backup" && args[0]!="verify-same" && args[0]!="test-red")) {Console.Error.WriteLine("Usage: probe info | backup PATH | verify-same PATH | test-red PATH"); return 2;}
                using(var dev=new EneDevice()) {
                    Console.WriteLine("Device: 0CF2:7750 MI_00");
                    Console.WriteLine("Groups: " + string.Join(", ",dev.Counts));
                    if(args[0]=="backup") {if(args.Length!=2) throw new ArgumentException("backup requires PATH"); Snapshots.SaveNew(args[1],Snapshots.Capture(dev)); Console.WriteLine("Backup: " + Path.GetFullPath(args[1]));}
                    if(args[0]=="verify-same") {if(args.Length!=2) throw new ArgumentException("verify-same requires backup PATH"); Snapshots.SaveNew(args[1],Snapshots.Capture(dev)); if(!dev.VerifySameBytes(0)) throw new IOException("readback mismatch"); Console.WriteLine("Same-value write and readback: OK; no effect strobe sent.");}
                    if(args[0]=="test-red") {
                        if(args.Length!=2) throw new ArgumentException("test-red requires backup PATH");
                        var before=Snapshots.Capture(dev); Snapshots.SaveNew(args[1],before);
                        for(int g=0;g<Math.Min(2,dev.Counts.Length);g++) {
                            byte[] raw=new byte[dev.Counts[g]*3]; for(int i=0;i<raw.Length;i+=3) raw[i]=255;
                            dev.WriteGroup(g,raw);
                            if(Snapshots.Hex(dev.ReadGroup(g))!=Snapshots.Hex(raw)) throw new IOException("group " + g + " readback mismatch");
                            Console.WriteLine("Group " + g + ": red RAM written, strobe sent, readback matched.");
                        }
                        Console.WriteLine("Visual confirmation required. One-shot may time out; backup: " + args[1]);
                    }
                }
                return 0;
            } catch(Exception ex) {Console.Error.WriteLine(ex.ToString()); return 1;}
        }
    }
}
