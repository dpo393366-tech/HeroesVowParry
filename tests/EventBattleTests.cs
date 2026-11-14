using System;
using System.Collections.Generic;
using System.Reflection;
using Vow.Counters;
namespace UnityEngine {
 class Obj {public static implicit operator bool(Obj o)=>o!=null;}
 static class Time {public static int frameCount;public static float timeScale=1;}
 static class Application {public static bool isFocused=true;}
 static class Input {public static bool down,held,up;public static bool GetMouseButtonDown(int b)=>down;public static bool GetMouseButton(int b)=>held;public static bool GetMouseButtonUp(int b)=>up;}
}
namespace BepInEx.Configuration {
 class ConfigEntry<T> {public T Value;public ConfigEntry(T v){Value=v;}}
 class ConfigDescription {public ConfigDescription(string s,object o){}}
 class AcceptableValueRange<T> {public AcceptableValueRange(T a,T b){}}
 class ConfigFile {public bool SaveOnConfigSet;public Dictionary<string,object> entries=new();public ConfigFile(string p="",bool b=false){}public ConfigEntry<T> Bind<T>(string s,string k,T v,object d){var id=s+"/"+k;if(!entries.ContainsKey(id))entries[id]=new ConfigEntry<T>(v);return (ConfigEntry<T>)entries[id];}}
}
namespace BepInEx {static class Paths {public static string ConfigPath=System.IO.Path.GetTempPath();}}
namespace BepInEx.Unity.IL2CPP {class BasePlugin {public BepInEx.Configuration.ConfigFile Config=new();}}
namespace HarmonyLib {class Harmony {}class HarmonyMethod {public int priority;public HarmonyMethod(Type t,string s){}}static class Priority {public const int Last=0;}static class AccessTools{public static object DeclaredMethod(Type t,string n)=>null;}static class Ext{public static void Patch(this Harmony h,object m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){}}}
enum FMHEOPMKFJB {MySide,Ally,Enemy,Neutral}
class CLAGKDECFID {public string OJPECMNINNA;}
static class NEINKOOBJIH {public static CLAGKDECFID POMNMBGDDIN;}
class NFIDHHGKMDC {public FMHEOPMKFJB IBLKPJDLABN;public IntPtr Pointer;}
class LEDJAIBAAJC : NFIDHHGKMDC {}
class Actor:UnityEngine.Obj {public CLAGKDECFID hero=new();public NFIDHHGKMDC attr=new();public LEDJAIBAAJC m_debateAttribute=new();public IntPtr Pointer;public CLAGKDECFID GetHero()=>hero;public NFIDHHGKMDC GetAttribute()=>attr;}
class KJFOPOCBHBM {public enum CCENIDMDCCN{Attack,Buff}public CCENIDMDCCN HFBAJIGHOAF;}
class MMGEDKCCMHH {public enum GANKBONOKGD{Attack,Buff}public GANKBONOKGD HFBAJIGHOAF;}
class Fi_Controller:UnityEngine.Obj {public static Fi_Controller EBACHLGLNGD;public static bool m_isInBattle,m_isEnd,m_autoBattle,m_isLaunchingAttack;public static List<Actor> m_listHero;public static Actor m_curHero;public static KJFOPOCBHBM m_curCard;public static int CJKNCEOMJNL;public int m_curOrder;public IntPtr Pointer=new(123);}
class De_Controller:UnityEngine.Obj {public static De_Controller EBACHLGLNGD;public static bool m_isInDebate,m_autoDebate,m_isLaunchingAttack;public bool m_isEnd;public static List<Actor> m_listHero;public static Actor m_curHero;public static MMGEDKCCMHH m_curCard;}
static class FICFPEHAJNJ {public static bool ODIOKGGBKEI;}
static class UI_BattlePreviewUI {public static bool m_isOpen;}
namespace Vow.Counters {
 class DuelCounters:BepInEx.Unity.IL2CPP.BasePlugin {public static bool IsPlayer(CLAGKDECFID h)=>h!=null&&h.OJPECMNINNA==NEINKOOBJIH.POMNMBGDDIN?.OJPECMNINNA;public static void StabilityNote(string s){}}
 static class Panel {public static bool InputAllowed=true;public static void Hook(params object[] o){}public static void Hook(HarmonyLib.Harmony h,Type a,string n,Type t,string before=null,string after=null){}public static void Switch(string s,object e){}public static void Slider(string s,object e,float a,float b,float c){}}
 static class BattleModal {public static bool Visible;}
 static class ImpactTiming {public class Attack {public bool motionFinished,cancelled;public bool Valid()=>true;}public static Attack Current(bool b)=>null;}
}
class EventBattleTests {
 static int checks;
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Static;
 static object Call(Type t,string n,params object[] a)=>t.GetMethod(n,F).Invoke(null,a);
 static void Check(bool x,string why){checks++;if(!x)throw new Exception(why);}
 static void Main(){
  var mod=new DuelCounters();mod.Config.Bind("PlayerBattle","EnemyAttackMultiplier",1.2f,"");mod.Config.Bind("EnemyTurns","TurnsPerRound",1.7f,"");
  PlayerBattleRules.Install(mod,new());MartialTurnFrequency.Install(mod,new());
  Check(mod.Config.Bind("PlayerBattle","EnemyAttackMultiplier",2f,"").Value==1.2f,"Saved attack multiplier reset");
  Check(mod.Config.Bind("EnemyTurns","TurnsPerRound",1f,"").Value==1.7f,"Saved turn multiplier reset");
  var fresh=new DuelCounters();PlayerBattleRules.Install(fresh,new());MartialTurnFrequency.Install(fresh,new());
  Check(fresh.Config.Bind("PlayerBattle","EnemyAttackMultiplier",0f,"").Value==1.2f,"Fresh attack default");
  Check(fresh.Config.Bind("EnemyTurns","TurnsPerRound",0f,"").Value==1.2f,"Fresh turn default");
  foreach(bool debate in new[]{false,true})foreach(bool protagonist in new[]{false,true})foreach(bool auto in new[]{false,true})foreach(var side in Enum.GetValues<FMHEOPMKFJB>()){
   UnityEngine.Time.frameCount++;
   var ally=new Actor{hero=new(){OJPECMNINNA="event-officer"},Pointer=new(1)};ally.attr.IBLKPJDLABN=ally.m_debateAttribute.IBLKPJDLABN=side;ally.attr.Pointer=new(1);ally.m_debateAttribute.Pointer=new(1);
   var enemy=new Actor{hero=new(){OJPECMNINNA="enemy"},Pointer=new(2)};enemy.attr.IBLKPJDLABN=enemy.m_debateAttribute.IBLKPJDLABN=FMHEOPMKFJB.Enemy;enemy.attr.Pointer=new(2);enemy.m_debateAttribute.Pointer=new(2);
   var roster=new List<Actor>{ally,enemy};NEINKOOBJIH.POMNMBGDDIN=protagonist?new(){OJPECMNINNA="player"}:null;
   if(protagonist){var p=new Actor{hero=NEINKOOBJIH.POMNMBGDDIN,Pointer=new(3)};roster.Add(p);}
   Fi_Controller.EBACHLGLNGD=new();De_Controller.EBACHLGLNGD=new();Fi_Controller.m_isInBattle=!debate;De_Controller.m_isInDebate=debate;Fi_Controller.m_listHero=De_Controller.m_listHero=roster;Fi_Controller.m_autoBattle=De_Controller.m_autoDebate=auto;
   Check(PlayerBattleRules.ManualCombat(debate),"Event battle availability");
   Check(PlayerBattleRules.Manual(debate,ally.hero,side)==(side==FMHEOPMKFJB.MySide||side==FMHEOPMKFJB.Ally),"Friendly-target scope without protagonist");
   Check(PlayerBattleRules.AutoRoll(debate,ally.hero),"NPC automatic roll lost");
   object[] args={debate?(object)enemy.m_debateAttribute:enemy.attr,100};Call(typeof(PlayerBattleRules),debate?"DebateAttack":"FightAttack",args);
   Check((int)args[1]==(protagonist?120:100),"Attack multiplier scope changed with manual scope");
   Fi_Controller.m_curHero=De_Controller.m_curHero=enemy;Fi_Controller.m_curCard=new();De_Controller.m_curCard=new();Fi_Controller.m_isLaunchingAttack=De_Controller.m_isLaunchingAttack=true;
   UnityEngine.Input.down=UnityEngine.Input.held=true;UnityEngine.Input.up=false;GuardInfoInput.Observe();Check(GuardInfoInput.Suppress(),"RMB guard must not open native modal");
   UnityEngine.Time.frameCount++;UnityEngine.Input.down=false;UnityEngine.Input.held=false;UnityEngine.Input.up=true;Check(GuardInfoInput.Suppress(),"Release edge must not open modal");
   UnityEngine.Time.frameCount++;UnityEngine.Input.up=false;Check(!GuardInfoInput.Suppress(),"Input reservation stuck");Check(UnityEngine.Time.timeScale==1,"Guard changed pause state");
   Fi_Controller.m_isEnd=true;De_Controller.EBACHLGLNGD.m_isEnd=true;Check(!PlayerBattleRules.ManualCombat(debate),"Finished battle still accepts parry");Fi_Controller.m_isEnd=false;
  }
  Fi_Controller.m_isInBattle=false;De_Controller.m_isInDebate=false;Check(!PlayerBattleRules.ManualCombat(false)&&!PlayerBattleRules.ManualCombat(true),"Outside battle eligible");
  for(int tenth=10;tenth<=40;tenth++){
   var carry=new Dictionary<int,int>();int enemies=0,allies=0;
   for(int round=0;round<10;round++){var plan=MartialTurnFrequency.PlanFractional(new[]{1,2,3},u=>u==2,tenth/10f,carry);foreach(int u in plan)if(u==2)enemies++;else allies++;}
   Check(enemies==tenth,"Fractional enemy schedule");Check(allies==20,"Allied turns duplicated");
  }
  Console.WriteLine($"PASS {checks} event scope, RMB ownership, saved balance and fractional schedule checks (mocked game).");
 }
}
