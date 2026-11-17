// Optional maintenance tool. The generated OFL font is committed, so builds need no font download.
import subsetFont from 'subset-font';
import {readFile,writeFile,readdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
const base=new URL('../../',import.meta.url),assets=new URL('Unity/Assets/Frontline/',base);
const url='https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf';
const source=process.argv[2]?await readFile(process.argv[2]):Buffer.from(await (await fetch(url)).arrayBuffer());
const chars=new Set();for(let i=0x20;i<=0x24f;i++)chars.add(String.fromCodePoint(i));
for(const [a,b]of [[0x2000,0x206f],[0x2460,0x24ff],[0x2600,0x27ff],[0x3000,0x303f],[0xff00,0xffef]])for(let i=a;i<=b;i++)chars.add(String.fromCodePoint(i));
const decoder=new TextDecoder('gb18030');for(let a=0xa1;a<=0xf7;a++)for(let b=0xa1;b<=0xfe;b++){const c=decoder.decode(new Uint8Array([a,b]));if(c!=='�')chars.add(c);}
async function scan(dir){for(const e of await readdir(dir,{withFileTypes:true})){const child=new URL(e.name+(e.isDirectory()?'/':''),dir);if(e.isDirectory())await scan(child);else if(/\.(cs|json)$/.test(e.name))for(const c of await readFile(child,'utf8'))chars.add(c);}}
await scan(assets);const text=[...chars].sort((a,b)=>a.codePointAt(0)-b.codePointAt(0)).join('');
const result=await subsetFont(source,text,{targetFormat:'sfnt',preserveNameIds:[0,1,2,3,4,5,6,13,14],noLayoutClosure:true});
const target=new URL('Resources/NotoSansCJKsc-Regular.otf',assets);await writeFile(target,result);
const hash=b=>createHash('sha256').update(b).digest('hex');
await writeFile(new URL('manifest.json',import.meta.url),JSON.stringify({license:'SIL OFL 1.1',original:url,sourceSha256:hash(source),subsetSha256:hash(result),sourceBytes:source.length,subsetBytes:result.length,requestedCharacters:chars.size,coverage:'GB2312 byte grid decoded with GB18030, Latin, punctuation, symbols, fullwidth and all shipped C#/JSON text'},null,2)+'\n');
console.log(`OFL font subset: ${source.length} -> ${result.length} bytes (${chars.size} requested characters); ${fileURLToPath(target)}`);

