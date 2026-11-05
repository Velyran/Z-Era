$csc='C:\Program Files\dotnet\sdk\7.0.402\Roslyn\bincore\csc.dll'
$managed='U:\UnityEngine\2022.3.62f2c1\Editor\Data\Managed\UnityEngine'
$netstd='C:\Program Files\dotnet\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1\netstandard.dll'
$refs=Get-ChildItem $managed -Filter *.dll | ForEach-Object { '/reference:'+$_.FullName }
& dotnet $csc /nologo /target:library /langversion:9.0 /nostdlib+ /out:.dsh-tmp/Chapter01SmallHospitalLayout.dll "/reference:$netstd" $refs Assets/Editor/Chapter01SmallHospitalLayout.cs
exit $LASTEXITCODE
