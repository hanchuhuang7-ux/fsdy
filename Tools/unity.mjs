import {existsSync,mkdirSync,readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {resolve,dirname} from 'node:path';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),[task='build',...extra]=process.argv.slice(2);
mkdirSync(resolve(root,'Builds/Logs'),{recursive:true});
const editor=process.env.UNITY_EDITOR_PATH||'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe';
let executable,args,log;
if(['build','windows','web','physics','profile'].includes(task)){
 if(!existsSync(editor))throw Error('请安装 Unity 6000.6.9f1，并设置 UNITY_EDITOR_PATH 指向 Unity.exe。');
 const target={build:'Both',windows:'Windows',web:'Web',physics:'CheckPhysics',profile:'ProfileBoth'}[task];
 log=resolve(root,`Builds/Logs/${target}.log`);executable=editor;
 args=['-batchmode','-nographics',...(task==='physics'?[]:['-quit']),'-projectPath',resolve(root,'Unity'),'-executeMethod',`Frontline.Editor.BuildTools.${target}`,'-logFile',log];
 if(task==='physics')args.push('--frontline-task','checks','--out',resolve(root,'Builds/Checks'));
} else {
 executable=process.env.FRONTLINE_EXE||resolve(root,'Builds/Windows/FrontlineLab.exe');
 if(!existsSync(executable))throw Error('请先运行 npm run build:windows，或设置 FRONTLINE_EXE。');
 const outIndex=extra.indexOf('--out'),out=resolve(root,outIndex>=0?extra[outIndex+1]:`Builds/${task}`);mkdirSync(out,{recursive:true});log=resolve(out,'player.log');
 args=['-batchmode','-nographics','--frontline-task',task,'-logFile',log,...extra];if(outIndex<0)args.push('--out',out);
}
console.log(`Unity ${task} started; log: ${log}`);
const result=spawnSync(executable,args,{cwd:root,stdio:'inherit',windowsHide:true});
if(result.error)throw result.error;if(existsSync(log)){const lines=readFileSync(log,'utf8').split(/\r?\n/);console.log(lines.filter(s=>/FRONTLINE_|Agent round|Exception:|error CS|Shader error/.test(s)).join('\n'));}
if(result.status!==0)throw Error(`Unity ${task} failed (${result.status}); see ${log}`);console.log(`Unity ${task} succeeded.`);
