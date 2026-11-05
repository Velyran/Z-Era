$dir='Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props'
$files=Get-ChildItem $dir -Filter '*.prefab' | Where-Object {$_.BaseName -match '^SM_Prop_(Bed_Hospital|Bed_Gurney|WheelChair|Medical|Chair_|Study_Chair|Study_Desk|Desk_|Seat_|Papers_)'}
foreach($f in $files){$txt=Get-Content $f.FullName -Raw;$blocks=[regex]::Split($txt,'(?m)^--- ');$gos=@{};$ts=@();foreach($b in $blocks){if($b -match '^!u!1 &(\d+)' -and $b -match '(?m)^  m_Name: (.+)$'){$id=[regex]::Match($b,'^!u!1 &(\d+)').Groups[1].Value;$gos[$id]=$Matches[1]};if($b -match '^!u!4 &(\d+)'){$ts+= $b}};"`n$($f.Name)";foreach($t in $ts){$go=[regex]::Match($t,'m_GameObject: \{fileID: (\d+)').Groups[1].Value;$parent=[regex]::Match($t,'m_Father: \{fileID: (\d+)').Groups[1].Value;$pos=[regex]::Match($t,'m_LocalPosition: (.+)').Groups[1].Value;$rot=[regex]::Match($t,'m_LocalRotation: (.+)').Groups[1].Value;$scale=[regex]::Match($t,'m_LocalScale: (.+)').Groups[1].Value;"  $($gos[$go]) parent=$parent p=$pos q=$rot s=$scale"}}
'REFERENCES:'
$cs=Get-Content 'Assets/Editor/Chapter01HospitalDressing.cs' -Raw
$refs=[regex]::Matches($cs,'"(SM_Prop_[^"]+)"')|%{$_.Groups[1].Value}|select -Unique
foreach($r in $refs){if(!(Test-Path "$dir/$r.prefab")){"MISSING $r"}};"checked=$($refs.Count)"
'CACHE:'
Get-Item 'Library/metadata','Library/Artifacts','Library/SourceAssetDB' -ErrorAction SilentlyContinue | Select-Object FullName,LastWriteTime
