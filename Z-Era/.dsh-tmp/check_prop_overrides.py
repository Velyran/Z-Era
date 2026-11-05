import runpy,re,json
from pathlib import Path
ns=runpy.run_path(str(Path(__file__).parent/'parse_interior.py'))
ROOT=ns['ROOT']; blocks=ns['blocks']; trs=ns['trs']; seen=ns['seen']; val=ns['val']; vec=ns['vec']; ref=ns['ref']
bym={fid:b for typ,fid,b in blocks}
for t in sorted(seen):
 tr=trs[t]; b=bym[tr['go']]; name=val(b,'m_Name')
 if 'Dresser_03_Drawer' in (name or ''):
  txt=(ROOT/'Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Dresser_03.prefab').read_text(encoding='utf-8')
  fid=tr['source']['fileID']; mm=re.search(r'^--- !u!4 &'+str(fid)+r'\s*\n(.*?)(?=^---|\Z)',txt,re.M|re.S)
  pb=mm.group(1); changes={}
  for k,label in [('m_LocalPosition','pos'),('m_LocalRotation','rot'),('m_LocalScale','scale')]:
   default=vec(pb,k); actual=tr[label]
   if any(abs(a-d)>1e-5 for a,d in zip(actual,default)):changes[label]={'scene':actual,'prefab':default}
  print(name,'TRANSFORM_CHANGES',json.dumps(changes))
 if 'Car_Wrecked_Overgrowth' in (name or ''):
  txt=(ROOT/'Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Props/SM_Prop_Car_Wrecked_01.prefab').read_text(encoding='utf-8')
  source=ref(val(b,'m_CorrespondingSourceObject'))['fileID']; mm=re.search(r'^--- !u!1 &'+str(source)+r'\s*\n(.*?)(?=^---|\Z)',txt,re.M|re.S)
  print(name,'SCENE_ACTIVE',val(b,'m_IsActive'),'PREFAB_DEFAULT_ACTIVE',val(mm.group(1),'m_IsActive'),'SOURCE_GO',source)
print('OTHER_SCENES',*[str(p.relative_to(ROOT)) for p in (ROOT/'Assets/Scenes').glob('*Chapter*')])
