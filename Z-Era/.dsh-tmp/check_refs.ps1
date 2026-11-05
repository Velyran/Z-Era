$cs=Get-Content 'Assets/Editor/Chapter01HospitalDressing.cs' -Raw
$refs=[regex]::Matches($cs,'Place\("([^"]+)"')|%{$_.Groups[1].Value}|select -Unique
foreach($r in $refs){$p="Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/$r.prefab"; "{0} {1}" -f ($(if(Test-Path $p){'OK'}else{'MISSING'}),$r)}