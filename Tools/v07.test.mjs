import test from 'node:test';import assert from 'node:assert/strict';import {existsSync,readFileSync,readdirSync} from 'node:fs';import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const read=p=>readFileSync(new URL('../'+p,import.meta.url),'utf8');
test('Core retains zero UnityEngine dependencies and the fixed 60Hz / 3m grid',()=>{for(const file of readdirSync(new URL('../Unity/Assets/Frontline/Core/',import.meta.url)).filter(f=>f.endsWith('.cs')))assert.doesNotMatch(read('Unity/Assets/Frontline/Core/'+file),/\bUnityEngine\b/);assert.match(read('Unity/Assets/Frontline/Core/Rules.cs'),/Step=1f\/60f/);assert.match(read('Unity/Assets/Frontline/Core/Battle.cs'),/Math.Floor\(x\/3\)/);});
function gitObject(kind,body) {
  return createHash('sha1').update(kind+' '+body.length+'\0').update(body).digest();
}
// These frozen trees contain only non-executable UTF-8 files stored with LF in Git.
function sourceTree(path) {
  const entries=readdirSync(path,{withFileTypes:true}).sort((a,b)=>Buffer.compare(
    Buffer.from(a.name+(a.isDirectory()?'/':'')),Buffer.from(b.name+(b.isDirectory()?'/':''))));
  return gitObject('tree',Buffer.concat(entries.flatMap(entry=>{
    const folder=entry.isDirectory();
    assert.ok(folder||entry.isFile(),entry.name);
    const url=new URL(entry.name+(folder?'/':''),path);
    const object=folder?sourceTree(url):gitObject('blob',Buffer.from(readFileSync(url,'utf8').replaceAll('\r\n','\n')));
    return [Buffer.from((folder?'40000 ':'100644 ')+entry.name+'\0'),object];
  })));
}
test('Frozen legacy source trees remain byte-identical to the handoff trees',()=>{
  const expected={src:'7f09629cfca1ebe5c4c27fcb034857c8940759ef',test:'7e1293310f2d3d79c2d4cb2498d9d85df598e25a',scripts:'192e32422c014cacd2384f298736aaf2dedea9df'};
  const root=new URL('../',import.meta.url);
  for(const [path,sha] of Object.entries(expected))
    assert.equal(sourceTree(new URL(path+'/',root)).toString('hex'),sha,path);
  if(!existsSync(new URL('.git',root)))return;
  const git=args=>execFileSync('git',args,{cwd:root,encoding:'utf8'}).trim();
  try {
    for(const [path,sha] of Object.entries(expected))assert.equal(git(['rev-parse','HEAD:'+path]),sha,path);
    assert.equal(git(['diff','HEAD','--name-only','--',...Object.keys(expected)]),'');
  } catch(error) {
    if(error.code!=='ENOENT')throw error;
  }
});
test('Visual ecology cannot instantiate colliders, mutate terrain or consume battle RNG',()=>{const source=read('Unity/Assets/Frontline/Runtime/MapEcology.cs');assert.doesNotMatch(source,/new GameObject|AddComponent|\.Height\s*=|Random\.Next/);assert.match(source,/new System.Random/);assert.doesNotMatch(read('Unity/Assets/Frontline/Runtime/BattleView.cs'),/CreatePrimitive|Resources.Load<Mesh>\("(?:Cube|Sphere|Cylinder)"/);});
test('Shaders retain instancing, vertex colour, fog and wind on both platforms',()=>{const shader=read('Unity/Assets/Frontline/Resources/FrontlineInstanced.shader');for(const feature of ['multi_compile_instancing','multi_compile_fog','color:COLOR','UNITY_DEFINE_INSTANCED_PROP','WindVertex','SHADOWCASTER'])assert.ok(shader.toUpperCase().includes(feature.toUpperCase()),feature);});
test('Historical v0.6/v0.7 engines remain accepted while v0.9 has a distinct identity',()=>{const replay=read('Unity/Assets/Frontline/Core/Replay.cs');assert.match(replay,/Engine="unity-0.9.0"/);assert.match(replay,/p.engineVersion=="unity-0.6.0"/);assert.match(replay,/p.engineVersion=="unity-0.7.0"/);assert.match(read('Unity/Assets/Frontline/Runtime/FrontlineApp.cs'),/replay.engineVersion=="unity-0.6.0"\?\(IWorldFactory\)new LegacyUnityWorldFactory/);});
test('Baked resource manifest has both LODs for all required assets',()=>{const manifest=JSON.parse(read('Unity/Assets/Frontline/Models/model-manifest.json'));for(const name of ['body_militia','body_shield','body_spear','body_archer','body_crossbow','body_bomber','body_medic','body_giant','body_cavalry','body_catapult','horse','wreck_horse','catapult','wreck_catapult','prop_barrel','prop_barricade','prop_banner','prop_spikes','prop_spring','ring','island_root','mist'])for(const suffix of ['', '_lod1'])assert.ok(manifest.resources.includes('Models/'+name+suffix));});
