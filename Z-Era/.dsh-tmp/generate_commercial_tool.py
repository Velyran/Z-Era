from pathlib import Path
import json,re,hashlib
root=Path('.')
data=json.loads(Path('.dsh-tmp/interior_prefabs.json').read_text(encoding='utf-8'))
items=data['prefabs']
body=next(x for x in items if x['guid']=='5aed343e83179d249af6934614d6f0ea')
assert body['position']==[0,0,0] and body['rotation']==[0,0,0,1] and body['scale']==[1,1,1]
assert all(x['direct_child'] for x in items)
props=[x for x in items if '/Props/' in x['prefab_path']]
assert len(props)==132
assert len(items)-len(props)==9
assert all(Path(x['prefab_path']).exists() for x in props)
assert all(x['prefab_file'].startswith('SM_Prop_') for x in props)
def f(v):
    return format(v,'.9g')+'f'
def vec(v):return 'new Vector3('+', '.join(f(n) for n in v)+')'
def quat(v):return 'new Quaternion('+', '.join(f(n) for n in v)+')'
lines=[]
for x in props:
    lines.append('        // Reference Transform '+str(x['scene_fileID'])+'; m_SourcePrefab GUID '+x['guid'])
    lines.append('        new Placement("'+Path(x['prefab_file']).stem+'", '+vec(x['position'])+', '+quat(x['rotation'])+', '+vec(x['scale'])+'),')
template=Path('.dsh-tmp/commercial_tool_template.cs.txt').read_text(encoding='utf-8')
out=template.replace('__PLACEMENTS__','\n'.join(lines)).replace('__EXTRACTION_SUMMARY__','141 prefab roots: 132 props included; main building and 8 structural building parts excluded.\n    // Reference building local TRS is identity; group offset (150, 0, 100) is not copied.')
p=Path('.dsh-tmp/Chapter01CommercialReferenceDressing.cs')
p.write_text(out,encoding='utf-8')
print('Generated',p,'placements=',len(props),'unique prefabs=',len({x['guid'] for x in props}))
print('Excluded:',[(x['prefab_file'],x['name']) for x in items if x not in props])
