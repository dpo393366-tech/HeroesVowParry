using System;
namespace Vow.Counters;

// Sample the clock that native hit dispatch actually uses. AnimatorStateInfo
// length and speed can already incorporate authored speed multipliers; dividing
// them again predicted a .22s lead where live contact was .38s away.
// Value type, constant work, no per-frame allocations or combat speed changes.
internal struct SingleCueClock
{
    bool sampled;int clip,state;
    double at;float position;
    internal bool Sample(int nextClip,int nextState,float nextPosition,double now,out double rate){
        rate=0;
        double dt=now-at,delta=nextPosition-position;
        bool valid=sampled && clip==nextClip && state==nextState &&
            dt>0 && dt<=.25 && double.IsFinite(dt) && double.IsFinite(delta) && delta>0 && delta<.5;
        sampled=float.IsFinite(nextPosition)&&double.IsFinite(now);
        clip=nextClip;state=nextState;position=nextPosition;at=now;
        if(!valid)return false;
        rate=delta/dt;return double.IsFinite(rate)&&rate>0;
    }
    internal static bool Due(float contact,float position,double rate,double lead)=>
        double.IsFinite(rate)&&rate>0 && (contact-position)/rate<=lead+1e-7;
}
