using System;
using System.Collections.Generic;

namespace Vow.Counters;

// One count per successfully countered action, not per projectile or AOE target.
// A turn can temporarily lend control to a special attacker: keep separate counts
// for each actor in the same native round/order, and clear at the actual turn end.
internal static class ParryStagger
{
    static IntPtr controller;
    static int round,order;
    static readonly Dictionary<IntPtr,int> counts=new();
    internal static bool Record(IntPtr battle,int currentRound,int currentOrder,IntPtr attacker)
    {
        if(attacker==IntPtr.Zero)return false;
        if(controller!=battle || round!=currentRound || order!=currentOrder){
            Reset();controller=battle;round=currentRound;order=currentOrder;
        }
        counts.TryGetValue(attacker,out int count);
        // Saturate after the third: the sixth counter must not cause another
        // displacement in this turn. The next turn starts from zero.
        if(count>=3)return false;
        counts[attacker]=++count;
        return count==3;
    }
    internal static void Miss(IntPtr battle,int currentRound,int currentOrder,IntPtr attacker){
        if(controller==battle && round==currentRound && order==currentOrder && counts.TryGetValue(attacker,out int count) && count<3)counts.Remove(attacker);
    }
    internal static void Reset(){counts.Clear();controller=IntPtr.Zero;round=order=0;}
}
