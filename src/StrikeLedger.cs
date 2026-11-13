using System;
using System.Collections.Generic;

namespace Vow.Counters;

// Integer shares are assigned by strike index, not by the order of successes.
// No-parry sum is EXACTLY the native packet, including its rounding remainder.
internal static class StrikeLedger
{
    internal static int Remaining(int total,int count,Func<int,bool> blocked)
    {
        if(total<=0 || count<1)return total;
        int each=total/count,extra=total%count;long sum=0;
        for(int i=0;i<count;i++)if(!blocked(i))sum+=each+(i<extra?1:0);
        return (int)sum;
    }
    internal static bool SameTime(float a,float b)=>Math.Abs(a-b)<.0001f;
}
