namespace Vow.Counters;
// Engine-free state policy: never detach patches while a native battle is alive.
internal sealed class SwitchState {
 internal bool Desired{get;private set;}
 internal bool Active{get;private set;}
 internal SwitchState(bool active){Active=Desired=active;}
 internal void Request(bool desired){Desired=desired;}
 internal bool Pending=>Desired!=Active;
 internal bool CanApply(bool battle)=>Pending&&!battle;
 internal void Commit(){Active=Desired;}
 internal void Fail(){Desired=Active;}
}
