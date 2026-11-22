using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Vow.Updater;
using Vow.Shared;
class UpdaterTests {
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
 static void Reject(Action a,string message){bool failed=false;try{a();}catch{failed=true;}Check(failed,message);}
 static byte[] Zip(Payload p,string dllDir){using var m=new MemoryStream();using(var z=new ZipArchive(m,ZipArchiveMode.Create,true)){using(var s=new StreamWriter(z.CreateEntry("update.json").Open()))s.Write(JsonSerializer.Serialize(p));foreach(var n in Engine.Names){using var s=z.CreateEntry(n+".dll").Open();s.Write(File.ReadAllBytes(Path.Combine(dllDir,n+".dll")));}}return m.ToArray();}
 static void Main(string[] args){
  var repo=Path.GetFullPath(args[0]);var old=Path.GetFullPath(args[1]);var root=Path.Combine(Path.GetTempPath(),"ParryUpdateTest-"+Guid.NewGuid());
  string Dest(string n)=>Path.Combine(root,"BepInEx","plugins",n,n+".dll");
  var previous=new Payload{version="2.0.0",modules=Engine.Names.Select(n=>new Module{name=n,sha256=Engine.Hash(File.ReadAllBytes(Path.Combine(old,n+".dll")))}).ToArray()};
  var next=new Payload{version=JsonDocument.Parse(File.ReadAllText(Path.Combine(repo,"manifest.json"))).RootElement.GetProperty("version").GetString(),modules=Engine.Names.Select(n=>new Module{name=n,sha256=Engine.Hash(File.ReadAllBytes(Path.Combine(repo,"dist",n+".dll")))}).ToArray()};
  void Reset(){foreach(var n in Engine.Names){Directory.CreateDirectory(Path.GetDirectoryName(Dest(n)));File.Copy(Path.Combine(old,n+".dll"),Dest(n),true);}}
  Reset();var marker=Path.Combine(root,"BepInEx","patchers","HeroesVowParry","owner.json");Directory.CreateDirectory(Path.GetDirectoryName(marker));File.WriteAllText(marker,JsonSerializer.Serialize(previous));
  Check(Engine.OwnedInstallation(root),"Original standalone ownership");
  var cfg=Path.Combine(root,"BepInEx","settings.cfg");File.WriteAllText(cfg,"user-settings");var zip=Zip(next,Path.Combine(repo,"dist"));var hash=Engine.Hash(zip);
  Reject(()=>Engine.Apply(root,zip,new string('0',64),_=>{}),"Corrupt download accepted");Check(Engine.InstalledMatches(root,previous),"Corrupt download modified DLL");
  Reject(()=>Engine.Apply(root,zip,hash,_=>{},i=>{if(i==1)throw new IOException("Injected interruption");}),"First write failure escaped");Check(Engine.InstalledMatches(root,previous),"Rollback both modules");
  Reject(()=>Engine.Apply(root,zip,hash,_=>{},i=>{if(i==2)throw new IOException("Injected second write failure");}),"Second write failure escaped");Check(Engine.InstalledMatches(root,previous),"Second rollback");
  Engine.Apply(root,zip,hash,_=>{});Check(Engine.InstalledMatches(root,next),"Release update failed");Check(File.ReadAllText(cfg)=="user-settings","Settings replaced");
  Check(!Engine.OwnedInstallation(root),"Unrecorded DLL replaced other distribution");
  var cache=Path.Combine(root,"BepInEx","cache","HeroesVowParryUpdate");File.WriteAllText(Path.Combine(cache,"installed.json"),JsonSerializer.Serialize(new{hash,payload=next}));Check(Engine.OwnedInstallation(root),"Receipt ownership");
  Engine.Apply(root,zip,hash,_=>{},_=>throw new Exception("Unnecessary replacement"));count++;
  File.Delete(Dest("DodgeLimit"));Reject(()=>Engine.Apply(root,zip,hash,_=>{}),"Removed module restored");Check(!Engine.OwnedInstallation(root),"Removed ownership");Reset();
  var recovery=Path.Combine(root,"BepInEx","disabled-mods","ParryAuto-test");Directory.CreateDirectory(recovery);foreach(var n in Engine.Names)File.Copy(Dest(n),Path.Combine(recovery,n+".backup"));
  File.WriteAllText(Path.Combine(cache,"pending.json"),JsonSerializer.Serialize(new Journal{folder="ParryAuto-test",modules=previous.modules}));File.Copy(Path.Combine(repo,"dist","DuelCounters.dll"),Dest("DuelCounters"),true);
  Engine.Recover(root,_=>{});Check(Engine.InstalledMatches(root,previous),"Interrupted transaction not restored");Check(!File.Exists(Path.Combine(cache,"pending.json")),"Journal left behind");
  Reject(()=>Engine.SafePath(root,Path.Combine(root,"..","outside")),"Path escape allowed");
  var release=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{draft=false,prerelease=false,assets=new[]{new{name="HeroesVowParry-update.zip",browser_download_url=Engine.DownloadPrefix+"v"+next.version+"/HeroesVowParry-update.zip",digest="sha256:"+hash}}}));
  Check(Engine.ReadRelease(release).hash==hash,"GitHub metadata parse");Reject(()=>Engine.ReadRelease(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(release).Replace(Engine.DownloadPrefix,"https://untrusted.example/"))),"Foreign host allowed");
  using(var client=new HttpClient(new Handler())){
   Check(Engine.Get(client,"https://example.test",4,CancellationToken.None).GetAwaiter().GetResult().Length==4,"HTTP read");
   Reject(()=>Engine.Get(client,"https://example.test",3,CancellationToken.None).GetAwaiter().GetResult(),"Oversize accepted");
  }
  double Score(double wise,double soul)=>StrengthMetric.MartialScore(300,200,2,100,soul,3,wise);
  var baseline=Score(100,100);Check(Score(200,100)>baseline,"Intellect has no effect");Check(Score(100,200)>baseline,"Debate soul has no effect");Check(StrengthMetric.Compare(baseline,baseline)==1,"Equal matchup not neutral");
  for(int i=0;i<1000;i++){Check(double.IsFinite(Score(i*100,i*100)),"Finite strength");Check(StrengthMetric.Compare(Score(i+1,100),Score(i,100))>=1,"Monotonic intellect");}
  Console.WriteLine($"PASS {count} updater/strength checks. Fixture: {root}");
 }
 sealed class Handler:HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{1,2,3,4})});}
}
