#!/usr/bin/env node
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createReplayBattle,validateReplay} from '../src/replay.js';
const [input,output]=process.argv.slice(2);if(!input||!output)throw Error('用法：node Tools/convert-legacy-replay.mjs old-seed.json full-trajectory.json');
const pkg=validateReplay(JSON.parse(await readFile(resolve(input),'utf8')));
if(pkg.kind==='seed') {
 const battle=await createReplayBattle(pkg);
 try {
  const frames=[battle.snapshot()];
  while(!battle.result){battle.step();if(battle.tick%6===0)frames.push(battle.snapshot());}
  const final=battle.snapshot();if(Math.abs(frames.at(-1).time-final.time)>.000001)frames.push(final);
  pkg.kind='trajectory';pkg.frames=frames;pkg.result=battle.result;
  if(pkg.version>=2){pkg.highlights=battle.result.highlights||[];pkg.orderLog=battle.result.orders||pkg.orderLog||[];}
 } finally {battle.dispose();}
}
validateReplay(pkg);await writeFile(resolve(output),JSON.stringify(pkg));console.log(`Converted ${pkg.engineVersion}: ${pkg.frames.length} frames → ${resolve(output)}`);
