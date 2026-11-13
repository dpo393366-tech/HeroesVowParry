using System;using System.Collections.Generic;using System.Reflection;using Vow.Dodge;
class QA{
 static int checks;
 static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 static object[] Call(string n,params object[] args){typeof(DodgeLimit).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);return args;}
 static int Evades(NFIDHHGKMDC a,int hit,int evade,bool absolute,bool buff,int cap,bool cannot=false){
  int count=0;for(int roll=0;roll<100;roll++){
   Call("Clear");DodgeLimit.Ceiling.Value=cap;GLIJMELMEHI.BOJHKIIJLLG=absolute;a.cannot=cannot;Fi_Controller.m_curHero.attr.hit=hit;
   var context=new Fi_Controller.FMHMOHIGKCA{_cellTarget_5__14=new(){attr=a}};
   object[] enter=Call("Enter",context,null);
   int changed=(int)Call("Evade",a,false,evade)[2];
   Check(unchecked(hit-changed)==100,"only the native probability branch is bypassed");
   AOHIBKLOAEI.roll=roll;bool result=(bool)Call("Decision",a,buff)[1];if(result)count++;
   Call("Leave",enter[1]);
  }return count;
 }
 static bool Strike(NFIDHHGKMDC defender,int roll,bool forceBuff=true){
  var target=new GKHLLOOEEFN{attr=defender};var ctx=new Fi_Controller.FMHMOHIGKCA{_cellTarget_5__14=target};var enter=Call("Enter",ctx,null);
  Call("Evade",defender,false,1000);AOHIBKLOAEI.roll=roll;
  bool dodge=(bool)Call("Decision",defender,forceBuff)[1];Call("Outcome",target,dodge?LADIINNDGLB.t_be_dodge:LADIINNDGLB.t_hit);
  Call("Outcome",target,dodge?LADIINNDGLB.t_be_dodge:LADIINNDGLB.t_hit); // duplicate callback must not count twice
  Call("Leave",enter[1]);if(!dodge){Fi_Controller.FPGNHJGOCGJ=new(){target};Call("HitResolved",true,true);}return dodge;
 }
 static void Main(){
  new DodgeLimit().Load();Fi_Controller.m_curHero=new();var a=new NFIDHHGKMDC();var b=new NFIDHHGKMDC();
  foreach(int hit in new[]{-100,0,30,99,100,200,10000})foreach(int dodge in new[]{0,20,100,200,1000})foreach(bool absolute in new[]{false,true})foreach(bool buff in new[]{false,true})foreach(bool cannot in new[]{false,true})foreach(int cap in new[]{0,30,70}){
   int expected=Math.Min(cap,DodgeRules.NativePercent(hit,dodge,absolute,cannot,buff));
   Check(Evades(a,hit,dodge,absolute,buff,cap,cannot)==expected,"exhaustive 100 roll outcomes and cap");
  }
  Check(DodgeRules.NativePercent(100,120,true,false,false)==100,"absolute-mode stat evasion can be 100%");
  Check(DodgeRules.NativePercent(100,120,false,false,false)==85,"native soft low-hit correction");
  Check(DodgeRules.NativePercent(100,20,false,false,false)==19,"native strict greater-than off-by-one preserved");
  Call("Clear");DodgeLimit.Ceiling.Value=70;DodgeLimit.Consecutive.Value=4;GLIJMELMEHI.BOJHKIIJLLG=true;Fi_Controller.m_curHero.attr.hit=100;a.cannot=false;b.cannot=false;
  for(int i=0;i<4;i++)Check(Strike(a,99),"four successful dodges");
  Fi_Controller.FPGNHJGOCGJ=new(){new(){attr=a}};Call("HitResolved",true,false);
  Check(Strike(b,99),"independent defender has its own streak");Check(!Strike(a,99),"fifth attack unavoidable even with absolute-dodge buff");Check(Strike(a,99),"counter resets after forced hit");
  Check(!Strike(a,0),"ordinary hit resets");for(int i=0;i<4;i++)Check(Strike(a,99),"ordinary-hit reset grants a new full streak");
  Check(!Strike(a,99),"new fifth attack hits");
  Call("Joining",a);Check(Strike(a,99),"reused or newly joined actor resets");Call("Clear");Check(Strike(a,99),"new battle resets");
  var ctx2=new Fi_Controller.FMHMOHIGKCA{_cellTarget_5__14=new(){attr=a}};var e=Call("Enter",ctx2,null);
  Check((int)Call("Evade",a,true,123)[2]==123,"preview query unchanged");Check((int)Call("Evade",b,false,456)[2]==456,"unrelated actor unchanged");
  Call("Leave",e[1]);Check((int)Call("Evade",a,false,123)[2]==123,"outside native attack unchanged");
  DodgeLimit.Enabled.Value=false;e=Call("Enter",ctx2,null);Check((int)Call("Evade",a,false,123)[2]==123,"OFF restores native result");Call("Leave",e[1]);
  Console.WriteLine("Dodge: "+checks+" checks passed, including 84,000 exact roll outcomes, independent streaks, absolute buffs, previews and OFF.");
 }
}
namespace BepInEx{class BepInPlugin:Attribute{public BepInPlugin(string a,string b,string c){}}}
namespace BepInEx.Configuration{
 public class ConfigEntry<T>{public T Value;public event EventHandler SettingChanged;}
 public class ConfigDescription{public ConfigDescription(string s,object x){}}public class AcceptableValueRange<T>{public AcceptableValueRange(T a,T b){}}
 public class ConfigFile{public ConfigEntry<T> Bind<T>(string a,string b,T value,object desc)=>new(){Value=value};}
}
namespace BepInEx.Unity.IL2CPP{public class BasePlugin{public BepInEx.Configuration.ConfigFile Config=new();public Logger Log=new();public virtual void Load(){}public void AddComponent<T>(){}}public class Logger{public void LogInfo(object o){}public void LogWarning(object o)=>throw new Exception(o.ToString());}}
namespace HarmonyLib{public class Harmony{public Harmony(string n){}public void Patch(object m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){}public void UnpatchSelf(){}}public class HarmonyMethod{public HarmonyMethod(Type t,string n){}}public class AccessTools{public static object DeclaredMethod(Type t,string n)=>null;}}
namespace UnityEngine{public class MonoBehaviour{public MonoBehaviour(IntPtr p){}}public static class Input{public static bool GetKeyDown(KeyCode k)=>false;}public enum KeyCode{F11}public static class Screen{public static int width=1920;}public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float f)=>(int)Math.Round(f);public static int Clamp(int f,int a,int b)=>Math.Clamp(f,a,b);}public struct Rect{public Rect(float a,float b,float c,float d){}}public static class GUI{public static void Box(Rect r,string s){}}public static class GUILayout{public static void BeginArea(Rect r){}public static void EndArea(){}public static void Label(string s){}public static bool Toggle(bool b,string s)=>b;public static float HorizontalSlider(float f,float a,float b)=>f;}}
public class NFIDHHGKMDC{static int id;public IntPtr Pointer=(IntPtr)(++id);public int hit=100;public bool cannot,EDJIDCFHLOP;public bool MCMCPMECLJH()=>cannot;public int OICDHNHGPHP(bool p)=>hit;}
public class GKHLLOOEEFN{public NFIDHHGKMDC attr=new();public NFIDHHGKMDC GetAttribute()=>attr;}
public class Fi_Controller{public static GKHLLOOEEFN m_curHero;public static KJFOPOCBHBM m_tmpCard=new(),m_curCard;public static List<GKHLLOOEEFN> FPGNHJGOCGJ,m_listTargetHero;public class JFBIHELODLP{public int __1__state;}public class FMHMOHIGKCA{public GKHLLOOEEFN _cellTarget_5__14;}}
public class GLIJMELMEHI{public static bool BOJHKIIJLLG;}public class AOHIBKLOAEI{public static int roll;public static int FBBNHMFIKCM(int a,int b)=>roll;}
public enum LADIINNDGLB{t_be_dodge,t_hit}


public class KJFOPOCBHBM{public enum CCENIDMDCCN{Attack,Recovery}public CCENIDMDCCN HFBAJIGHOAF;}
