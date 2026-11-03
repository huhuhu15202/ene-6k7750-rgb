// Windows background controller, based on TeamLocaBit's MIT-licensed ENE protocol.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
namespace MaxsunRgb {
    internal static class Background {
        private static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string LogFile = Path.Combine(Root,"MaxsunRGB.log");
        private static void Log(string message) {
            try { if(File.Exists(LogFile) && new FileInfo(LogFile).Length > 262144) File.Move(LogFile,Path.Combine(Root,"MaxsunRGB-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log")); File.AppendAllText(LogFile,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+message+Environment.NewLine); } catch { }
        }
        [STAThread] public static void Main() {
            bool first;
            using(var mutex=new Mutex(true,"Local\\MaxsunRgbEne7750",out first)) {
                if(!first) return;
                bool backedUp=false, online=false; string previousError=null; var clock=Stopwatch.StartNew();
                Log("Background RGB started (groups 0,1; rainbow, brightness 75%).");
                while(true) {
                    try {
                        using(var dev=new EneDevice()) {
                            if(dev.Counts.Length!=5 || dev.Counts[0]!=128 || dev.Counts[1]!=128 || dev.Counts[2]!=1 || dev.Counts[3]!=1 || dev.Counts[4]!=6) throw new IOException("Unexpected ENE group layout; refusing to write.");
                            if(!backedUp) {
                                string folder=Path.Combine(Root,"backups"); string path=Path.Combine(folder,"background-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");
                                Snapshots.SaveNew(path,Snapshots.Capture(dev)); backedUp=true; Log("Initial LED RAM backup saved: "+path);
                            }
                            double seconds=clock.Elapsed.TotalSeconds;
                            double hue=(seconds/90.0*360.0)%360.0;
                            byte[] rgb=FromHue(hue,0.75);
                            for(int g=0;g<2;g++) {
                                byte[] raw=new byte[dev.Counts[g]*3];
                                for(int i=0;i<raw.Length;i+=3) {raw[i]=rgb[0];raw[i+1]=rgb[2];raw[i+2]=rgb[1];}
                                dev.WriteGroup(g,raw);
                            }
                        }
                        if(!online) {Log("Connected and sending frames.");online=true;previousError=null;}
                        Thread.Sleep(200);
                    } catch(Exception ex) {
                        online=false; string error=ex.GetType().Name+": "+ex.Message;
                        if(error!=previousError) {Log("Waiting for controller: "+error); previousError=error;}
                        Thread.Sleep(5000);
                    }
                }
            }
        }
        private static byte[] FromHue(double h,double brightness) {
            double x=1-Math.Abs((h/60)%2-1),r=0,g=0,b=0;
            if(h<60){r=1;g=x;} else if(h<120){r=x;g=1;} else if(h<180){g=1;b=x;} else if(h<240){g=x;b=1;} else if(h<300){r=x;b=1;} else {r=1;b=x;}
            return new byte[]{(byte)(r*brightness*255),(byte)(g*brightness*255),(byte)(b*brightness*255)};
        }
    }
}
