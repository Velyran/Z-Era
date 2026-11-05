from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
root=ET.parse('Assembly-CSharp-Editor.csproj').getroot()
ns={'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
refs=[e.text for e in root.findall('.//m:Reference/m:HintPath',ns) if 'UnityEngine' in e.text or 'UnityEditor' in e.text]
managed=Path('U:/UnityEngine/2022.3.62f2c1/Editor/Data/Managed')
framework=managed/'UnityEngine'/'UnityEngine.CoreModule.dll'
assert framework.exists(), framework
mono=Path('U:/UnityEngine/2022.3.62f2c1/Editor/Data/MonoBleedingEdge/lib/mono/4.7.1-api')
assert (mono/'mscorlib.dll').exists(), mono
refs += [str(mono/n) for n in ['mscorlib.dll','System.dll','System.Core.dll']]
args=['/nologo','/target:library','/langversion:9.0','/nostdlib+','/out:.dsh-tmp/Chapter01CommercialReferenceDressing.dll']
args += ['/reference:'+str(Path(r)) for r in dict.fromkeys(refs)]
args += ['Assets/Editor/Chapter01CommercialReferenceDressing.cs']
Path('.dsh-tmp/compile_reference_tool.rsp').write_text('\n'.join('"'+a+'"' for a in args),encoding='utf-8')
result=subprocess.run(['dotnet','C:/Program Files/dotnet/sdk/7.0.402/Roslyn/bincore/csc.dll','@.dsh-tmp/compile_reference_tool.rsp'])
raise SystemExit(result.returncode)
