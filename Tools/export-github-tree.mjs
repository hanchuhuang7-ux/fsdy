// Prepare a content-addressed review snapshot. Does not authenticate or publish.
import {execFileSync} from 'node:child_process';import {writeFileSync,mkdirSync} from 'node:fs';
const base=process.argv[2];if(!/^[a-f0-9]{40}$/.test(base??''))throw Error('Pass the inspected GitHub base commit SHA');
const git=(...args)=>execFileSync('git',args,{maxBuffer:32*1024*1024});
const changes=git('diff','--name-status','-z','--no-renames',base,'HEAD').toString('utf8').split('\0');const elements=[],binary=[];
for(let i=0;i<changes.length-1;){const status=changes[i++],path=changes[i++];if(status==='D'){elements.push({path,mode:'100644',type:'blob',sha:null});continue;}const bytes=git('show',`HEAD:${path}`),sha=git('rev-parse',`HEAD:${path}`).toString().trim();const mode=git('ls-tree','HEAD','--',path).toString().slice(0,6);if(bytes.includes(0)){binary.push({path,sha,base64:bytes.toString('base64')});elements.push({path,mode,type:'blob',sha});}else elements.push({path,mode,type:'blob',content:bytes.toString('utf8')});}
mkdirSync('Builds',{recursive:true});writeFileSync('Builds/github-tree.json',JSON.stringify({elements,binary,sourceCommit:git('rev-parse','HEAD').toString().trim(),sourceTree:git('rev-parse','HEAD^{tree}').toString().trim()}));console.log(`Prepared ${elements.length} changes; ${binary.length} binary resources`);
