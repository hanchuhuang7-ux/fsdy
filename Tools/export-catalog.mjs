import fs from 'node:fs';
import {UNITS,PROPS,MAPS,MISSIONS,formation,defaultConfig,validateConfig} from '../src/data.js';
const root=new URL('../Unity/Assets/Frontline/Resources/',import.meta.url);
const presets=['balanced','rush','volley','phalanx','battery','titans','cavalry','support','siege'];
const catalog={version:'0.6.0',units:Object.entries(UNITS).map(([id,d])=>({id,...d})),props:Object.entries(PROPS).map(([id,d])=>({id,...d})),maps:Object.entries(MAPS).map(([id,d])=>({id,...d})),missions:MISSIONS,formations:presets.map(id=>({id,red:formation(id,'red'),blue:formation(id,'blue')}))};
fs.writeFileSync(new URL('catalog.json',root),JSON.stringify(catalog,null,2));
for(const [name,file]of [['default-battle',defaultConfig()],['large-battle',JSON.parse(fs.readFileSync(new URL('../examples/large-battle.json',import.meta.url),'utf8'))]])fs.writeFileSync(new URL(name+'.json',root),JSON.stringify(validateConfig(file),null,2));
console.log('Exported ten units, six maps, five props, six missions and nine formations');
