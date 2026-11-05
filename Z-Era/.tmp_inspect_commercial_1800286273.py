import re, json, pathlib, collections, hashlib
root=pathlib.Path.cwd()
scene=root/'Assets/Scenes/3_Chapter01.unity'
prefab=root/'Assets/Packages/ScenesPackages/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Commercial_Small_01.prefab'
paths=[scene,prefab,pathlib.Path(str(prefab)+'.meta')]
before={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
def docs(p):
    text=p.read_text(encoding='utf-8-sig')
    matches=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)( stripped)?\s*$',text,re.M))
    return {int(m[2]):{'cls':int(m[1]),'stripped':bool(m[3]),'line':text.count('\n',0,m.start())+1,'body':text[m.end():matches[i+1].start() if i+1<len(matches) else len(text)]} for i,m in enumerate(matches)}
def ref(d,key):
    m=re.search(r'^\s*'+re.escape(key)+r': \{fileID: (-?\d+)',d['body'],re.M)
    return int(m[1]) if m else None
def val(d,key):
    m=re.search(r'^  '+re.escape(key)+r': (.*)$',d['body'],re.M)
    return m[1] if m else None
def vector(d,key):
    s=val(d,key)
    return {k:float(v) for k,v in re.findall(r'([xyzw]): ([-+\deE.]+)',s or '')}
def mods(d):
    return [(int(a),b,c) for a,b,c in re.findall(r'- target: \{fileID: (\d+),[^}]+\}\s+propertyPath: ([^\n]+)\s+value:([^\n]*)',d['body'])]
s=docs(scene); p=docs(prefab); inst=1800286273; tr=1800286274; source_tr=4794952211823098
m=mods(s[inst]); overrides={(a,b):c.strip() for a,b,c in m}
stripped=[{'id':i,'class':d['cls'],'source':ref(d,'m_CorrespondingSourceObject'),'line':d['line']} for i,d in s.items() if ref(d,'m_PrefabInstance')==inst]
pose={k:vector(p[source_tr],k) for k in ['m_LocalPosition','m_LocalRotation','m_LocalScale']}
for k,v in pose.items():
    for axis in v:
        if (source_tr,k+'.'+axis) in overrides:v[axis]=float(overrides[source_tr,k+'.'+axis])
children=[]
for i,d in p.items():
    if d['cls']==4 and ref(d,'m_Father')==source_tr:
        go=ref(d,'m_GameObject'); pos=vector(d,'m_LocalPosition')
        for axis in pos:
            if (i,'m_LocalPosition.'+axis) in overrides:pos[axis]=float(overrides[i,'m_LocalPosition.'+axis])
        children.append({'transform':i,'gameobject':go,'name':overrides.get((go,'m_Name'),val(p[go],'m_Name')),'position':pos,'active':overrides.get((go,'m_IsActive'),val(p[go],'m_IsActive')),'line':d['line']})
normal=[]; nested=[]
for i,d in s.items():
    if d['cls'] in (4,224) and ref(d,'m_Father')==tr:
        go=ref(d,'m_GameObject'); normal.append({'transform':i,'gameobject':go,'name':val(s[go],'m_Name') if go in s else None,'line':d['line']})
    if d['cls']==1001 and ref(d,'m_TransformParent')==tr:
        nested.append({'instance':i,'names':[c.strip() for a,b,c in mods(d) if b=='m_Name'],'line':d['line']})
section=re.search(r'    m_AddedGameObjects:(.*?)(?=    m_AddedComponents:|  m_SourcePrefab:)',s[inst]['body'],re.S)
# The addedObject field identifies a GameObject (not a Transform); retain all records even if none.
added=[]
for target,go in re.findall(r'targetCorrespondingSourceObject: \{fileID: (\d+),[^}]+\}.*?addedObject: \{fileID: (\d+)\}',section[1] if section else '',re.S):
    go=int(go); added.append({'target':int(target),'gameobject':go,'name':val(s[go],'m_Name') if go in s else None})
parent=ref(s[inst],'m_TransformParent'); parent_go=ref(s[parent],'m_GameObject')
result={'files':[{'path':str(x.relative_to(root)),'mtime':x.stat().st_mtime,'sha256':before[str(x)]} for x in paths],'instance':inst,'root_source_transform':source_tr,'root_source_gameobject':1443285041389150,'stripped':stripped,'root_pose':pose,'parent':{'transform':parent,'gameobject':parent_go,'name':val(s[parent_go],'m_Name') if parent_go in s else None},'prefab_transform_count':sum(d['cls']==4 for d in p.values()),'prefab_children':children,'local_y_histogram':dict(collections.Counter(str(c['position'].get('y')) for c in children)),'normal_scene_direct_children':normal,'prefab_instance_direct_children':nested,'added_gameobjects':added,'added_section':section[1].strip() if section else None,'prefix_counts':dict(collections.Counter('_'.join(c['name'].split('_')[:2]) for c in children if c['name'])),'unchanged':all(before[str(x)]==hashlib.sha256(x.read_bytes()).hexdigest() for x in paths)}
print(json.dumps(result,ensure_ascii=False,indent=2))
