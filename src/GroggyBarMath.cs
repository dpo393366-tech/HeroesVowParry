using System;
namespace Vow.Counters;
internal static class GroggyBarMath {
    internal static float Remaining(int count,int threshold,bool stunned)=>stunned?0:Math.Clamp(1-count/(float)Math.Max(1,threshold),0,1);
}
