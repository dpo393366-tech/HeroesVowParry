using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using HarmonyLib;using Vow.Counters;using UnityEngine;
class PacingComparison {
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Apply(bool old){if(old)BaselineAttackPacing.ApplySpeed();else AttackPacing.ApplySpeed();}
 static void End(bool old,ImpactTiming.Attack a){if(old)BaselineAttackPacing.End(a);else AttackPacing.End(a);}
 static ImpactTiming.Attack Begin(bool old,int seed){
  var a=new ImpactTiming.Attack{debate=seed%3==0,quickBasic=seed%5!=0,quickContactAnimation=0,debateCard=new(),animationLoaded=true};
  if(old)BaselineAttackPacing.BeforeLaunch(a);else AttackPacing.BeforeLaunch(a);return a;
 }
 static List<float> Run(bool old,int seed){
  AttackPacing.Reset();BaselineAttackPacing.Reset();Time.Transform=null;Time.Writes=0;Time.ThrowWrite=false;Time.timeScale=2;
  DuelCounters.NoDelay=true;DuelCounters.QuickBasic=true;DuelCounters.AttackLead=0;DuelCounters.WarningLead=0;DuelCounters.QuickBasicSpeed=.35f;Fi_BattleCtrlUI.m_isAccelerate=De_DebateCtrlUI.m_isAccelerate=seed%2==0;
  // Model another installed clock writer without changing either pacing body.
  if(seed%4==0)Time.Transform=v=>v>0?v*1.3f:v;
  var trace=new List<float>();var random=new Random(seed);var a=Begin(old,seed);
  for(int i=0;i<160;i++){
   switch(random.Next(10)){
    case 0:case 1:Time.timeScale=new[]{0f,.1f,.5f,1f,2f,3f}[random.Next(6)];if(old)Apply(true);break;
    case 2:DuelCounters.QuickBasicSpeed=(float)(.1+random.NextDouble());Apply(old);break;
    case 3:a.fight.m_curAnimationIndex=random.Next(2);Apply(old);break;
    case 4:if(old)BaselineAttackPacing.Contact(a);else AttackPacing.Contact(a);break;
    case 5:End(old,a);a=Begin(old,seed+i);break;
    case 6:DuelCounters.QuickBasic=!DuelCounters.QuickBasic;Apply(old);break;
    case 7:a.cancelled=true;Apply(old);break;
    case 8:a.motionFinished=true;Apply(old);break;
    default:Apply(old);break;
   }
   trace.Add(Time.timeScale);
  }
  End(old,a);trace.Add(Time.timeScale);Time.Transform=null;return trace;
 }
 static int others;static void OtherClock()=>others++;
 static void Main(){try{
  var expected=new List<float>[128];for(int s=0;s<128;s++)expected[s]=Run(true,s);
  var h=new Harmony("test.parry.editor");AttackPacing.Install(h);
  Check(Harmony.GetPatchInfo(typeof(GlobalUpdateCtrl).GetMethod("Update"))?.Owners.Contains(h.Id)!=true,"external editor entry remains unpatched by parry");
  Check(Harmony.GetPatchInfo(AccessTools.PropertySetter(typeof(Time),"timeScale")).Owners.Contains(h.Id),"actual Harmony clock-write postfix installed");
  for(int s=0;s<128;s++){
   var actual=Run(false,s);int mismatch=Enumerable.Range(0,actual.Count).FirstOrDefault(i=>Math.Abs(actual[i]-expected[s][i])>1e-5,-1);
   Check(mismatch<0,"baseline mismatch seed="+s+" step="+mismatch+(mismatch>=0?" old="+expected[s][mismatch]+" new="+actual[mismatch]:""));
  }
  AttackPacing.Reset();Time.Transform=null;Time.timeScale=2;Time.Writes=0;DuelCounters.QuickBasic=true;DuelCounters.NoDelay=true;DuelCounters.QuickBasicSpeed=.35f;
  var active=Begin(false,1);Time.Transform=v=>v<1?throw new InvalidOperationException("other writer rejected cap"):v;
  bool failed=false;try{Time.timeScale=2;}catch(InvalidOperationException){failed=true;}
  Check(failed&&!(bool)typeof(AttackPacing).GetField("handlingScaleWrite",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),"clock-writer exception releases reentrancy guard");
  Time.Transform=null;Time.timeScale=2;Check(Math.Abs(Time.timeScale-.35f)<1e-5,"observer still works after exception");End(false,active);
  Time.Writes=0;Time.timeScale=.2f;Check(Time.Writes==1&&Time.timeScale==.2f,"no active attack leaves external slow motion and clock write untouched");
  var saved=new OwnedPatches(h);var other=new Harmony("test.other.clock");other.Patch(AccessTools.PropertySetter(typeof(Time),"timeScale"),postfix:new HarmonyMethod(typeof(PacingComparison),nameof(OtherClock)));
  saved.Set(false);Time.Writes=0;int n=others;Time.timeScale=2;Check(others>n,"disabling parry preserves other mod clock hook");
  Check(!Harmony.GetPatchInfo(AccessTools.PropertySetter(typeof(Time),"timeScale")).Owners.Contains(h.Id),"disable removes only parry observer");
  saved.Set(true);saved.Set(true);Check(Harmony.GetPatchInfo(AccessTools.PropertySetter(typeof(Time),"timeScale")).Postfixes.Count(p=>p.owner==h.Id)==1,"reenabling adds one observer without duplication");
  h.UnpatchSelf();other.UnpatchSelf();
  Console.WriteLine("PASS pacing compatibility "+checks+" checks; baseline/new128 scenarios x160 operations plus restore, pause, cap changes, clip changes, cancel, contact, other clock writer and actual Harmony on/off ownership. Unity/game actions mocked.");
 }catch(Exception e){Console.WriteLine(e);Environment.ExitCode=1;}}
}
