import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync, existsSync, readdirSync} from 'node:fs';
import {createHash} from 'node:crypto';

const url = path => new URL('../' + path, import.meta.url);
const read = path => readFileSync(url(path),'utf8');
const sha = path => createHash('sha256').update(read(path).replaceAll('\r\n','\n')).digest('hex');
const root = 'Unity/Assets/Frontline/';

test('4.0.0 ships the same single Unity scene and matching player, package and model versions', () => {
  for (const p of ['package.json','package-lock.json',root+'Models/model-manifest.json'])
    assert.equal(JSON.parse(read(p)).version,'4.0.0',p);
  assert.match(read(root+'Editor/BuildTools.cs'),/bundleVersion="4\.0\.0"/);
  assert.match(read('Unity/ProjectSettings/ProjectSettings.asset'),/^  bundleVersion: 4\.0\.0\r?$/m);
  assert.deepEqual(readdirSync(url('Unity/Assets/Scenes/')).filter(s => s.endsWith('.unity')),['Frontline.unity']);
});
const manifest=()=>JSON.parse(read(root+'Models/model-manifest.json'));
const baked=name=>read(root+'Models/Resources/Models/'+name+'.asset');
const bytes=path=>readFileSync(url(path));
const hash=data=>createHash('sha256').update(data).digest('hex');

test('artist-built manor tiers, crop states, equipment and civilian roles have complete LODs',()=>{
  const m=manifest(),classes=['manor_hall','cottage','farm','irrigated_field','pasture','timber_camp','smithy','barracks','stables','tavern','drill_yard','granary','chapel','bakery','weaver'];
  assert.equal(new Set(m.resources).size,m.resources.length);
  const ids=[...classes,...['farm','irrigated_field'].flatMap(t=>[t+'_fallow',t+'_rich'])];
  for(const id of ids)for(const tier of ['','_wing','_tower'])for(const lod of ['','_lod1']){
    const key=id+tier+lod;assert.ok(m.resources.includes('Models/'+key),key);assert.ok(existsSync(url(root+'Models/Resources/Models/'+key+'.asset')),key);
  }
  for(const id of ['manor_plaza','manor_well','manor_dock','manor_market','frontier_gate','frontier_palisade','bridge_fittings','catapult','catapult_arm','retinue_guard',
    'backyard_garden','backyard_poultry','backyard_workshop','backyard_militia_yard','tree','pine','shrub','rock','shore_rock'])
    for(const lod of ['','_lod1'])assert.ok(m.resources.includes('Models/'+id+lod),id);
  const costumes=new Map(m.costumes.map(c=>[c.name,c]));
  for(const id of ['charger','militia','shield','spear','archer','crossbow','cavalry','giant','medic','bomber','watch','villager','farmer','steward','merchant','watchman','herbalist','mason','minstrel']){
    const c=costumes.get(id);assert.ok(c&&c.main>c.mid&&c.mid>c.proxy,id);
  }
  for(const {id} of JSON.parse(read(root+'Resources/catalog.json')).heroes){
    const variants=[0,1,2,3].map(i=>costumes.get(id+(i?'_t'+i:'')));
    assert.ok(variants.every(c=>c.attachments>0&&c.attachmentHash.length===64));
    assert.equal(new Set(variants.map(c=>c.attachmentHash)).size,4,id);
  }
});

test('1.2 preserves historical rule signatures and both expedition replay identities', () => {
  const baseline = {
    'Tools/history08-hashes.json':'f9b7e5dd077578704ed3eeeaab2834f9678db321a9cfce16f616eb2f5ee557e6',
    'Tools/history09-hashes.json':'feafdb913c28c0b109f0f3178969f5725b3da60dd62f6d5ce581a36c4f63ac79',
    [root+'Core/CombatMotion.cs']:'4b070e7d22676a6a1733d4819bfbd90905874d7ee9f10605ea8fd69f0f79b4ca',
  };
  for (const [path,hash] of Object.entries(baseline)) assert.equal(sha(path),hash,path);
  const replay=read(root+'Core/Replay.cs');
  assert.match(replay,/Engine="unity-0\.9\.0"/);
  assert.match(replay,/ExpeditionEngine="unity-manor-1\.2\.0"/);
  assert.match(replay,/LegacyExpeditionEngine="unity-manor-1\.1\.0"/);
  assert.match(replay,/ArmamentExpeditionEngine="unity-manor-1\.7\.0"/);
  assert.match(read(root+'Core/Rules.cs'),/Validate\(Deployment input,bool expedition=false\)/);
});


test('scanned material arrays, cutout trees and the frozen PhysX hulls survive a clean bake',()=>{
  const layout=mesh=>[...mesh.matchAll(/- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)/g)].map(m=>({offset:+m[2],dimension:+m[4]}));
  for(const name of ['manor_hall_tower','cottage','bakery','tree','pine','rock','arena_ground']){
    const mesh=baked(name),channels=layout(mesh);
    assert.deepEqual([channels[4].dimension,channels[5].dimension],[2,4],name);
    const count=+mesh.match(/m_VertexCount: (\d+)/)[1],data=Buffer.from(mesh.match(/_typelessdata: ([a-f0-9]+)/i)[1],'hex');
    const ids=new Set();for(let p=channels[5].offset;p<data.length;p+=data.length/count){
      const id=data.readFloatLE(p);ids.add(id);assert.ok(Number.isInteger(id)&&id>=1&&id<128,name);
      for(const k of [4,8,12])assert.ok(data.readFloatLE(p+k)>=0&&data.readFloatLE(p+k)<=1,name);
    }
    if(name==='tree'||name==='pine')assert.ok([...ids].some(i=>i>=64&&i<112)&&[...ids].some(i=>i>=32&&i<64));
    if(name==='arena_ground')assert.deepEqual([...ids],[7]);
  }
  const hull=Buffer.from(baked('rock_lod1').replaceAll('\r\n','\n'));
  assert.equal(createHash('sha1').update('blob '+hull.length+'\0').update(hull).digest('hex'),'e3a36e16270490edf0ad5dc58edd93141c86d7e8');
  const m=manifest();assert.ok(!m.resources.some(n=>n.includes('water_ripple')));
  for(const name of m.textures){
    const base=root+(name.startsWith('Scans/')?'ThirdParty/Resources/':'Resources/')+name+'.png';
    const meta=read(base+'.meta');assert.match(meta,/enableMipMap: 1/);assert.match(meta,/aniso: [2-9]/);
    for(const target of ['Standalone','WebGL'])assert.match(meta,new RegExp('buildTarget: '+target+'[\\s\\S]*?overridden: 1'));
    if(name.startsWith('Scans/'))assert.match(meta,/flipbookColumns: [348]/);
  }
});

test('the unchanged survey endpoints join both terrain LODs and the roads use feathered scans', () => {
  const model=name=>read(root+'Models/Resources/Models/'+name+'.asset');
  const geometry=name=>{
    const source=model(name),count=+source.match(/m_VertexCount: (\d+)/)[1];
    const bytes=Buffer.from(source.match(/_typelessdata: ([a-f0-9]+)/i)[1],'hex');
    const stride=bytes.length/count;
    return Array.from({length:count},(_,i)=>[0,4,8].map(offset=>bytes.readFloatLE(i*stride+offset)));
  };
  for(const suffix of ['','_lod1']){
    const rows=new Map();
    for(const [x,y,z] of geometry('road_path'+suffix)){
      assert.equal(y,0);
      const edge=rows.get(z)??[Infinity,-Infinity];
      edge[0]=Math.min(edge[0],x);edge[1]=Math.max(edge[1],x);rows.set(z,edge);
    }
    for(const z of [-.5,.5]){
      assert.ok(Math.abs(rows.get(z)[0]+.5)<1e-5&&Math.abs(rows.get(z)[1]-.5)<1e-5);
    }
    assert.ok([...rows.values()].some(([a,b])=>Math.abs(a+b)*.5>.12));
  }
  const shader=read(root+'Resources/FrontlineRoad.shader');
  assert.match(shader,/Blend SrcAlpha OneMinusSrcAlpha ZWrite Off/);
  assert.match(shader,/only_renderers gles3/);
  assert.match(shader,/forcemaxcount:64/);
});


test('all shipped FBXs are curated, credited and tied to a pinned CC0 artist source',()=>{
  const m=manifest(),lock=JSON.parse(read('Tools/cc0-sources.json')),credits=read(root+'ThirdParty/CREDITS-CC0.md');
  assert.equal(m.generator,'Frontline.Editor.ExternalAssetImporter');
  assert.deepEqual(readdirSync(url(root+'ThirdParty/Art')).filter(n=>n.endsWith('.fbx')).sort(),m.sources.map(n=>n+'.fbx').sort());
  assert.deepEqual(readdirSync(url(root+'ThirdParty/Actors')).filter(n=>n.endsWith('.fbx')).sort(),m.skeletons.map(n=>n.name+'.fbx').sort());
  for(const source of lock.files){
    assert.match(source.sha256,/^[a-f0-9]{64}$/);
    assert.ok(lock.sources[source.source].license.startsWith('https://'));
    assert.ok(credits.includes(source.sha256),source.path);
  }
  for(const rig of m.skeletons){
    const path=root+'ThirdParty/Actors/'+rig.name+'.fbx';
    const data=bytes(path);assert.equal(data.subarray(0,18).toString(),'Kaydara FBX Binary');
    assert.ok(rig.boneCount>=30&&rig.skinnedVertices>=500&&rig.takes.length>=14,rig.name);
    assert.ok(credits.includes(hash(data)),rig.name);
  }
  assert.ok(existsSync(url(root+'ThirdParty/Resources/CC0/ArtistRoster.asset')));
  const shader=read(root+'Resources/FrontlineInstanced.shader');
  assert.match(shader,/JointRows\(v\.joints\.w,row,v\.weights\.w/);
  assert.match(shader,/#pragma only_renderers gles3/);assert.match(shader,/forcemaxcount:64/);
});
