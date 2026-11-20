import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync, existsSync, readdirSync} from 'node:fs';
import {createHash} from 'node:crypto';

const url = path => new URL('../' + path, import.meta.url);
const read = path => readFileSync(url(path),'utf8');
const sha = path => createHash('sha256').update(read(path).replaceAll('\r\n','\n')).digest('hex');
const root = 'Unity/Assets/Frontline/';

test('1.6.0 ships the same single Unity scene and matching player, package and model versions', () => {
  for (const p of ['package.json','package-lock.json',root+'Models/model-manifest.json'])
    assert.equal(JSON.parse(read(p)).version,'1.6.0',p);
  assert.match(read(root+'Editor/BuildTools.cs'),/bundleVersion="1\.6\.0"/);
  assert.match(read('Unity/ProjectSettings/ProjectSettings.asset'),/^  bundleVersion: 1\.6\.0\r?$/m);
  assert.deepEqual(readdirSync(url('Unity/Assets/Scenes/')).filter(s => s.endsWith('.unity')),['Frontline.unity']);
});
test('manor buildings, hero equipment and civilian props all have unique baked mesh pairs', () => {
  const {resources} = JSON.parse(read(root+'Models/model-manifest.json'));
  assert.equal(resources.length,310);
  assert.equal(new Set(resources).size,resources.length);
  const heroes=JSON.parse(read(root+'Resources/catalog.json')).heroes;
  assert.equal(heroes.length,3);
  const heroMeshes=heroes.flatMap(({id})=>['body_'+id,'arm_right_'+id]);
  for (const name of ['manor_hall','cottage','farm','timber_camp','smithy','barracks','stables','civilian_hat','civilian_pack',
    'irrigated_field','pasture','tavern','drill_yard','granary','chapel','bakery','weaver','frontier_gate','bridge_fittings','mill_sails','farmer_clothes','villager_clothes','villager_wrap','resident_sleeve',
    'farm_fallow','farm_rich','irrigated_field_fallow','irrigated_field_rich','retinue_guard',
    ...['steward','merchant','herbalist','watchman','mason','minstrel'].map(id=>'specialist_'+id),
    ...['plaza','well','market','dock','fence','gatehouse','laundry','firewood','straw','roof_snow'].map(id=>'manor_'+id),
    ...['hoe','sickle','bucket','crate','basket','jug'].map(id=>'carry_'+id),
    ...['garden','poultry','workshop','militia_yard'].map(id=>'backyard_'+id),
    ...['manor_hall','cottage','farm','irrigated_field','pasture','timber_camp','smithy','barracks','stables','tavern','drill_yard','granary','chapel','bakery','weaver'].flatMap(id=>[id+'_wing',id+'_tower']),...heroMeshes]) {
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
  assert.match(read(root+'Core/Rules.cs'),/Validate\(Deployment input,bool expedition=false\)/);
});

