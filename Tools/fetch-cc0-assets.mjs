import {createHash} from 'node:crypto';
import {mkdir,readFile,rename,writeFile,rm,readdir} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import {dirname,basename,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';

if(process.argv.includes('--help')){
  console.log('node Tools/fetch-cc0-assets.mjs [--prepare]\n'+
    'Verifies cached files and ZIP members, then downloads missing CC0 source files.\n'+
    'Use --prepare to regenerate the selected FBXs and compressed-array sources with Blender, numpy and Pillow.\n'+
    'BLENDER_PATH and CC0_PYTHON select executables. A normal Unity build uses the checked-in curated art.');
  process.exit(0);
}
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const cache=join(root,'ThirdPartyAssets'),destination=join(root,'Unity/Assets/Frontline/ThirdParty');
const lockFile=join(root,'Tools/cc0-sources.json');
const lock=JSON.parse(await readFile(lockFile,'utf8'));
const headers={'User-Agent':'FrontlineLab-CC0-Importer/4.0 (+https://github.com/hanchuhuang7-ux/fsdy)'};
const digest=(data,algo='sha256')=>createHash(algo).update(data).digest('hex');
const gitBlob=data=>createHash('sha1').update('blob '+data.length+'\0').update(data).digest('hex');
function valid(data,file) {
  return data.length===file.bytes && (!file.sha256||file.sha256===digest(data)) &&
    (!file.gitBlob||file.gitBlob===gitBlob(data)) && (!file.md5||file.md5===digest(data,'md5'));
}
await mkdir(cache,{recursive:true});
for(const record of lock.archives??[]) {
  const members=lock.files.filter(f=>f.archive===record.name);
  let complete=true;
  for(const member of members){
    const path=join(cache,member.path);
    if(!existsSync(path)||!valid(await readFile(path),member)){complete=false;break;}
  }
  if(complete)continue;
  const archive=join(cache,record.name);
  if(!existsSync(archive)) {
    const page=await fetch(record.page,{headers});
    if(!page.ok)throw Error('Unable to read '+record.page);
    const jar=new Map();
    function keep(res){for(const raw of res.headers.getSetCookie()){const s=raw.split(';')[0],i=s.indexOf('=');if(i>0)jar.set(s.slice(0,i),s.slice(i+1));}return res;}
    function formHeaders(referer){return {...headers,Referer:referer,Cookie:[...jar].map(([k,v])=>k+'='+v).join('; ')};}
    keep(page);
    let link;
    if(record.provider==='itch-standard') {
      const html=await page.text();
      const csrf=html.match(/<meta name="csrf_token" value="([^"]+)"/)?.[1]??html.match(/<meta content="([^"]+)" name="csrf_token"/)?.[1];
      if(!csrf)throw Error('Missing publisher download form');
      const post=async(url,referer,values)=>keep(await fetch(url,{method:'POST',headers:formHeaders(referer),
        body:new URLSearchParams({csrf_token:csrf,...values})})).json();
      const grant=(await post(record.page+'/download_url',record.page,{})).url;
      if(new URL(grant).origin!==new URL(record.page).origin)throw Error("Unexpected download-page host");
      const offered=await keep(await fetch(grant,{headers:formHeaders(record.page)})).text();
      if(!offered.includes('data-upload_id="'+record.upload+'"')||!offered.includes('[Standard].zip'))throw Error('Free Standard edition changed');
      link=(await post(record.page+'/file/'+record.upload,grant,{source:'game_download'})).url;
    } else link=(await (await fetch(record.ticket,{headers:formHeaders(record.page)})).json()).url;
    const archiveURL=new URL(link,record.page);
    if(archiveURL.protocol!=='https:')throw Error('Invalid publisher download URL');
    const bytes=Buffer.from(await (await fetch(archiveURL,{headers:{...headers,Referer:record.page}})).arrayBuffer());
    if(digest(bytes)!==record.sha256)throw Error('Archive checksum mismatch: '+record.name);
    const temporary=archive+'.part-'+process.pid;
    try{await writeFile(temporary,bytes);await rename(temporary,archive);}finally{await rm(temporary,{force:true});}
  } else if(digest(await readFile(archive))!==record.sha256)throw Error('Archive checksum mismatch: '+record.name);
}
const archives=(await readdir(cache)).filter(n=>n.toLowerCase().endsWith('.zip'));
const archiveEntries=new Map();
for(const archive of archives) {
  const path=join(cache,archive);
  const result=spawnSync('tar',['-tf',path],{encoding:'utf8',maxBuffer:32*1024*1024});
  if(result.status!==0){if(lock.archives.some(a=>a.name===archive))throw Error('Unreadable source archive '+archive);continue;}
  for(const entry of result.stdout.split(/\r?\n/)) {
    if(!entry||entry.endsWith('/')||entry.startsWith('-')||entry.split(/[\\/]/).includes('..'))continue;
    const name=basename(entry);
    if(!archiveEntries.has(name))archiveEntries.set(name,[]);
    archiveEntries.get(name).push({path,entry});
  }
}
async function acquire(file) {
  const output=join(cache,file.path);
  if(existsSync(output)) {
    const data=await readFile(output);
    if(valid(data,file))return digest(data);
  }
  await mkdir(dirname(output),{recursive:true});
  const matches=archiveEntries.get(basename(file.path))??[];
  for(const item of matches) {
    // Extract one vetted member to stdout, never install archive paths or execute bundled content.
    if(file.archive&&(basename(item.path)!==file.archive||item.entry!==file.entry))continue;
    const r=spawnSync('tar',['-xOf',item.path,item.entry.replace(/[\[\]]/g,'\\$&')],{maxBuffer:file.bytes+4096});
    if(r.status===0&&valid(r.stdout,file)){await writeFile(output,r.stdout);return digest(r.stdout);}
  }
  if(file.archive)throw Error('Missing or modified archive member '+file.entry);
  let last;
  for(let attempt=0;attempt<3;attempt++) {
    try {
      const res=await fetch(file.url,{headers,signal:AbortSignal.timeout(180000)});
      if(!res.ok)throw Error('HTTP '+res.status);
      const bytes=Buffer.from(await res.arrayBuffer());
      if(!valid(bytes,file))throw Error('Publisher size/hash mismatch ('+bytes.length+' bytes)');
      const temp=output+'.part-'+process.pid;
      try {await writeFile(temp,bytes);await rename(temp,output);}finally{await rm(temp,{force:true});}
      return digest(bytes);
    }catch(err){last=err;}
  }
  throw Error(file.path+' from '+file.url+': '+last);
}
let next=0;
const hashes=new Array(lock.files.length);
async function worker() {
  while(next<lock.files.length) {
    const i=next++;hashes[i]=await acquire(lock.files[i]);
    if(i%24===0)console.log('Verified '+lock.files[i].path);
  }
}
await Promise.all(Array.from({length:5},worker));
if(lock.files.some(f=>!f.sha256)) {
  if(!process.argv.includes('--pin'))throw Error('Missing SHA-256 pin');
  lock.files.forEach((f,i)=>{f.sha256=hashes[i];});
  await writeFile(lockFile,JSON.stringify(lock,null,2)+'\n');
}
if(process.argv.includes('--prepare')) {
  const blender=process.env.BLENDER_PATH||'blender',python=process.env.CC0_PYTHON||'python';
  for(const [exe,args] of [
    [python,['Tools/cc0/prepare_scans.py']],[python,['Tools/cc0/canopy.py']],
    ...['prepare_meshes.py','prepare_mantle.py','prepare_company.py'].map(name=>[blender,['-b','-t','3','-noaudio','--factory-startup','--disable-autoexec','--python-exit-code','2','-P','Tools/cc0/'+name]])
  ]) {
    const result=spawnSync(exe,args,{cwd:root,stdio:'inherit',windowsHide:true});
    if(result.error||result.status!==0)throw Error('Failed '+args.at(-1)+': '+(result.error??result.status));
  }
}
const credits=['# Frontline Lab 4.0.0 — CC0 art sources','',
  'The game uses artist-authored meshes, skeletal clips and scanned surfaces. The original works listed below were released under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Local derivatives use the same dedication.',
  '','Poly Haven provides the scanned materials and natural assets through its [public API](https://polyhaven.com/our-api). Original archives live in the local ThirdPartyAssets cache. Each SHA-256 identifies the upstream bytes before conversion.',
  ''];
for(const [id,source] of Object.entries(lock.sources)) {
  credits.push('## '+source.title,'',source.author+' · [Original release]('+source.page+') · [CC0 declaration]('+source.license+')','','| Source file | SHA-256 |','|---|---|');
  for(let i=0;i<lock.files.length;i++)if(lock.files[i].source===id) {
    const f=lock.files[i];credits.push('| ['+f.path.split('/').slice(1).join('/')+']('+f.url+') | \x60'+hashes[i]+'\x60 |');
  }
  credits.push('');
}
credits.push('## Files used by Unity','','The compiled GPU palettes sample the artists’ original skeletons. The armoured body uses crownjoshua’s plate geometry and deform weights fitted to the Universal Humanoid bind pose. The intermediate horse trot blends Quaternius’s walk and gallop. The foliage slices are photographed leaf/branch samples projected in depth from the original Poly Haven models.','','| Imported file | Source group | Local SHA-256 |','|---|---|');
for(const sub of ['Art','Actors','Resources/Scans']){
  const folder=join(destination,sub);
  if(!existsSync(folder))continue;
  for(const name of (await readdir(folder)).filter(n=>/\.(?:fbx|png)$/i.test(n)).sort()){
    const stem=name.replace(/\.[^.]+$/,'');
    let groups;
    if(sub==='Actors')groups=stem==='Horse'?['animals_pack']:['outfits','humans','motions','actors',...(stem==='Castellan'?['court']:[])];
    else if(sub==='Resources/Scans')groups=stem.startsWith('Wardrobe')?['outfits','humans']:
      stem==='FoliageViews'?['island_tree_01','pine_sapling_small','shrub_01']:
      Object.keys(lock.sources).filter(k=>lock.sources[k].page.startsWith('https://polyhaven.com/a/')&&
      (stem.startsWith('Specimen')===lock.files.some(f=>f.source===k&&f.path.endsWith('.gltf'))));
    else if(stem==='Mantle')groups=['actors'];
    else groups=[...new Set(lock.files.filter(f=>basename(f.path).replace(/\.[^.]+$/,'')===stem).map(f=>f.source))];
    if(!groups.length)throw Error('No provenance for '+name);
    const authors=groups.map(g=>'['+lock.sources[g].title+']('+lock.sources[g].page+')').join(', ');
    credits.push('| '+sub+'/'+name+' | '+authors+' | '+digest(await readFile(join(folder,name)))+' |');
  }
}
credits.push('');
await mkdir(destination,{recursive:true});
const creditPath=join(destination,'CREDITS-CC0.md'),creditText=credits.join('\n');
if(!existsSync(creditPath)||await readFile(creditPath,'utf8')!==creditText)await writeFile(creditPath,creditText);
console.log('Verified '+hashes.length+' source files.');
