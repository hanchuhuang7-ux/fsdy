import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const read=p=>readFileSync(new URL('../'+p,import.meta.url),'utf8');
test('0.8 battle, movement and physics remain frozen byte for byte',()=>{for(const [p,h] of Object.entries(JSON.parse(read('Tools/history09-hashes.json'))))assert.equal(createHash('sha256').update(read(p).replaceAll('\r\n','\n')).digest('hex'),h,p);});
test('0.8 seed selects frozen battle and factory without upgrading identity',()=>{const s=read('Unity/Assets/Frontline/Runtime/FrontlineApp.cs');assert.match(s,/History08.Battle/);assert.match(s,/new Unity08WorldFactory/);assert.match(read('Unity/Assets/Frontline/Runtime/Unity08Physics.cs'),/using CombatMotion=Frontline.Core.History08.CombatMotion/);});
test('cavalry entrance filters layers and teams; walls own secondary feedback listeners',()=>{const s=read('Unity/Assets/Frontline/Runtime/UnityPhysics.cs');const entrance=s.slice(s.indexOf('public void UnitContact'),s.indexOf('public void ObstacleContact'));assert.match(entrance,/layer!=UnitLayer/);assert.match(entrance,/other.team==body.team/);assert.match(entrance,/if\(!body.fast\)return/);assert.match(s,/class ObstacleContacts/);assert.match(s,/collision.GetContact\(0\)/);assert.match(s,/if\(d.id=="cavalry"\)/);assert.match(s,/rigidbody.MoveRotation/);});
test('wall feedback and visible batched strokes require no new physics or texture',()=>{const s=read('Unity/Assets/Frontline/Runtime/BattleView.cs');assert.match(s,/e.type=="wall"/);assert.match(s,/e.type=="sweep"/);assert.match(s,/guard\*\.15f/);assert.match(read('Unity/Assets/Frontline/Runtime/BattleAudio.cs'),/"wall"/);});
