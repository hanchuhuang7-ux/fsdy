import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const read=p=>readFileSync(new URL('../'+p,import.meta.url),'utf8');
test('0.8 battle, movement and physics remain frozen byte for byte',()=>{for(const [p,h] of Object.entries(JSON.parse(read('Tools/history09-hashes.json'))))assert.equal(createHash('sha256').update(read(p).replaceAll('\r\n','\n')).digest('hex'),h,p);});
test('0.8 seed selects frozen battle and factory without upgrading identity',()=>{const s=read('Unity/Assets/Frontline/Runtime/FrontlineApp.cs');assert.match(s,/History08.Battle/);assert.match(s,/new Unity08WorldFactory/);assert.match(read('Unity/Assets/Frontline/Runtime/Unity08Physics.cs'),/using CombatMotion=Frontline.Core.History08.CombatMotion/);});
test('cavalry entrance filters layers and teams; walls own secondary feedback listeners',()=>{const s=read('Unity/Assets/Frontline/Runtime/UnityPhysics.cs');const entrance=s.slice(s.indexOf('public void UnitContact'),s.indexOf('public void ObstacleContact'));assert.match(entrance,/layer!=UnitLayer/);assert.match(entrance,/other.team==body.team/);assert.match(entrance,/if\(!body.fast\)return/);assert.match(s,/class ObstacleContacts/);assert.match(s,/collision.GetContact\(0\)/);assert.match(s,/if\(d.id=="cavalry"\)/);assert.match(s,/rigidbody.MoveRotation/);});
test('wall feedback and visible batched strokes require no new physics or texture',()=>{
  const s=read('Unity/Assets/Frontline/Runtime/BattleView.cs');
  const joints=read('Unity/Assets/Frontline/Runtime/BattleView.IK.cs');
  const meshes=read('Unity/Assets/Frontline/Runtime/BattleView.JointMeshes.cs');
  assert.match(s,/e.type=="wall"/);
  assert.match(s,/e.type=="sweep"/);
  assert.match(joints,/float block=Ease\(input\.guard\)/);
  assert.match(joints,/rig\.shield=pose\.root\*AtGrip\(left,shieldTurn,/);
  assert.match(meshes,/Add\(shield,rig\.shield,input\.color\)/);
  assert.match(read('Unity/Assets/Frontline/Runtime/BattleAudio.cs'),/"wall"/);
});

const runtime='Unity/Assets/Frontline/Runtime/';
test('0.9.5 only reports cancellation for a running job',()=>{
  const app=read(runtime+'FrontlineApp.cs');
  const cancel=app.slice(app.indexOf('public void CancelJob()'),app.indexOf('public void RequestStart()'));
  assert.match(app,/HasJob\s*=>\s*Busy\s*\|\|\s*job != null\s*\|\|\s*activeJob != null/);
  assert.match(cancel,/bool wasRunning=HasJob;[\s\S]*if\(wasRunning\)\{Tell\("已停止计算"\)/);
  assert.equal(cancel.split('Tell("已停止计算")').length,2);
  assert.match(app,/if\(returned\)Tell\("已返回布阵"\)/);
});

test('0.9.5 HUD updates only dirty readouts and shades the desktop map title without blocking input',()=>{
  const hud=read(runtime+'NativeHud.cs');
  const updates=hud.slice(hud.indexOf('void ShowNotice('));
  assert.doesNotMatch(updates,/\.Count\(|\.Skip\(|\.Select\(|string\.Join\(/);
  assert.match(updates,/shownTick!=tick/);
  assert.match(updates,/shownHighlightCount!=highlights.Count/);
  assert.match(updates,/shownReplayTime!=app.ReplayTime/);
  assert.match(hud,/Float\(baseRoot,"Map title",[^\n]+,Slice\.Rail\);\s*mapTitle.GetComponent<Image>\(\).raycastTarget=false/);
});

test('0.9.5 static pages upload only when dirty and spear streaks use the weapon height',()=>{
  const view=read(runtime+'BattleView.cs');
  assert.match(view,/q\.y\s*\+=\s*thrust\s*\?\s*\.05f\s*:\s*\.25f/);
  assert.match(view,/void Clear\(\)\{count=0;if\(fixedPage\)visibilityVersion=-1/);
  for(const [channel,method] of [['color','SetVectorArray'],['flash','SetFloatArray'],['wind','SetFloatArray']])
    assert.match(view,new RegExp('if\\(page\\.'+channel+'Dirty[^)]*\\)\\{page\\.properties\\.'+method+'[^;]*;page\\.'+channel+'Dirty=false'));
  assert.match(view,/foreach\(var b in still\)Draw\(b,true\);if\(Deploying\)foreach\(var b in grid\)Draw\(b,true\)/);
  assert.match(view,/mesh\.moving\?\?Buffer\(mesh,moving,0\)/);
});

test('0.9.5 preserves the replay identity, formation schema and physics turn threshold',()=>{
  assert.match(read('Unity/Assets/Frontline/Core/Replay.cs'),/Engine="unity-0\.9\.0"/);
  assert.match(read('Unity/Assets/Frontline/Core/Models.cs'),/version="0\.6\.0"/);
  assert.match(read(runtime+'UnityPhysics.cs'),/Math\.Abs\(angle\s*-\s*facing\)\s*>\s*\.015f/);
});
