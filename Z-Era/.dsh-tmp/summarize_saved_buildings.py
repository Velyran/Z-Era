import json,math,sys
from pathlib import Path
from collections import Counter,defaultdict
P=Path(__file__).resolve().parent
reports=json.loads((P/'saved_buildings_inspection.json').read_text(encoding='utf-8'))
def cat(n):
 n=n.lower()
 if any(x in n for x in ['bed_hospital','bed_gurney','wheelchair','medical','curtain']):return 'medical'
 if any(x in n for x in ['cardboard','crate','barrel']):return 'storage_clutter'
 if any(x in n for x in ['chair','seat','coffeetable','desk']):return 'seating_office'
 if any(x in n for x in ['shelf','checkout']):return 'shelf_checkout'
 return 'utility_misc'
for r in reports:
 print('SCENE',r['scene'],'disk',r['disk_last_write'])
 for t in r['targets']:
  if not t['descendant_prefabs']:continue
  print('TARGET',t['name'],'position',t['world_position'],'instance',t['instance_id'],'direct',t['direct_prefab_count'],'indirect',t['indirect_prefab_count'])
  groups=defaultdict(list)
  for x in t['descendant_prefabs']:
   y=x['building_local_position'][1]
   # nearest structural floor elevation on 3m pitch (origin supports floor y=0.3)
   floor=max(0,int(math.floor((y+1.0)/3)))
   groups[floor].append(x)
  print('PREFIX',dict(Counter(x['name'].split('_SM_')[0] for x in t['descendant_prefabs'])))
  print('TYPES',len(set(x['guid'] for x in t['descendant_prefabs'])),'CATEGORY',dict(Counter(cat(x['name']) for x in t['descendant_prefabs'])))
  for f,xs in sorted(groups.items()):print('LAYER',f,'count',len(xs),'y_range',[min(x['building_local_position'][1] for x in xs),max(x['building_local_position'][1] for x in xs)],'category',dict(Counter(cat(x['name']) for x in xs)))
