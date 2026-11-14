param([Parameter(Mandatory=$true)][string]$GameDir,[string]$ConvenienceDll='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
Add-Type -Path (Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')
function Check($v,$message){if(!$v){throw $message};Write-Host "PASS $message"}
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $repo 'dist/DuelCounters.dll'))
try{
 $panel=$a.MainModule.GetType('Vow.Counters.Panel')
 $update=$panel.Methods|Where-Object Name -EQ 'Update'
 $key=@($update.Body.Instructions|Where-Object {$_.Operand.Name -eq 'GetKeyDown'})
 Check ($key.Count -eq 1 -and $key[0].Previous.Operand -eq 292) 'standalone uses F11 only'
 Check (!@($panel.Methods|Where-Object HasBody|ForEach-Object {$_.Body.Instructions}|Where-Object {$_.Operand.Name -in @('get_Attached','get_LegacyOpen','TryAttach')}).Count) 'standalone panel never delegates key ownership to convenience UI'
 $settings=$a.MainModule.GetType('Vow.Counters.StandaloneSettings')
 $instructions=@($settings.Methods|Where-Object HasBody|ForEach-Object {$_.Body.Instructions})
 Check (!@($instructions|Where-Object {$_.Operand.Name -in @('GetAssemblies','Invoke','GetField','TryAttach')}).Count) 'standalone does not attach to another menu'
 $draw=$settings.Methods|Where-Object Name -EQ 'Draw'
 Check (@($draw.Body.Instructions|Where-Object {$_.Operand.DeclaringType.FullName -eq 'Vow.Counters.DodgeSettings' -and $_.Operand.Name -eq 'Draw'}).Count -eq 1) 'dodge controls always included in standalone F11'
}finally{$a.Dispose()}
if($ConvenienceDll){
 $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $ConvenienceDll))
 try{
  $panel=$a.MainModule.GetType('Vow.Settings.SettingsPanel')
  $update=$panel.Methods|Where-Object Name -EQ 'Update'
  $key=@($update.Body.Instructions|Where-Object {$_.Operand.Name -eq 'GetKeyDown'})
  Check ($key.Count -eq 1 -and $key[0].Previous.Operand -eq 293) 'convenience uses F12 only'
  $ctor=$panel.Methods|Where-Object Name -EQ '.cctor'
  Check (!@($ctor.Body.Instructions|Where-Object {$_.Operand -in @('DuelCounters','DodgeLimit')}).Count) 'convenience discovery excludes standalone modules'
  Check ($null -eq $a.MainModule.GetType('Vow.Settings.CompactSettings')) 'parry controls not compiled into convenience settings'
 }finally{$a.Dispose()}
}
