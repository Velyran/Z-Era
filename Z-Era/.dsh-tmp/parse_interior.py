import re,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SCENE=ROOT/'Assets/Scenes/3_Detailed_Reference_Scene.unity'
PREF=ROOT/'Assets/Packages/ScenesPackages/PolygonApocalypse'
OUT=ROOT/'.dsh-tmp'
text=SCENE.read_text(encoding='utf-8')
# Parse YAML-ish blocks without external yaml dependency
blocks=[]
starts=list(re.finditer(r'^--- !u!(\d+) &(\d+)\s*$',text,re.M))
for i,m in enumerate(starts):
    end=starts[i+1].start() if i+1<len(starts) else len(text)
    blocks.append((int(m.group(1)),int(m.group(2)),text[m.end():end]))

def val(body,key):
    m=re.search(r'^\s*'+re.escape(key)+r':\s*(.+)$',body,re.M)
    return m.group(1).strip() if m else None
def vec(body,key):
    s=val(body,key)
    if not s:return None
    return [float(x) for x in re.findall(r'[xyz w]:\s*([-+0-9.eE]+)',s)]
def ref(s):
    if not s:return None
    m=re.search(r'fileID:\s*(-?\d+)',s); g=re.search(r'guid:\s*([0-9a-f]+)',s)
    return {'fileID':int(m.group(1)) if m else 0, **({'guid':g.group(1)} if g else {})}
objs={}; trs={}; prefbs=[]; go_to_t={}
for typ,fid,b in blocks:
    if typ==1:
        objs[fid]={'id':fid,'name':val(b,'m_Name'),'component_ids':[int(x) for x in re.findall(r'- component: \{fileID: (-?\d+)\}',b)],'prefab_internal':ref(val(b,'m_PrefabInternal'))}
    elif typ==4:
        f=ref(val(b,'m_Father')); cm=re.search(r'^  m_Children:[ \t]*\n((?:  -[^\n]*\n)*)',b,re.M)
        c=[int(x) for x in re.findall(r'fileID: (-?\d+)',cm.group(1))] if cm else []
        trs[fid]={'id':fid,'go':int(re.search(r'm_GameObject: \{fileID: (-?\d+)',b).group(1)),'parent':f['fileID'] if f else 0,'children':c,'source':ref(val(b,'m_CorrespondingSourceObject')),'pos':vec(b,'m_LocalPosition') or [0,0,0],'rot':vec(b,'m_LocalRotation') or [0,0,0,1],'scale':vec(b,'m_LocalScale') or [1,1,1]}
        go_to_t[trs[fid]['go']]=fid
    elif typ==1001:
        sp=ref(val(b,'m_SourcePrefab')); rg=re.search(r'm_RootGameObject: \{fileID: (-?\d+)',b)
        tp=ref(val(b,'m_TransformParent'))
        if sp: prefbs.append({'id':fid,'source':sp,'root_go':int(rg.group(1)) if rg else 0,'parent':tp['fileID'] if tp else 0,'body':b})
# target root and descendants
root_t=1736055158
seen=set(); stack=[root_t]
while stack:
    t=stack.pop()
    if t in seen:continue
    seen.add(t); stack.extend(trs.get(t,{}).get('children',[]))
# ancestor helper
def in_tree(t):
    seen2=set()
    while t and t not in seen2:
        if t in seen:return True
        seen2.add(t); t=trs.get(t,{}).get('parent',0)
    return False
# map prefab instance roots; include any with root GO transform in subtree
instances=[]
for p in prefbs:
    t=go_to_t.get(p['root_go'])
    if t and t in seen:
        o=objs.get(p['root_go'],{}); tr=trs[t]
        instances.append((p,t,o,tr))
# guid to meta path
metas={}
for mp in PREF.rglob('*.meta'):
    try: mt=mp.read_text(encoding='utf-8',errors='ignore')
    except: continue
    gm=re.search(r'^guid:\s*([0-9a-f]+)',mt,re.M)
    if gm: metas[gm.group(1)]=str(mp.relative_to(ROOT)).replace('\\','/')[:-5]
# quaternion math
def qmul(a,b):
 x1,y1,z1,w1=a; x2,y2,z2,w2=b
 return [w1*x2+x1*w2+y1*z2-z1*y2,w1*y2-x1*z2+y1*w2+z1*x2,w1*z2+x1*y2-y1*x2+z1*w2,w1*w2-x1*x2-y1*y2-z1*z2]
def qinv(q):return [-q[0],-q[1],-q[2],q[3]]
def qrot(q,v):return qmul(qmul(q,v+[0]),qinv(q))[:3]
def world(t):
 if not t or t==root_t:return ([0,0,0],[0,0,0,1],[1,1,1])
 tr=trs[t]; p,r,s=world(tr['parent']); lp=[tr['pos'][i]*s[i] for i in range(3)]
 return ([p[i]+qrot(r,lp)[i] for i in range(3)],qmul(r,tr['rot']),[s[i]*tr['scale'][i] for i in range(3)])
def classify(name,path,guid=None):
 x=(name or '')+' '+(path or '')
 if re.search(r'glass',x,re.I):return 'Glass'
 if re.search(r'overgrowth|vegetation|plant|grass|ivy|moss|tree|bush',x,re.I):return 'Overgrowth/vegetation'
 if re.search(r'attach',x,re.I):return 'Attachments'
 if guid=='5aed343e83179d249af6934614d6f0ea' or re.search(r'(^|/)SM_Bld_',x,re.I):return 'Building body'
 return 'Decoration'
# locate prefab root defaults from prefab files: root Transform values by first Transform with m_GameObject root-ish; fallback scene values
items=[]
for p,t,o,tr in instances:
    path=metas.get(p['source'].get('guid',''))
    wpos,wrot,wscale=world(t)
    item={'scene_fileID':t,'gameobject_fileID':p['root_go'],'name':o.get('name'),'category':classify(o.get('name'),path,p['source'].get('guid')),'prefab_file':Path(path).name if path else None,'prefab_path':path,'guid':p['source'].get('guid'),'reference_fileID':p['source'].get('fileID'),'position':tr['pos'],'rotation':tr['rot'],'scale':tr['scale'],'world_position':wpos,'world_rotation':wrot,'world_scale':wscale,'parent_transform_fileID':tr['parent'],'direct_child':tr['parent']==root_t}
    items.append(item)
# building body based exact source guid
body=next((x for x in items if x['guid']=='5aed343e83179d249af6934614d6f0ea'),None)
# Check target building instance modifications: root pose is excluded; any other target/property is an internal child override.
internal_child_overrides=[]
for p,t,o,tr in instances:
    if p['source'].get('guid')!='5aed343e83179d249af6934614d6f0ea': continue
    for mm in re.finditer(r'- target: \{fileID: (-?\d+), guid: [0-9a-f]+, type: 2\}\s*\n\s*propertyPath: ([^\n]+)',p['body']):
        fid=int(mm.group(1)); prop=mm.group(2).strip()
        if not (fid==4118376944406062 and prop.startswith('m_Local')):
            internal_child_overrides.append({'target_fileID':fid,'propertyPath':prop})
# relative transform against body world using inverse body (uniform/nonuniform approximate component inverse)
if body:
 bp,br,bs=body['world_position'],body['world_rotation'],body['world_scale']
 for x in items:
  d=[x['world_position'][i]-bp[i] for i in range(3)]
  d=qrot(qinv(br),d); d=[d[i]/bs[i] if bs[i] else d[i] for i in range(3)]
  x['relative_to_building']={'position':d,'rotation':qmul(qinv(br),x['world_rotation']),'scale':[x['world_scale'][i]/bs[i] if bs[i] else x['world_scale'][i] for i in range(3)]}
# non-prefab objects in tree
nonpref=[]
for t in sorted(seen):
 if t==root_t:continue
 if not any(it['scene_fileID']==t for it in items):
  tr=trs[t]; nonpref.append({'transform_fileID':t,'gameobject_fileID':tr['go'],'name':objs.get(tr['go'],{}).get('name')})
report={'scene':str(SCENE.relative_to(ROOT)).replace('\\','/'),'target_root':{'gameobject_fileID':1736055157,'transform_fileID':root_t,'name':objs[1736055157]['name'],'position':trs[root_t]['pos'],'rotation':trs[root_t]['rot'],'scale':trs[root_t]['scale']},'descendant_transform_count':len(seen)-1,'prefab_instance_count':len(items),'counts_by_category':{k:sum(1 for x in items if x['category']==k) for k in sorted(set(x['category'] for x in items))},'excluded_object_names':[x['name'] for x in items if x['category'] in ('Building body','Glass','Attachments','Overgrowth/vegetation')],'building_transform':body,'non_direct_children':[x for x in items if not x['direct_child']],'non_prefab_objects':nonpref,'prefab_internal_child_overrides':bool(internal_child_overrides),'prefab_internal_child_override_evidence':internal_child_overrides[:50],'props_only_prefabs':[x for x in items if x['category']=='Decoration'],'notes':['Scene parsed read-only; no YAML modified.','Prefab internal child transforms are retained as hierarchy evidence and not emitted as separate prefab instances unless they have their own m_SourcePrefab.','Props-only export excludes building body, Glass, Attachments, and Overgrowth/vegetation; raw prefabs array preserves all source instances.']}
(OUT/'interior_prefabs.json').write_text(json.dumps({'report':report,'prefabs':items},ensure_ascii=False,indent=2),encoding='utf-8')
summary=f"target={report['target_root']['name']} transforms={report['descendant_transform_count']} prefabs={len(items)} counts={report['counts_by_category']} non_direct={len(report['non_direct_children'])} non_prefab={len(nonpref)} body={body['prefab_file'] if body else None}"
(OUT/'interior_prefabs_report.txt').write_text(summary+'\n',encoding='utf-8')
print(summary)
print('JSON',OUT/'interior_prefabs.json')
print('REPORT',OUT/'interior_prefabs_report.txt')
