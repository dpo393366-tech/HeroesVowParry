using System;
namespace Vow.Shared;

// One absolute score supports both percentile ordering and pairwise ratios.
// Stored in log space: large modded stats cannot overflow the product.
internal static class StrengthMetric {
    static double Valid(double v,double minimum)=>double.IsFinite(v)?Math.Max(minimum,v):minimum;
    internal static double Score(double attack,double defence,double efficiency,double soul,double quality){
        return .7*(Math.Log(1+Valid(attack,1))+Math.Log(101+Valid(defence,0))+Math.Log(1+Valid(efficiency,.1)))
            +.2*Math.Log(1+Valid(soul,0))+.1*Math.Log(1+Valid(quality,1));
    }
    internal static double Compare(double first,double second)=>Math.Exp(Math.Clamp(first-second,Math.Log(.05),Math.Log(20)));
}
