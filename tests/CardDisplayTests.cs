using System;
using System.Reflection;
using Vow.Counters;
namespace HarmonyLib {
 class HarmonyMethod {public HarmonyMethod(Type t,string n){}}
 class Harmony {public int count;public void Patch(object m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){count++;}}
 class AccessTools {public static object DeclaredMethod(Type t,string n)=>n;}
}
class Fi_BattleCtrlUI {} class De_DebateCtrlUI {}
class GO {public bool active=true;public void SetActive(bool b){active=b;}}
class Fi_zCard {public GO gameObject=new();} class De_zCard {public GO gameObject=new();}
class CardDisplayTests {
 static int checks;
 static void Call(string n,params object[] args)=>typeof(DiscardDisplay).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
 static void Main(){
  var h=new HarmonyLib.Harmony();DiscardDisplay.Install(h);Check(h.count==8,"Draw/discard hooks for both modes");
  foreach(bool debate in new[]{false,true}){
   object c=debate?new De_zCard():new Fi_zCard();GO go=debate?((De_zCard)c).gameObject:((Fi_zCard)c).gameObject;
   string draw=debate?"DrawnDebate":"DrawnFight",discard=debate?"HideDebate":"HideFight";
   Call(draw,c);Check(go.active,"Ordinary hand creation must remain visible");
   var d=new object[]{c,1f};Call(discard,d);Check(go.active&&(float)d[1]==1,"Ordinary pool delay must not change");
   object[] outer={0};Call("BeginDraw",outer);object[] inner={0};Call("BeginDraw",inner);
   Call(draw,c);Check(!go.active,"Central draw presentation must be hidden");Call("EndDraw",inner);
   go.SetActive(true);Call(draw,c);Check(!go.active,"Nested draw finalizer must preserve outer scope");
   Call("EndDraw",outer);go.SetActive(true);Call(draw,c);Check(go.active,"Reused native hand must remain visible after draw");
   try {Call("BeginDraw",outer);throw new Exception("native draw failure");}catch{}finally{Call("EndDraw",outer);}
   Call(draw,c);Check(go.active,"Exceptional draw must not leak suppression");
   Call("Begin",outer);Call("Begin",inner);Call("End",inner);d[1]=1f;Call(discard,d);
   Check(!go.active&&(float)d[1]==0,"Committed discard must hide and return without visual delay");
   Call("End",outer);go.SetActive(true);d[1]=.8f;Call(discard,d);Check(go.active&&(float)d[1]==.8f,"Discard scope must end");
   Call(draw,new object[]{null});Call(discard,new object[]{null,1f});checks++;
  }
  Console.WriteLine($"PASS {checks} draw/discard presentation lifecycle checks (mocked UI; not gameplay).");
 }
}
