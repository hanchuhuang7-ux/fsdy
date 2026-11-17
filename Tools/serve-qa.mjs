// Explicit local acceptance server. Not part of the hosted game.
import http from 'node:http';
import {readFile,writeFile,mkdir,stat} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=process.env.QA_PROFILE==='1'?fileURLToPath(new URL('../Builds/WebProfile/',import.meta.url)):fileURLToPath(new URL('../dist/',import.meta.url));
const output=fileURLToPath(new URL(process.env.QA_PROFILE==='1'?'../Builds/Visual08/WebProfile/':'../Builds/Visual08/Web/',import.meta.url));
await stat(resolve(root,'index.html'));await mkdir(output,{recursive:true});
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.wasm':'application/wasm','.json':'application/json','.png':'image/png'};
http.createServer(async(req,res)=>{
 try{
  const url=new URL(req.url,'http://localhost');
  if(req.method==='POST'&&url.pathname.startsWith('/__qa/')){
   const name=url.pathname.slice(6);if(!/^(v08-|visual-|combat-)[a-zA-Z0-9-]+\.(png|json)$/.test(name))throw Error('Invalid result name');
   let size=0;const chunks=[];for await(const chunk of req){size+=chunk.length;if(size>8*1024*1024)throw Error('Result too large');chunks.push(chunk);}
   await writeFile(resolve(output,name),Buffer.concat(chunks));res.writeHead(200);res.end('ok');console.log(`QA ${name} ${size} bytes`);return;
  }
  const path=resolve(root,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));if(!path.startsWith(resolve(root)+sep))throw Error('Forbidden');
  const content=await readFile(path);res.writeHead(200,{'Content-Type':types[extname(path)]||'application/octet-stream','Content-Length':content.length,'Cache-Control':'no-store'});res.end(content);
 }catch(error){res.writeHead(400);res.end(String(error.message));}
}).listen(5180,'127.0.0.1',()=>console.log('Local: http://127.0.0.1:5180/?frontlineQA=1'));
