import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const read=p=>readFileSync(new URL('../'+p,import.meta.url),'utf8');
test('frozen historical rules and both physics adapters retain audited hashes',()=>{
 for(const [p,expected] of Object.entries(JSON.parse(read('Tools/history08-hashes.json'))))assert.equal(createHash('sha256').update(read(p).replaceAll('\r\n','\n')).digest('hex'),expected,p);
});
test('historical seed expansion preserves its engine identity and matching rules',()=>{
 const s=read('Unity/Assets/Frontline/Runtime/FrontlineApp.cs');
 assert.match(s,/History07.Battle/);assert.match(s,/new Unity07WorldFactory/);assert.match(s,/frames,b.Result,false,replay.engineVersion/);
});
test('hit flash is an instance channel and feedback is bounded without hit sampling',()=>{
 const s=read('Unity/Assets/Frontline/Core/Battle.cs');const hit=s.slice(s.indexOf('public void Hit('),s.indexOf('void Kill('));
 assert.doesNotMatch(hit,/%6/);assert.match(s,/effectPool.Pop/);assert.match(s,/lastVisualTick/);
 assert.match(read('Unity/Assets/Frontline/Resources/FrontlineInstanced.shader'),/UNITY_DEFINE_INSTANCED_PROP\(float,_HitFlash\)/);
 assert.match(read('Unity/Assets/Frontline/Runtime/BattleView.cs'),/SetFloatArray\(HitId/);
});
test('native acceptance executes actual collision, projectile and full historical goldens',()=>{
 const s=read('Unity/Assets/Frontline/Runtime/HeadlessRunner.cs');
 assert.match(s,/CombatPhysicsChecks.Run/);assert.match(s,/HistoricalChecks.Run/);
 const golden=JSON.parse(read('Unity/Assets/Frontline/Resources/history-goldens.json'));
 assert.deepEqual(golden.map(g=>[g.engine,g.tick,g.winner]),[['unity-0.6.0',1847,'red'],['unity-0.7.0',2279,'blue'],['unity-0.8.0',1468,'blue']]);
});
