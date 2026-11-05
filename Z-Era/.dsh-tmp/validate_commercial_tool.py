from pathlib import Path
import json,re,hashlib,struct
report=json.loads(Path('.dsh-tmp/interior_prefabs.json').read_text(encoding='utf-8'))
props={str(x['scene_fileID']):x for x in report['prefabs'] if '/Props/' in x['prefab_path']}
source=Path('Assets/Editor/Chapter01CommercialReferenceDressing.cs').read_text(encoding='utf-8')
rows=re.findall(r'// Reference Transform (\d+); m_SourcePrefab GUID ([a-f0-9]+)\n\s*new Placement\("([^"]+)", new Vector3\(([^)]+)\), new Quaternion\(([^)]+)\), new Vector3\(([^)]+)\)\)',source)
assert len(rows)==len(props)==132
f32=lambda x:struct.pack('f',float(x))
for fid,guid,name,p,r,s in rows:
    expected=props[fid]
    assert guid==expected['guid'] and name+'.prefab'==expected['prefab_file']
    for actual,key in [(p,'position'),(r,'rotation'),(s,'scale')]:
        assert [f32(v.strip().removesuffix('f')) for v in actual.split(',')]==[f32(v) for v in expected[key]],(fid,key)
    meta=Path(expected['prefab_path']+'.meta').read_text(encoding='utf-8')
    assert 'guid: '+guid in meta
for path,old in json.loads(Path('.dsh-tmp/scene_hashes.json').read_text()).items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==old,path+' changed'
print('PASS: 132 placements, 27 source prefabs, every position/quaternion/scale matches reference float32, GUID matches asset, both scene hashes unchanged.')
