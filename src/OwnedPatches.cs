using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace Vow.Counters;
// Capture once after initialization. Re-enabling never rebinds configuration or duplicates events.
internal sealed class OwnedPatches {
 readonly Harmony harmony;
 readonly List<(MethodBase target,int slot,HarmonyMethod hook)> saved=new();
 readonly MethodInfo patchMethod;
 internal bool Applied{get;private set;}=true;
 internal int Count=>saved.Count;
 internal OwnedPatches(Harmony h){
  harmony=h;
  patchMethod=typeof(Harmony).GetMethods().Where(m=>m.Name=="Patch"&&!m.IsGenericMethod)
   .Where(m=>m.GetParameters().Length>=5&&m.GetParameters()[0].ParameterType==typeof(MethodBase)).OrderByDescending(m=>m.GetParameters().Length).First();
  foreach(var target in Harmony.GetAllPatchedMethods().ToArray()){
   var info=Harmony.GetPatchInfo(target);
   if(info==null)continue;
   string[] lists={"Prefixes","Postfixes","Transpilers","Finalizers","ILManipulators"};
   for(int slot=0;slot<Math.Min(lists.Length,patchMethod.GetParameters().Length-1);slot++){
    var member=info.GetType().GetProperty(lists[slot]);
    var entries=(member?.GetValue(info)??info.GetType().GetField(lists[slot])?.GetValue(info)) as System.Collections.IEnumerable;
    if(entries==null)continue;
    foreach(Patch p in entries){if(p.owner!=h.Id)continue;
     var hook=new HarmonyMethod(p.PatchMethod){priority=p.priority,before=p.before,after=p.after,debug=p.debug};
     saved.Add((target,slot+1,hook));
    }
   }
  }
  if(saved.Count==0)throw new InvalidOperationException("Parry hook set is empty");
 }
 internal void Set(bool enabled){
  if(enabled==Applied)return;
  if(!enabled){harmony.UnpatchSelf();Applied=false;return;}
  try{
   foreach(var p in saved){var args=new object[patchMethod.GetParameters().Length];args[0]=p.target;args[p.slot]=p.hook;patchMethod.Invoke(harmony,args);}
   Applied=true;
  }catch{harmony.UnpatchSelf();Applied=false;throw;}
 }
}
