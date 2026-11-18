import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync, existsSync, readdirSync} from 'node:fs';
import {createHash} from 'node:crypto';

const url = path => new URL('../' + path, import.meta.url);
const read = path => readFileSync(url(path),'utf8');
const sha = path => createHash('sha256').update(read(path).replaceAll('\r\n','\n')).digest('hex');
const root = 'Unity/Assets/Frontline/';

test('1.0 ships the same single Unity scene and matching player, package and model versions', () => {
  for (const p of ['package.json','package-lock.json',root+'Models/model-manifest.json'])
    assert.equal(JSON.parse(read(p)).version,'1.0.0',p);
  assert.match(read(root+'Editor/BuildTools.cs'),/bundleVersion="1\.0\.0"/);
  assert.deepEqual(readdirSync(url('Unity/Assets/Scenes/')).filter(s => s.endsWith('.unity')),['Frontline.unity']);
});
test('manor buildings and civilian props have unique baked mesh pairs', () => {
  const {resources} = JSON.parse(read(root+'Models/model-manifest.json'));
  assert.equal(resources.length,146);
  assert.equal(new Set(resources).size,146);
  for (const name of ['manor_hall','cottage','farm','timber_camp','smithy','barracks','stables','civilian_hat','civilian_pack']) {
    for (const suffix of ['','_lod1']) {
      const key = 'Models/' + name + suffix;
      assert.ok(resources.includes(key),key);
      assert.ok(existsSync(url(root+'Models/Resources/'+key+'.asset')),key+' baked asset');
    }
  }
});
test('1.0 campaign uses the frozen 0.9 rules, adapter and signed historical rule lists', () => {
  const baseline = {
    'Tools/history08-hashes.json':'f9b7e5dd077578704ed3eeeaab2834f9678db321a9cfce16f616eb2f5ee557e6',
    'Tools/history09-hashes.json':'feafdb913c28c0b109f0f3178969f5725b3da60dd62f6d5ce581a36c4f63ac79',
    [root+'Core/Battle.cs']:'f5cba593081ae55a34544bb35b6d960d8cfe2eabfee3cc70ba308fdf855766ca',
    [root+'Core/CombatMotion.cs']:'4b070e7d22676a6a1733d4819bfbd90905874d7ee9f10605ea8fd69f0f79b4ca',
    [root+'Core/Rules.cs']:'3c2af2a9a4743e6d128dc086cd9abdebba47ad94c9a402772febd71ac023040f',
    [root+'Runtime/UnityPhysics.cs']:'ee9307c7773ccf8a4f5a02d8c94a171a9ebe5a64787ee3eaf6eb18273f7fc1a4'
  };
  for (const [path,hash] of Object.entries(baseline)) assert.equal(sha(path),hash,path);
});

