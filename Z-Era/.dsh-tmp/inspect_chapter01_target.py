from pathlib import Path
import re, json, hashlib
scene = Path('Assets/Scenes/3_Chapter01.unity')
text = scene.read_text(encoding='utf-8-sig')
blocks = {m.group(2): (m.group(1), m.group(3)) for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', text, re.M|re.S)}
b = blocks['323922313'][1]
mods = {path: value for fileid,path,value in re.findall(r'- target: \{fileID: (\d+),[^\n]*\}\n\s+propertyPath: ([^\n]+)\n\s+value: ([^\n]*)', b) if fileid == '4118376944406062'}
parent = re.search(r'm_TransformParent: \{fileID: (\d+)\}', b).group(1)
print(json.dumps({'targetInstance':323922313,'source':re.search(r'm_SourcePrefab: ([^\n]+)',b).group(1),'rootOverrides':{k:v for k,v in mods.items() if k.startswith('m_Local') or k=='m_Name'},'parentID':parent,'parentBlock':blocks.get(parent),'sha256':hashlib.sha256(scene.read_bytes()).hexdigest()}, ensure_ascii=False,indent=2))
Path('.dsh-tmp/scene_hashes.json').write_text(json.dumps({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [scene,Path('Assets/Scenes/3_Detailed_Reference_Scene.unity')]}),encoding='utf-8')
