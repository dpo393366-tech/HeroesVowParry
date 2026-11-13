using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace Vow.Counters;

// Capture strings on the game thread; all file inspection/rotation/writes run
// on one background worker. Never call Unity/IL2CPP from that worker.
internal static class CombatLog {
    static readonly object gate=new();
    static readonly Queue<(string path,long limit,string line)> queue=new();
    static bool running;
    internal static void Write(string path,long limit,string line){
        lock(gate){
            if(queue.Count>=2048)return; // optional diagnostics cannot stall combat
            queue.Enqueue((path,limit,line));
            if(running)return;running=true;Task.Run(Drain);
        }
    }
    static void Drain(){
        while(true){
            var batch=new Dictionary<string,(long limit,StringBuilder text)>();
            lock(gate){
                if(queue.Count==0){running=false;return;}
                while(queue.Count>0){var x=queue.Dequeue();
                    if(!batch.TryGetValue(x.path,out var b))b=(x.limit,new StringBuilder());
                    b.text.Append(x.line);batch[x.path]=b;}
            }
            foreach(var x in batch)try{
                if(File.Exists(x.Key)&&new FileInfo(x.Key).Length>x.Value.limit)File.Move(x.Key,x.Key+".previous",true);
                File.AppendAllText(x.Key,x.Value.text.ToString());
            }catch{/* Diagnostic IO failure must not change game state. */}
        }
    }
}
