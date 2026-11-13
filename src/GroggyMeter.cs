namespace Vow.Counters;

// Per-enemy, per-battle state. Turn keys are native round/order pairs, not frames.
internal sealed class GroggyMeter
{
    internal int count,skips;
    internal bool stunned;
    // Successful outgoing attack: victim HP must actually decrease.
    // Blocked, dodged and zero-damage hits do not reduce the attacker's meter.
    // Reducing the meter never cancels an already scheduled groggy turn.
    internal void Hit(int before,int after){if(before>after && count>0)count--;}
    int activationRound,activationOrder,lastRound=int.MinValue,lastOrder=int.MinValue;
    bool lastSkip;
    internal bool RecordAttack(ref bool counted,int round,int order,int threshold=10){
        if(counted)return false;counted=true;return Record(round,order,threshold);
    }
    internal bool Record(int round,int order,int threshold=10){
        if(stunned)return false;
        threshold=System.Math.Clamp(threshold,1,50);
        if(++count<threshold)return false;
        count=threshold;skips=1;stunned=true;activationRound=round;activationOrder=order;
        lastRound=int.MinValue;lastOrder=int.MinValue;return true;
    }
    internal bool Turn(int round,int order){
        if(!stunned)return false;
        // Finish the card which triggered groggy, then end this existing turn.
        // It is not one of the next skipped turn.
        if(round==activationRound&&order==activationOrder)return true;
        if(round==lastRound&&order==lastOrder)return lastSkip;
        lastRound=round;lastOrder=order;
        if(skips>0){skips--;return lastSkip=true;}
        Recover();return false;
    }
    // Recovery removes the complete temporary state, not a division of a
    // previously multiplied stat. Repeated stuns never retain a damage factor.
    internal void Recover(){stunned=false;count=skips=0;lastSkip=false;activationRound=activationOrder=lastRound=lastOrder=int.MinValue;}
    internal float EffectiveMultiplier(float configured=1.5f)=>!stunned?1f:
        float.IsFinite(configured)?System.Math.Clamp(configured,1f,5f):1.5f;
    internal int Damage(int value,float multiplier=1.5f){
        if(value<=0)return value;
        multiplier=EffectiveMultiplier(multiplier);
        return (int)System.Math.Min(int.MaxValue,System.Math.Round(value*(double)multiplier,System.MidpointRounding.AwayFromZero));
    }
}
