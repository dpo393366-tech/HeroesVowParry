using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vow.Counters;
class SwitchTests {
 static int count;
 static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 [MethodImpl(MethodImplOptions.NoInlining)] public static int Value()=>7;
 public static void Parry(ref int __result)=>__result+=10;
 public static void Foreign(ref int __result)=>__result*=2;
 static void Main(){
  for(int initial=0;initial<2;initial++){
   var s=new SwitchState(initial==1);
   s.Request(initial==0);Check(s.Pending,"pending");Check(!s.CanApply(true),"battle never interrupted");
   Check(s.Active==(initial==1),"battle state remains");Check(s.CanApply(false),"outside battle applies");s.Commit();Check(!s.Pending,"commit");
   s.Request(!s.Active);s.Fail();Check(!s.Pending,"failure returns previous state");
   s.Request(!s.Active);s.Request(s.Active);Check(!s.Pending,"cancel queued change");
  }
  var own=new Harmony("tests.parry.owner");var other=new Harmony("tests.other.owner");
  var target=AccessTools.Method(typeof(SwitchTests),nameof(Value));
  other.Patch(target,postfix:new HarmonyMethod(typeof(SwitchTests),nameof(Foreign)){priority=Priority.Last});
  own.Patch(target,postfix:new HarmonyMethod(typeof(SwitchTests),nameof(Parry)){priority=Priority.First});
  var saved=new OwnedPatches(own);Check(saved.Count==1,"captures own hook only");
  for(int i=0;i<100;i++){
   saved.Set(false);saved.Set(false);
   Check(Value()==14,"OFF restores original plus unrelated mod");
   Check(Harmony.GetPatchInfo(target).Postfixes.Count(p=>p.owner==other.Id)==1,"unrelated owner retained");
   saved.Set(true);saved.Set(true);
   Check(Value()==34,"ON restores priority and behavior");
   Check(Harmony.GetPatchInfo(target).Postfixes.Count(p=>p.owner==own.Id)==1,"no duplicate hooks");
  }
  own.UnpatchSelf();other.UnpatchSelf();Check(Value()==7,"clean teardown");
  Console.WriteLine($"PASS {count} lifecycle checks including 100 real Harmony OFF/ON cycles. Native Unity behavior tested separately.");
 }
}
