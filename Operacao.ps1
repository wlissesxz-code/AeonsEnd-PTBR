param([ValidateSet('Instalar','Restaurar')][string]$Acao='Instalar',[string]$GamePath)
$ErrorActionPreference='Stop'
function Hash([string]$Path){ (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Find-Game {
 $roots=@('C:\Program Files (x86)\Steam','C:\Program Files\Steam')
 foreach($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')){
  $v=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
  if($v.SteamPath){$roots+= $v.SteamPath}; if($v.InstallPath){$roots+= $v.InstallPath}
 }
 foreach($root in @($roots | Select-Object -Unique)){
  $vdf=Join-Path $root 'steamapps\libraryfolders.vdf'
  if(Test-Path -LiteralPath $vdf){
   foreach($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw),'"path"\s+"([^"]+)"')){$roots+= $match.Groups[1].Value.Replace('\\','\')}
  }
 }
 $found=@($roots | Select-Object -Unique | ForEach-Object {Join-Path $_ "steamapps\common\Aeon's End"} | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'AeonsEnd.exe')})
 if($found.Count -eq 1){return $found[0]}
 return (Read-Host 'Cole o caminho da pasta do jogo (Steam > Gerenciar > Explorar arquivos locais)').Trim('"')
}
if(!$GamePath){$GamePath=Find-Game}
$GamePath=[IO.Path]::GetFullPath($GamePath)
$exe=Join-Path $GamePath 'AeonsEnd.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Pasta incorreta: AeonsEnd.exe não foi encontrado. Nenhum arquivo foi alterado.'}
foreach($p in @(Get-Process AeonsEnd -ErrorAction SilentlyContinue)){
 try{$processPath=$p.Path}catch{$processPath=$null}
 if($processPath -and [string]::Equals($processPath,$exe,[StringComparison]::OrdinalIgnoreCase)){
  throw "O jogo ainda está ativo nesta pasta (PID $($p.Id)). Feche esse processo no Gerenciador de Tarefas. Nenhum arquivo foi alterado."
 }
}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifesto.json') -Raw | ConvertFrom-Json
$allowed=@('AeonsEnd_Data\data.unity3d','AeonsEnd_Data\Managed\AeonsEndEngine.dll','AeonsEnd_Data\Managed\Assembly-CSharp.dll','AeonsEnd_Data\Managed\PortugueseSupport.dll','AeonsEnd_Data\Managed\Unity.TextMeshPro.dll')
if($manifest.files.Count -ne $allowed.Count){throw 'Manifesto incompleto.'}
$seen=@{}; $items=@()
foreach($f in $manifest.files){
 if($f.path -cnotin $allowed -or $seen.ContainsKey($f.path)){throw 'Caminho inválido no manifesto.'}; $seen[$f.path]=$true
 $dest=Join-Path $GamePath $f.path; $backup="$dest.original-ptbr"
 $exists=Test-Path -LiteralPath $dest
 $actual=if($exists){Hash $dest}else{$null}
 $known=@($f.original_sha256,$f.translated_sha256)+@($f.previous_sha256)
 if($exists -and $actual -notin $known){throw "Arquivo desconhecido ou versão incompatível: $($f.path). Nenhum arquivo foi alterado."}
 $source=$null
 if($f.original_sha256){
  if(!$exists){throw "Arquivo do jogo ausente: $($f.path). Nenhum arquivo foi alterado."}
  if(Test-Path -LiteralPath $backup){
   if((Hash $backup) -ne $f.original_sha256){throw "Backup original incompatível: $backup. Nenhum arquivo foi alterado."}
   $source=$backup
  }elseif($actual -eq $f.original_sha256){$source=$dest}
  else{throw "Falta o backup original: $backup. Use a verificação de arquivos do Steam. Nenhum arquivo foi alterado."}
 }
 if($exists){
  try{$handle=[IO.File]::Open($dest,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$handle.Dispose()}
  catch{throw "O Windows não liberou $($f.path). Feche o jogo e confira a permissão de gravação da pasta. Nenhum arquivo foi alterado."}
 }
 $items+= [pscustomobject]@{File=$f;Dest=$dest;Backup=$backup;Source=$source;Existed=$exists;OldHash=$actual;CreatedBackup=$false;Touched=$false}
}
if($Acao -eq 'Instalar'){
 foreach($asset in $manifest.assets){
  if($asset.path -notmatch '^(patches\\[A-Za-z0-9.-]+\.aedelta|PortugueseSupport\.dll|AeonsEndPatch\.dll)$'){throw 'Caminho de pacote inválido.'}
  $path=Join-Path $PSScriptRoot $asset.path
  if(!(Test-Path -LiteralPath $path) -or (Hash $path) -ne $asset.sha256){throw "Pacote incompleto ou alterado: $($asset.path). Extraia novamente o ZIP. Nenhum arquivo foi alterado."}
 }
 Add-Type -Path (Join-Path $PSScriptRoot 'AeonsEndPatch.dll')
}
$stageBase=Join-Path ([IO.Path]::GetTempPath()) 'AeonsEnd-PTBR'
$stage=Join-Path $stageBase ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$keepStage=$false
try{
 # Build every result and save the current state before changing game files.
 $index=0
 foreach($item in $items){
  $f=$item.File
  $item | Add-Member NoteProperty Staged (Join-Path $stage "new-$index")
  $item | Add-Member NoteProperty Rollback (Join-Path $stage "old-$index")
  if($item.Existed){Copy-Item -LiteralPath $item.Dest -Destination $item.Rollback}
  if($Acao -eq 'Restaurar'){
   if($item.Source){Copy-Item -LiteralPath $item.Source -Destination $item.Staged}
   $expected=$f.original_sha256
  }else{
   Write-Output "Preparando $($f.path)..."
   if(!$f.original_sha256){Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PortugueseSupport.dll') -Destination $item.Staged}
   else{
    $source=$item.Source
    if($f.transform -eq 'UnityFSPayload'){
     $source=Join-Path $stage 'unity-payload.bin'
     [AeonUnityPayload]::Extract($item.Source,$source)
     if((Hash $source) -ne $f.payload_sha256){throw 'Conteúdo do arquivo Unity incompatível.'}
    }
    [AeonDelta]::Apply($source,(Join-Path $PSScriptRoot $f.patch),$item.Staged,[long]$f.size)
    if($f.transform -eq 'UnityFSPayload'){Remove-Item -LiteralPath $source}
   }
   $expected=$f.translated_sha256
  }
  if($expected -and (Hash $item.Staged) -ne $expected){throw "Falha ao verificar o resultado: $($f.path)"}
  $index++
 }
 # Recheck the preflight state in case another program changed it while staging.
 foreach($item in $items){
  if($item.Existed){if((Hash $item.Dest) -ne $item.OldHash){throw 'Um arquivo mudou durante a preparação. Tente novamente.'}}
  elseif(Test-Path -LiteralPath $item.Dest){throw 'Um arquivo apareceu durante a preparação. Tente novamente.'}
 }
 try{
  foreach($item in $items){
   if($Acao -eq 'Instalar' -and $item.File.original_sha256 -and !(Test-Path -LiteralPath $item.Backup)){
    Copy-Item -LiteralPath $item.Source -Destination $item.Backup
    $item.CreatedBackup=$true
    if((Hash $item.Backup) -ne $item.File.original_sha256){throw 'Falha ao preservar o backup original.'}
   }
   $item.Touched=$true
   if($Acao -eq 'Restaurar' -and !$item.File.original_sha256){if(Test-Path -LiteralPath $item.Dest){Remove-Item -LiteralPath $item.Dest}}
   else{Copy-Item -LiteralPath $item.Staged -Destination $item.Dest -Force}
   $expected=if($Acao -eq 'Restaurar'){$item.File.original_sha256}else{$item.File.translated_sha256}
   if($expected -and (Hash $item.Dest) -ne $expected){throw 'Falha ao verificar a cópia final.'}
  }
 }catch{
  $failure=$_
  foreach($item in $items){
   try{
    if($item.Touched){
     if($item.Existed){Copy-Item -LiteralPath $item.Rollback -Destination $item.Dest -Force; if((Hash $item.Dest) -ne $item.OldHash){throw 'Rollback inválido.'}}
     elseif(Test-Path -LiteralPath $item.Dest){Remove-Item -LiteralPath $item.Dest}
    }
    if($item.CreatedBackup){Remove-Item -LiteralPath $item.Backup}
   }catch{$keepStage=$true}
  }
  if($keepStage){throw "A operação falhou e a recuperação precisa de atenção. As cópias anteriores estão em: $stage. Erro inicial: $failure"}
  throw "Operação cancelada; os arquivos anteriores foram recuperados. Detalhe: $failure"
 }
 if($Acao -eq 'Instalar'){Write-Output 'Beta 0.3.1 instalada. Os arquivos originais foram preservados.'}
 else{Write-Output 'Arquivos originais restaurados. Os backups foram mantidos para segurança.'}
}finally{
 if(!$keepStage -and (Test-Path -LiteralPath $stage)){
  $resolved=[IO.Path]::GetFullPath($stage)
  $parent=[IO.Path]::GetFullPath($stageBase).TrimEnd('\')+'\'
  if(!$resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^[0-9a-f]{32}$'){throw 'Pasta temporária inválida; limpeza interrompida.'}
  Remove-Item -LiteralPath $resolved -Recurse -Force
 }
}

