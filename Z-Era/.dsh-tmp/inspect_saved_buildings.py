import re,json,math,sys
from pathlib import Path
from collections import Counter,defaultdict
from datetime import datetime
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.dsh-tmp'
GUIDS={'large':'d5ccfc9c8d2f52848b43a7d00834a680','medium':'5aed343e83179d249af6934614d6f0ea'}
HEADER=re.compile(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?[ \t]*$',re.M)
def blocks(text):
 ms=list(HEADER.finditer(text)); starts=[0]
 for m in re.finditer('\n',text): starts.append(m.end())
 import bisect
 return [(int(m[1]),int(m[2]),text[m.end():ms[i+1].start() if i+1<len(ms) else len(text)],bisect.bisect_right(starts,m.start())) for i,m in enumerate(ms)]
def val(b,k):
 m=re.search(r'^  '+re.escape(k)+r':[ \t]*(.*)$',b,re.M); return m[1].strip() if m else ''
def fid(s):
 m=re.search(r'fileID:\s*(-?\d+)',s); return int(m[1]) if m else 0
def guid(s):
 m=re.search(r'guid:\s*([0-9a-f]+)',s); return m[1] if m else ''
def vec(s,default):
 a=re.findall(r'[xyzw]:\s*([-+\d.eE]+)',s); return list(map(float,a)) if a else default[:]
def pose(b):return [vec(val(b,'m_LocalPosition'),[0,0,0]),vec(val(b,'m_LocalRotation'),[0,0,0,1]),vec(val(b,'m_LocalScale'),[1,1,1])]
def qmul(a,b):
 x,y,z,w=a; X,Y,Z,W=b; return [w*X+x*W+y*Z-z*Y,w*Y-x*Z+y*W+z*X,w*Z+x*Y-y*X+z*W,w*W-x*X-y*Y-z*Z]
def inv(q):return [-q[0],-q[1],-q[2],q[3]]
def rot(q,p):return qmul(qmul(q,p+[0]),inv(q))[:3]
meta={}
for p in (ROOT/'Assets/Packages/ScenesPackages/PolygonApocalypse').rglob('*.prefab.meta'):
 g=guid(p.read_text(encoding='utf-8',errors='ignore').replace('guid:', 'guid:'))
 if g:meta[g]=Path(str(p)[:-5])
assets={}
def asset(g):
 if g in assets:return assets[g]
 p=meta.get(g); gos={}; ts={}; rg=0
 if p:
  for typ,i,b,line in blocks(p.read_text(encoding='utf-8-sig')):
   if typ==1:gos[i]=val(b,'m_Name')
   elif typ==4:ts[i]={'go':fid(val(b,'m_GameObject')),'parent':fid(val(b,'m_Father')),'pose':pose(b)}
   elif typ==1001:rg=fid(val(b,'m_RootGameObject')) or rg
 rt=next((i for i,t in ts.items() if (t['go']==rg if rg else not t['parent'])),0)
 a={'path':str(p.relative_to(ROOT)).replace('\\','/') if p else None,'go':gos,'tr':ts,'root':rt,'rootgo':ts.get(rt,{}).get('go',0)}; assets[g]=a; return a

def analyze(path):
 text=path.read_text(encoding='utf-8-sig'); gos={}; trs={}; ps={}; got={}
 for typ,i,b,line in blocks(text):
  if typ==1:gos[i]={'name':val(b,'m_Name'),'line':line}
  elif typ==4:
   go=fid(val(b,'m_GameObject')); trs[i]={'go':go,'source':fid(val(b,'m_CorrespondingSourceObject')),'pi':fid(val(b,'m_PrefabInstance')),'parent':fid(val(b,'m_Father')),'pose':pose(b),'stripped':not bool(val(b,'m_GameObject')),'line':line}
   if go:got[go]=i
  elif typ==1001 and val(b,'m_SourcePrefab'):
   mods={}
   for m in re.finditer(r'- target: \{fileID: (-?\d+),.*?\}\s*\n\s*propertyPath: ([^\n]+)\n\s*value:[ \t]*([^\n]*)',b):mods[(int(m[1]),m[2].strip())]=m[3].strip()
   added=[(int(m[1]),int(m[2])) for m in re.finditer(r'targetCorrespondingSourceObject: \{fileID: (-?\d+),.*?\}\s*\n\s*insertIndex: -?\d+\s*\n\s*addedObject: \{fileID: (-?\d+)\}',b.split('m_AddedGameObjects:',1)[1].split('m_AddedComponents:',1)[0] if 'm_AddedGameObjects:' in b else '',re.S)]
   ps[i]={'guid':guid(val(b,'m_SourcePrefab')),'parent':fid(val(b,'m_TransformParent')),'oldgo':fid(val(b,'m_RootGameObject')),'mods':mods,'added':added,'line':line}
 srcmap={(t['pi'],t['source']):i for i,t in trs.items() if t['pi'] and t['source']}
 nodes={i:dict(t) for i,t in trs.items()}; rootmap={}
 def source_node(pid,s):
  key=srcmap.get((pid,s),('source',pid,s))
  if key in nodes and not nodes[key].get('stripped'):return key
  if key in nodes and nodes[key].get('resolved'):return key
  p=ps[pid]; a=asset(p['guid']); st=a['tr'].get(s,{'go':0,'parent':0,'pose':[[0,0,0],[0,0,0,1],[1,1,1]]}); po=[v[:] for v in st['pose']]
  for j,prefix in enumerate(['m_LocalPosition','m_LocalRotation','m_LocalScale']):
   for k,axis in enumerate('xyzw'[:len(po[j])]):
    v=p['mods'].get((s,prefix+'.'+axis))
    if v not in (None,''):po[j][k]=float(v)
  nodes[key]={'go':st['go'],'pose':po,'parent':0,'resolved':True,'pi':pid,'source':s,'name':p['mods'].get((st['go'],'m_Name'),a['go'].get(st['go'],''))}
  nodes[key]['parent']=source_node(pid,st['parent']) if st['parent'] else p['parent']
  return key
 for pid,p in ps.items():
  a=asset(p['guid'])
  if p['oldgo'] and got.get(p['oldgo']):rt=got[p['oldgo']]
  else:rt=source_node(pid,a['root'])
  rootmap[pid]=rt
 for i,t in trs.items():
  if t['stripped'] and t['pi'] in ps:source_node(t['pi'],t['source'])
 added_checks=[]
 for pid,p in ps.items():
  for src,child in p['added']:
   parent=source_node(pid,src); child=got.get(child,child)
   if child in nodes:
    added_checks.append(nodes[child]['parent']==parent)
    nodes[child]['parent']=parent
 children=defaultdict(list)
 for n,t in nodes.items():children[t['parent']].append(n)
 def descendants(n):
  seen=set(); stack=list(children[n])
  while stack:
   x=stack.pop()
   if x in seen:continue
   seen.add(x);stack.extend(children[x])
  return seen
 def world(n,seen=None):
  if not n or n not in nodes:return [0,0,0],[0,0,0,1],[1,1,1]
  t=nodes[n]; pp,pr,pscale=world(t['parent']); lp,lr,ls=t['pose']; d=rot(pr,[lp[k]*pscale[k] for k in range(3)])
  return [pp[k]+d[k] for k in range(3)],qmul(pr,lr),[pscale[k]*ls[k] for k in range(3)]
 def item(pid):
  p=ps[pid]; n=rootmap[pid]; a=asset(p['guid']); t=nodes[n]; wp,wr,ws=world(n)
  return {'instance_id':pid,'root_transform_id':n,'root_go_id':p['oldgo'] or a['rootgo'],'name':gos.get(t['go'],{}).get('name') or t.get('name') or a['go'].get(a['rootgo'],''),'guid':p['guid'],'prefab':a['path'],'local_pose':t['pose'],'world_position':wp,'world_rotation':wr,'world_scale':ws,'line':p['line'],'added_gameobjects':len(p['added'])}
 targets=[]
 for pid,p in ps.items():
  if p['guid'] not in GUIDS.values():continue
  it=item(pid); n=rootmap[pid]; desc=descendants(n); bp,br,bs=world(n); entries=[]
  for opid,on in rootmap.items():
   if on not in desc:continue
   x=item(opid); rp=rot(inv(br),[x['world_position'][k]-bp[k] for k in range(3)]); x['building_local_position']=[round(rp[k]/bs[k],4) for k in range(3)]; x['direct']=nodes[on]['parent']==n;entries.append(x)
  pnodes=set(rootmap.values()); loose=[]
  for dn in desc:
   if isinstance(dn,int) and dn not in pnodes and dn in trs and not trs[dn]['stripped'] and trs[dn]['go'] in gos:
    loose.append({'transform_id':dn,'name':gos[trs[dn]['go']]['name'],'parent_id':nodes[dn]['parent'],'local_pose':nodes[dn]['pose']})
  it.update({'descendant_prefabs':entries,'plain_objects':loose,'direct_prefab_count':sum(x['direct'] for x in entries),'indirect_prefab_count':sum(not x['direct'] for x in entries)})
  targets.append(it)
 stamp=datetime.fromtimestamp(path.stat().st_mtime).astimezone().isoformat()
 return {'scene':str(path.relative_to(ROOT)).replace('\\','/'),'disk_last_write':stamp,'targets':targets,'added_parent_links':{'checked':len(added_checks),'already_match':sum(added_checks)},'unresolved_prefab_guids':list({p['guid'] for p in ps.values() if not asset(p['guid'])['path']})}
if __name__=='__main__':
 paths=['Assets/Scenes/3_Chapter01.unity'] if '--sample' in sys.argv else ['Assets/Scenes/3_Chapter01.unity','Assets/Scenes/3_Detailed_Reference_Scene.unity']
 reports=[analyze(ROOT/p) for p in paths]
 out=OUT/'saved_buildings_inspection.json'; out.write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
 for r in reports:
  print('SCENE',r['scene'],'DISK',r['disk_last_write'],'ADDED',r['added_parent_links'],'UNRESOLVED',len(r['unresolved_prefab_guids']))
  for t in r['targets']:
   print(json.dumps({k:v for k,v in t.items() if k not in ('descendant_prefabs','plain_objects','local_pose')},ensure_ascii=False),'plain',len(t['plain_objects']))
 print('OUTPUT',out)
