import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync, existsSync, readdirSync} from 'node:fs';
import {createHash} from 'node:crypto';

const url = path => new URL('../' + path, import.meta.url);
const read = path => readFileSync(url(path),'utf8');
const sha = path => createHash('sha256').update(read(path).replaceAll('\r\n','\n')).digest('hex');
const root = 'Unity/Assets/Frontline/';

test('3.1.0 ships the same single Unity scene and matching player, package and model versions', () => {
  for (const p of ['package.json','package-lock.json',root+'Models/model-manifest.json'])
    assert.equal(JSON.parse(read(p)).version,'3.1.0',p);
  assert.match(read(root+'Editor/BuildTools.cs'),/bundleVersion="3\.1\.0"/);
  assert.match(read('Unity/ProjectSettings/ProjectSettings.asset'),/^  bundleVersion: 3\.1\.0\r?$/m);
  assert.deepEqual(readdirSync(url('Unity/Assets/Scenes/')).filter(s => s.endsWith('.unity')),['Frontline.unity']);
});
test('manor buildings, hero equipment and civilian props all have unique baked mesh pairs', () => {
  const {resources} = JSON.parse(read(root+'Models/model-manifest.json'));
  assert.equal(resources.length,374);
  assert.equal(new Set(resources).size,resources.length);
  const heroes=JSON.parse(read(root+'Resources/catalog.json')).heroes;
  assert.equal(heroes.length,3);
  const heroMeshes=heroes.flatMap(({id})=>['body_'+id,'arm_right_'+id,'crest_'+id]);
  for (const name of ['manor_hall','cottage','farm','timber_camp','smithy','barracks','stables','civilian_hat','civilian_pack',
    'irrigated_field','pasture','tavern','drill_yard','granary','chapel','bakery','weaver','frontier_gate','bridge_fittings','mill_sails','farmer_clothes','villager_clothes','villager_wrap','resident_sleeve',
    'farm_fallow','farm_rich','irrigated_field_fallow','irrigated_field_rich','retinue_guard',
    ...['steward','merchant','herbalist','watchman','mason','minstrel'].map(id=>'specialist_'+id),
    ...['plaza','well','market','dock','fence','gatehouse','laundry','firewood','straw','roof_snow'].map(id=>'manor_'+id),
    ...['hoe','sickle','bucket','crate','basket','jug'].map(id=>'carry_'+id),
    ...['garden','poultry','workshop','militia_yard'].map(id=>'backyard_'+id),
    ...['manor_hall','cottage','farm','irrigated_field','pasture','timber_camp','smithy','barracks','stables','tavern','drill_yard','granary','chapel','bakery','weaver'].flatMap(id=>[id+'_wing',id+'_tower']),...heroMeshes,'frontier_palisade','arm_braced_left','smith_clothes','carry_hammer',
    ...['road_path','road_link','road_setts','road_grass','plot_ropes','market_cart','verge_wall','verge_flowers','horse_shoulder'],
    ...heroes.flatMap(({id})=>[1,2,3].map(tier=>'arm_right_'+id+'_t'+tier)),
    ...heroes.flatMap(({id})=>[2,3].map(tier=>'crest_'+id+'_t'+tier))]) {
    for (const suffix of ['','_lod1']) {
      const key = 'Models/' + name + suffix;
      assert.ok(resources.includes(key),key);
      assert.ok(existsSync(url(root+'Models/Resources/'+key+'.asset')),key+' baked asset');
    }
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


test('material channels, shadow proxies and the frozen rock hull survive the full editor bake', () => {
  const model = name => read(root+'Models/Resources/Models/'+name+'.asset');
  const layout = mesh => [...mesh.matchAll(/- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)/g)]
    .map(m => ({offset:+m[2], dimension:+m[4]}));
  for (const name of ['manor_hall_tower','cottage','bakery','tree','pine','body_shield','body_spear','horse','arm_right_spear','arena_ground']) {
    const mesh=model(name), channels=layout(mesh);
    assert.deepEqual([channels[4].dimension,channels[5].dimension],[2,4],name);
    const vertices=+mesh.match(/m_VertexCount: (\d+)/)[1];
    const data=Buffer.from(mesh.match(/_typelessdata: ([a-f0-9]+)/i)[1],'hex');
    const stride=data.length/vertices, kinds=new Set();
    for (let at=channels[5].offset;at<data.length;at+=stride) {
      const id=data.readFloatLE(at);kinds.add(id);
      assert.ok(Number.isInteger(id)&&id>=1&&id<=13,name);
      for(const i of [4,8,12])assert.ok(data.readFloatLE(at+i)>=0&&data.readFloatLE(at+i)<=1,name);
    }
    if(name==='body_shield'||name==='arm_right_spear')assert.ok(kinds.has(5)&&kinds.has(6),name);
    if(name==='arena_ground')assert.deepEqual([...kinds],[7]);
    assert.equal(layout(model(name+'_lod1'))[5].dimension,0);
  }
  const hull=Buffer.from(model('rock_lod1').replaceAll('\r\n','\n'));
  assert.equal(createHash('sha1').update('blob '+hull.length+'\0').update(hull).digest('hex'),
    'e3a36e16270490edf0ad5dc58edd93141c86d7e8');
  const manifest=JSON.parse(read(root+'Models/model-manifest.json'));
  assert.ok(!manifest.resources.some(item=>item.includes('water_ripple')));
  for(const texture of manifest.textures){
    const meta=read(root+'Resources/'+texture+'.png.meta');
    assert.match(meta,/enableMipMap: 1/);assert.match(meta,/aniso: [2-9]/);
    for(const target of ['Standalone','WebGL'])assert.match(meta,new RegExp('buildTarget: '+target+'[\\s\\S]*?overridden: 1'));
  }
});

test('the baked village lanes meet at their survey endpoints and every hero has three distinct forged silhouettes', () => {
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
  for(const {id} of JSON.parse(read(root+'Resources/catalog.json')).heroes){
    const silhouettes=[1,2,3].map(t=>createHash('sha256').update(JSON.stringify(geometry('arm_right_'+id+'_t'+t))).digest('hex'));
    assert.equal(new Set(silhouettes).size,3,id);
  }
  const shader=read(root+'Resources/FrontlineRoad.shader');
  assert.match(shader,/Blend SrcAlpha OneMinusSrcAlpha ZWrite Off/);
  assert.match(shader,/only_renderers gles3/);
  assert.match(shader,/forcemaxcount:64/);
});
