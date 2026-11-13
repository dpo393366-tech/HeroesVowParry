using System;
using System.Collections.Generic;

namespace Vow.Dodge;

internal sealed class DodgeRules
{
    readonly Dictionary<IntPtr,int> streaks=new();
    internal int Streak(IntPtr defender)=>streaks.TryGetValue(defender,out int n)?n:0;
    internal bool MustHit(IntPtr defender,int limit)=>Streak(defender)>=Math.Clamp(limit,1,10);
    internal void Record(IntPtr defender,bool dodged){
        if(dodged)streaks[defender]=Math.Min(10,Streak(defender)+1);
        else streaks.Remove(defender);
    }
    internal void Forget(IntPtr defender)=>streaks.Remove(defender);
    internal void Clear()=>streaks.Clear();

    // Reproduces the installed native LaunchAttack integer comparison: roll [0,100) > H.
    internal static int NativePercent(int accuracy,int evade,bool absoluteMode,bool cannotDodge,bool guaranteedDodge){
        if(guaranteedDodge)return 100;
        if(cannotDodge)return 0;
        long hit=(long)accuracy-evade;
        if(!absoluteMode && hit<30)hit=Math.Max(10,30-(30-hit)/3);
        return (int)Math.Clamp(99-hit,0,100);
    }
    internal static int CappedPercent(int nativePercent,int ceiling,bool forced)=>forced?0:Math.Clamp(nativePercent,0,Math.Clamp(ceiling,0,70));
    internal static bool Roll(int percent,int value)=>value>=100-percent;
}
