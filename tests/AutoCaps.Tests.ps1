param([string]$SourceDir='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
if(!$SourceDir){$SourceDir=Join-Path $repo 'src'}
$out=Join-Path $repo 'bin/auto-caps-tests'
New-Item -ItemType Directory -Force $out|Out-Null
$source=Get-Content (Join-Path $SourceDir 'DuelCounters.cs') -Raw
$methods=@('static float Relative','static float Odds','internal static bool IsPlayer')|ForEach-Object {
 $match=[regex]::Match($source,'(?m)^\s*'+[regex]::Escape($_)+'.*$')
 if(!$match.Success){throw "Missing production method $_"};$match.Value
}
if(([regex]::Matches($source,'Souls\(h\),Souls\(dh\),dh\)')).Count -ne 2){throw 'Legacy martial/debate victim routing missing'}
if(([regex]::Matches($source,'Souls\(victim.GetHero\(\)\),victim.GetHero\(\)\)')).Count -ne 2){throw 'Per-hit martial/debate victim routing missing'}
if(([regex]::Matches($source,'\?StrengthPressure.EnemyOdds\(')).Count -ne 4){throw 'Enemy odds routing changed'}
$harness=@'
using System;
class Entry { public float Value; public Entry(float v){Value=v;} }
class CLAGKDECFID { public int OJPECMNINNA; }
static class NEINKOOBJIH { public static CLAGKDECFID POMNMBGDDIN; }
static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b); }
class Test {
 static Entry baseline=new Entry(10),playerCeiling=new Entry(10),allyCeiling=new Entry(10);
 METHODS
 static int checks;
 static void Eq(float a,float b){checks++;if(Math.Abs(a-b)>.00001f)throw new Exception(a+" != "+b);}
 static void Main(){
  var hero=new CLAGKDECFID{OJPECMNINNA=1};var sameHero=new CLAGKDECFID{OJPECMNINNA=1};var ally=new CLAGKDECFID{OJPECMNINNA=2};NEINKOOBJIH.POMNMBGDDIN=hero;
  Eq(Odds(100,100,100,100,hero),.1f);Eq(Odds(100,100,100,100,ally),.1f);
  playerCeiling.Value=0;Eq(Odds(1,999,1,999,sameHero),0);Eq(Odds(1,999,1,999,ally),.1f);
  playerCeiling.Value=80;allyCeiling.Value=5;Eq(Odds(1,999,1,999,hero),.6988f);Eq(Odds(1,999,1,999,ally),.05f);
  NEINKOOBJIH.POMNMBGDDIN=null;Eq(Odds(1,999,1,999,ally),.05f);Eq(Odds(1,999,1,999,sameHero),.05f);
  NEINKOOBJIH.POMNMBGDDIN=hero;
  for(int cap=0;cap<=100;cap++){
   playerCeiling.Value=cap;allyCeiling.Value=100-cap;
   Eq(Odds(1,999,1,999,hero),Math.Min(cap,69.88f)*.01f);
   Eq(Odds(1,999,1,999,ally),Math.Min(100-cap,69.88f)*.01f);
   Eq(Odds(999,1,999,1,hero),0);Eq(Odds(999,1,999,1,ally),0);
  }
  Console.WriteLine("PASS: "+checks+" production automatic-parry odds/cap checks, plus four victim routes and unchanged enemy routes.");
 }
}
'@
$harness.Replace('METHODS',($methods -join "`n"))|Set-Content "$out/AutoCaps.cs" -Encoding UTF8
$refs=@(Get-ChildItem 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.16/ref/net6.0/*.dll'|ForEach-Object {'-r:'+$_.FullName})
dotnet 'C:/Program Files/dotnet/sdk/7.0.203/Roslyn/bincore/csc.dll' -nologo -target:exe "-out:$out/AutoCaps.dll" $refs "$out/AutoCaps.cs"
if($LASTEXITCODE){throw 'Auto caps tests compile failed'}
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}'|Set-Content "$out/AutoCaps.runtimeconfig.json"
dotnet "$out/AutoCaps.dll"
if($LASTEXITCODE){throw 'Auto caps tests failed'}
