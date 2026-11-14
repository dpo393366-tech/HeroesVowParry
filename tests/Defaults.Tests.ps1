param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'BepInEx/core/BepInEx.Core.dll'))|Out-Null
$checks=@(
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','PlayerMaxPercent',10),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','AllyMaxPercent',10),
 @('DuelCounters','Vow.Counters.PlayerBattleRules','PlayerBattle','EnemyAttackMultiplier',1.2),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','QuickBasicAttackSpeed',0.8),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','CueInputWindowSeconds',0.35),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','PreCueBufferSeconds',0.2),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','GuardHoldSeconds',0.25),
 @('DuelCounters','Vow.Counters.DuelCounters','Counter','EnemyMaxPercent',60),
 @('DuelCounters','Vow.Counters.EnemyGroggy','EnemyGroggy','ParriedAttacksRequired',10),
 @('DuelCounters','Vow.Counters.EnemyGroggy','EnemyGroggy','DamageTakenMultiplier',1.3),
 @('DuelCounters','Vow.Counters.StrengthPressure','StrengthPressure','MaximumAttackSpeed',1.2),
 @('DuelCounters','Vow.Counters.StrengthPressure','StrengthPressure','MinimumParrySeconds',0.25),
 @('DuelCounters','Vow.Counters.ParryVisuals','Effects','WarningEffectsVisible',1),
 @('DodgeLimit','Vow.Dodge.DodgeLimit','Dodge','Enabled',1),
 @('DodgeLimit','Vow.Dodge.DodgeLimit','Dodge','MaxPercent',15),
 @('DodgeLimit','Vow.Dodge.DodgeLimit','Dodge','MaxConsecutiveDodges',4)
)
$assemblies=@{};foreach($n in 'DuelCounters','DodgeLimit'){$assemblies[$n]=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $repo "dist/$n.dll"))}
$count=0
try{
 foreach($c in $checks){
  $type=$assemblies[$c[0]].MainModule.GetType($c[1]);$found=@()
  foreach($m in $type.Methods){if(!$m.HasBody){continue};foreach($i in $m.Body.Instructions){if($i.OpCode.Code.ToString() -eq 'Ldstr' -and $i.Operand -ceq $c[3]){$found+=,$i.Next}}}
  if($found.Count -ne 1){throw ('Default not uniquely located: '+$c[3])}
  $i=$found[0];$code=$i.OpCode.Code.ToString();$v=$null
  if($code -in 'Ldc_R4','Ldc_R8','Ldc_I4','Ldc_I4_S'){$v=[double]$i.Operand}
  elseif($code -match '^Ldc_I4_([0-8])$'){$v=[double]$Matches[1]}
  if($null -eq $v -or [Math]::Abs($v-$c[4]) -gt 0.00001){throw ('Wrong compiled default: '+$c[3]+' = '+$v)}
  $count++
 }
 $dir=Join-Path ([IO.Path]::GetTempPath()) ('ParryDefaultQA-'+[guid]::NewGuid());New-Item -ItemType Directory $dir|Out-Null
 $fresh=[BepInEx.Configuration.ConfigFile]::new((Join-Path $dir 'fresh.cfg'),$false)
 $savedPath=Join-Path $dir 'saved.cfg';$content=''
 foreach($c in $checks){$content+='['+$c[2]+"]`n"+$c[3]+" = 0.123`n"}
 # Unique sections are required by INI: create existing values through the real API.
 $saved=[BepInEx.Configuration.ConfigFile]::new($savedPath,$false)
 foreach($c in $checks){$saved.Bind[float]($c[2],$c[3],[float]0.123,'').Value=[float]0.123}
 $saved.Save();$reopened=[BepInEx.Configuration.ConfigFile]::new($savedPath,$false)
 foreach($c in $checks){
  if([Math]::Abs($fresh.Bind[float]($c[2],$c[3],[float]$c[4],'').Value-$c[4]) -gt 0.00001){throw 'Fresh config default mismatch'};$count++
  if([Math]::Abs($reopened.Bind[float]($c[2],$c[3],[float]$c[4],'').Value-0.123) -gt 0.00001){throw 'Existing setting overwritten'};$count++
 }
 "PASS: $count compiled-default/fresh-config/preserved-setting checks."
}finally{foreach($a in $assemblies.Values){$a.Dispose()}}
