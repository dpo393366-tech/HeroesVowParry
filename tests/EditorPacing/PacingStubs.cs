
using System;using System.Collections.Generic;using System.Reflection;using System.Text.Json;using Vow.Counters;
namespace UnityEngine{
 public class Obj{public static implicit operator bool(Obj o)=>o!=null;}
 public class Animator:Obj{public float speed=1;public int layerCount=1;public bool isActiveAndEnabled=true,transition;public AnimatorStateInfo state=new(){name="Attack1"},next=new(){name="Attack1"};public AnimatorStateInfo GetCurrentAnimatorStateInfo(int i)=>state;public AnimatorStateInfo GetNextAnimatorStateInfo(int i)=>next;public bool IsInTransition(int i)=>transition;}
 public struct AnimatorStateInfo{public string name;public float normalizedTime;public bool IsName(string s)=>name==s;}
 public class AnimationClip:Obj{public float length=1;}
 public static class Time{static float value=2;public static Func<float,float> Transform;public static int Writes;public static bool ThrowWrite;
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Store(float v){if(ThrowWrite)throw new InvalidOperationException("clock write failed");if(++Writes>10000)throw new Exception("setter recursion");value=Transform==null?v:Transform(v);}
 public static float timeScale{[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]get=>value;[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]set=>Store(value);}
 public static float unscaledDeltaTime=1f/60;public static double unscaledTimeAsDouble;public static int frameCount;}
 public static class Mathf{public static bool Approximately(float a,float b)=>Math.Abs(a-b)<1e-5;}
 public static class Application{public static bool isFocused=true;}
}

namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
class KJFOPOCBHBM{public enum CCENIDMDCCN{Attack,Buff}public CCENIDMDCCN HFBAJIGHOAF;public string OJPECMNINNA;}
class MMGEDKCCMHH{public enum GANKBONOKGD{Attack,Buff}public GANKBONOKGD HFBAJIGHOAF;}
class PKLOJNCAOND{static int id;public IntPtr Pointer=(IntPtr)(++id);public bool EBHFFNHELFB,GHEGCDIKHDI,GLBFOMGOIIO;public int MFAPFHKKMOK;public float DHBBBOOGECL;}
class PFPODJLPKPL{public string HIENOINGBCN;public float PIJKBOHAMDK;public List<PKLOJNCAOND> DGBOCHKBFJG=new();}
class Fi_Controller{public bool LoadAttackAnim()=>true;public IntPtr Pointer=(IntPtr)10;public static bool m_isLaunchingAttack=true;public int m_curAnimationIndex;public List<PFPODJLPKPL> m_listBattleAnimation=new();public List<int> m_listEventIndex=new(){0};}
class De_Controller{public void LoadAttackAnim(){}public IntPtr Pointer=(IntPtr)20;public PFPODJLPKPL m_debateAnimation;public int m_curAnimationIndex;}
class GKHLLOOEEFN{public UnityEngine.Animator animator=new();public UnityEngine.Animator GetAnimator()=>animator;}
class NPCCtrl{}
class GlobalUpdateCtrl{[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]public void Update(){}public void DGKHPLBKLJB(){}public void LDKGPFEBOBD(){}}
class HILPKINAKPL{public static UnityEngine.AnimationClip NGFCCDLMGAN(string s,int i)=>new();}
class Fi_BattleCtrlUI{public static bool m_isAccelerate=true;}class De_DebateCtrlUI{public static bool m_isAccelerate=true;}
namespace Vow.Counters{
 static class DuelCounters{public static bool NoDelay=true,QuickBasic=true;public static float AttackLead=0,QuickBasicSpeed=.35f,WarningLead=0;public static void Warn(string s){} }
 static class CardLatency{public static void Begin(ImpactTiming.Attack a){}public static void Hold(bool d,string s){}public static void Loaded(ImpactTiming.Attack a){} }
 static class CombatTrace{public static void Event(string s){} }
 static class RealtimeParry{public static void Audit(ImpactTiming.Attack a,string s,string d){}public static void ContactCue(ImpactTiming.Attack a,int i,float f){}public static void ObserveLoadedAttack(ImpactTiming.Attack a){AttackPacing.ObserveSingle(a);} }
 static class ContactEffects{internal struct Context{}public static Context Begin()=>new();public static void Set(PerHitParry.Plan p,PerHitParry.Step s){}public static void Nearest(PerHitParry.Plan p,int i,float f){}public static Exception EventEnd(Context c,Exception e)=>e;}
 static class ImpactTiming{
  public static Attack current;public static Attack Current(bool d)=>current;
  internal class Attack{public bool valid=true,debate,preparing,launchEntered,quickBasic,quickBasicChecked,quickContactPassed,animationLoaded=true,cancelled,motionFinished,warningPlanned;public int quickContactAnimation=-1,firstContactAnimation;public float quickContactTime,quickNativeLead,firstContactTime,requestedWarning,warningCap;public double preparationAt,preparationElapsed,quickLoadedAt,firstCueAt=-1;public Fi_Controller fight=new();public De_Controller speech=new();public GKHLLOOEEFN fighter=new();public KJFOPOCBHBM fightCard=new();public MMGEDKCCMHH debateCard;public PerHitParry.Plan strikes;public AttackClipClock.State clipClock;public bool Valid()=>valid;}
 }
}

namespace Vow.Counters {
 static class AttackClipClock{public class State{}public static void Loaded(ImpactTiming.Attack a){} }
 static class StrikeLedger{public static bool SameTime(float a,float b)=>Math.Abs(a-b)<.00001;}
 static class PerHitParry{public class Plan{}public class Step{}public static bool TrySingle(ImpactTiming.Attack a,out int i,out float t){i=0;t=.5f;return a.quickBasic;}public static bool IsParryContact(ImpactTiming.Attack a,int i,float t)=>true;}
}
