import fs from 'node:fs/promises';
import {resolve} from 'node:path';
const root=resolve(new URL('..',import.meta.url).pathname.replace(/^\/(\w:)/,'$1'));
const load=p=>fs.readFile(resolve(root,p),'utf8').then(JSON.parse);
const [smoke,agents,series]=await Promise.all([load('Builds/Smoke/smoke.json'),load('Builds/Agents/agent-stats.json'),load('Builds/Agents/series.json')]);
console.log(JSON.stringify({smoke,agents,series:{wins:series.wins,winner:series.winner,rounds:series.rounds.map(r=>({seed:r.config.seed,time:r.result.time,winner:r.result.winner}))}},null,2));
