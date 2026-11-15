using System;
using System.Collections.Generic;
using System.Reflection;
using Vow.Counters;
namespace HarmonyLib {
 public class HarmonyMethod {public string Name;public HarmonyMethod(Type t,string n){Name=n;}}
 public class Harmony {public List<MethodInfo> Targets=new();public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){if(m==null)throw new Exception("missing hook");Targets.Add(m);}}
 public static class AccessTools{public static MethodInfo DeclaredMethod(Type t,string n)=>t.GetMethod(n);}
}
public class Obj{public bool active=true;public void SetActive(bool value){active=value;}}
public class Fi_zCard{public Obj gameObject=new();public static Fi_zCard GetOne()=>new();public void GoingPool(float duration){}}
public class De_zCard{public Obj gameObject=new();public static De_zCard GetOne()=>new();public void GoingPool(float duration){}}
public class Fi_BattleCtrlUI{public void FxCastCard(){}public void FxDrawCard(){}}
public class De_DebateCtrlUI{public void FxCastCard(){}public void FxDrawCard(){}}
public static class Regression{
 static int n;
 static void Check(bool x,string reason){n++;if(!x)throw new Exception(reason);}
 static void Call(string method,object[] args)=>typeof(DiscardDisplay).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 public static int Main(){try{
  var h=new HarmonyLib.Harmony();DiscardDisplay.Install(h);
  foreach(var m in h.Targets)Check(m.Name!="FxDrawCard"&&m.Name!="GetOne","draw must be entirely native");
  Check(h.Targets.Count==4,"only cast-scope and pool hooks remain");
  var f=new Fi_zCard();var d=new De_zCard();object[] a={f,1.5f};Call("HideFight",a);Check(f.gameObject.active&&(float)a[1]==1.5f,"unrelated pool calls unchanged");
  object[] outer={0};Call("Begin",outer);a=new object[]{f,1.5f};Call("HideFight",a);Check(!f.gameObject.active&&(float)a[1]==0,"fight discard still hidden and immediate");
  object[] inner={0};Call("Begin",inner);a=new object[]{d,1.5f};Call("HideDebate",a);Check(!d.gameObject.active&&(float)a[1]==0,"debate discard still hidden and immediate");
  Call("End",inner);f.gameObject.active=true;a=new object[]{f,1.5f};Call("HideFight",a);Check(!f.gameObject.active,"nested exit preserves outer discard");
  Call("End",outer);f.gameObject.active=true;a=new object[]{f,1.5f};Call("HideFight",a);Check(f.gameObject.active&&(float)a[1]==1.5f,"scope exits without leaking into next draw");
  // GetOne and FxDrawCard are unpatched: real hand objects remain active for bonus draws.
  for(int i=0;i<20;i++){Check(Fi_zCard.GetOne().gameObject.active,"fight bonus draw visible");Check(De_zCard.GetOne().gameObject.active,"debate bonus draw visible");}
  Console.WriteLine($"PASS {n} draw-hook removal/discard preservation assertions; native UI not simulated");return 0;
 }catch(Exception e){Console.WriteLine(e.Message);return 1;}}
}
