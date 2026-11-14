using System;
using System.Collections.Generic;
using Vow.Counters;
class GroggyComboTests {
 static int checks;static void C(bool ok,string note){if(!ok)throw new Exception(note);checks++;}
 static void Main(){
  for(int threshold=1;threshold<=50;threshold++){
   var m=new GroggyMeter();
   for(int i=1;i<=threshold*4;i++){
    bool counted=false;bool began=m.RecordAttack(ref counted,0,0,threshold,true);
    C(m.count==i*.25f,"Quarter points lost");C(began==(i==threshold*4),"Combo threshold");
    C(!m.RecordAttack(ref counted,0,0,threshold,true)&&m.count==i*.25f,"Duplicate AOE/callback counted");
    C(Math.Abs(GroggyBarMath.Remaining(m.count,threshold,m.stunned)-(1-i/(threshold*4f)))<.000001,"Fractional HUD");
   }
   C(m.stunned&&m.skips==1,"Stun not scheduled");C(m.Turn(0,0),"Activation turn");C(m.Turn(1,0),"One future skipped turn");C(m.Turn(1,0),"Repeated turn callback");C(!m.Turn(2,0)&&m.count==0&&!m.stunned,"Recovery reset");C(m.Damage(100)==100,"Damage multiplier stuck");
   for(int i=1;i<=threshold;i++)C(m.Record(3,0,threshold)==(i==threshold),"Single threshold changed");m.Recover();
  }
  var mixed=new GroggyMeter();for(int i=0;i<9;i++)mixed.Record(0,0);for(int i=0;i<3;i++)mixed.Record(0,0,10,true);C(mixed.count==9.75f&&!mixed.stunned,"Mixed accumulation");C(mixed.Record(0,0,10,true)&&mixed.count==10,"Mixed threshold");
  mixed.Recover();mixed.Record(0,0,10,true);mixed.Hit(100,100);C(mixed.count==.25f,"Zero damage altered gauge");mixed.Hit(100,90);C(mixed.count==0,"Relief became negative");
  mixed.Record(0,0);mixed.Record(0,0,10,true);mixed.Hit(100,90);C(mixed.count==.25f,"Fraction lost on successful enemy attack");
  C(!PerHitParry.GroggyCombo(null),"Null attack");
  var a=new ImpactTiming.Attack();a.strikes.analysed=true;a.strikes.steps.Add(new());C(!PerHitParry.GroggyCombo(a),"Single attack classified combo");
  a=new();a.strikes.analysed=true;a.strikes.steps.Add(new());for(int i=0;i<12;i++)a.strikes.steps.Add(new(){nativeOnly=true});C(!PerHitParry.GroggyCombo(a),"AOE victims classified combo");
  a=new();a.strikes.analysed=true;a.strikes.steps.Add(new());a.strikes.steps.Add(new());C(PerHitParry.GroggyCombo(a),"Authored combo missed");a.strikes.steps.Clear();C(PerHitParry.GroggyCombo(a),"Classification not cached");
  a=new(){debate=true,speech=new(){m_debateAnimation=new()}};a.speech.m_debateAnimation.DGBOCHKBFJG.Add(new(){EBHFFNHELFB=true,DHBBBOOGECL=.5f});a.speech.m_debateAnimation.DGBOCHKBFJG.Add(new(){EBHFFNHELFB=true,DHBBBOOGECL=.5f});C(!PerHitParry.GroggyCombo(a),"Same-time debate targets classified combo");
  a=new(){debate=true,speech=new(){m_debateAnimation=new()}};a.speech.m_debateAnimation.DGBOCHKBFJG.Add(new(){EBHFFNHELFB=true,DHBBBOOGECL=.5f});a.speech.m_debateAnimation.DGBOCHKBFJG.Add(new(){EBHFFNHELFB=true,DHBBBOOGECL=.75f});C(PerHitParry.GroggyCombo(a),"Debate combo missed");
  Console.WriteLine($"PASS {checks} fractional groggy, mixed hits, duplicate contacts, bar, recovery and classification checks.");
 }
}
namespace Vow.Counters {
 internal static class ImpactTiming {internal sealed class Attack {internal bool debate;internal Speech speech;internal PerHitParry.Plan strikes=new();}}
 internal sealed class Speech {internal Animation m_debateAnimation;}
 internal sealed class Animation {internal List<Event> DGBOCHKBFJG=new();}
 internal sealed class Event {internal bool EBHFFNHELFB;internal float DHBBBOOGECL;}
 internal static partial class PerHitParry {
  internal sealed class Step {internal bool nativeOnly;}
  internal sealed class Plan {internal bool analysed,groggyComboKnown,groggyCombo;internal List<Step> steps=new();}
  static Plan Get(ImpactTiming.Attack a)=>a.strikes;
  static void Analyse(Plan p){}
 }
}
