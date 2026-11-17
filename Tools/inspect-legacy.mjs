import {readFile} from 'node:fs/promises';
const r=JSON.parse(await readFile(process.argv[2],'utf8'));
console.log(JSON.stringify({version:r.version,engineVersion:r.engineVersion,keys:Object.keys(r),frameKeys:Object.keys(r.frames[0]),unit:r.frames[0].units[0],shot:r.frames.find(f=>f.projectiles.length)?.projectiles[0],corpse:r.frames.find(f=>f.ragdolls.length)?.ragdolls[0],order:r.orderLog[0],highlight:r.highlights[0]},null,2));
