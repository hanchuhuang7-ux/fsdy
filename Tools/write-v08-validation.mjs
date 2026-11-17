import {readFileSync,writeFileSync,mkdirSync,readdirSync,statSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {join} from 'node:path';
const read=p=>readFileSync(p,'utf8'),json=p=>JSON.parse(read(p));
const median=a=>[...a].sort((a,b)=>a-b)[Math.floor(a.length/2)];
const cpu=p=>{const data=json('Builds/'+p+'/benchmark.json');return {median:median(data.rows.filter(r=>r.fixture==='large-battle'&&r.run>0).map(r=>r.millisecondsPerStep)),data};};
const bytes=p=>readdirSync(p).reduce((n,f)=>n+(statSync(join(p,f)).isDirectory()?bytes(join(p,f)):statSync(join(p,f)).size),0);
const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const stages=['Benchmark08Baseline','Benchmark08First','Benchmark08IsolatedInitial','Benchmark08Hot','Benchmark08Diagnostic2','Benchmark08Candidate','Benchmark08Release','Benchmark08ReleaseFresh'].map(name=>({name,...cpu(name)}));
const release=stages.find(x=>x.name==='Benchmark08Release'),fresh=stages.at(-1);
const win=json('Builds/Visual08/WindowsRelease/visual-WindowsPlayer-1280.json'),web=json('Builds/Visual08/Web/visual-WebGLPlayer-1280.json'),mobile=json('Builds/Visual08/Web/visual-WebGLPlayer-390.json');
const winDev=json('Builds/Visual08/WindowsProfile/visual-WindowsPlayer-1280.json'),webDev=json('Builds/Visual08/WebProfile/visual-WebGLPlayer-1280.json');
const old=json('docs/validation/unity-v0.7.json'),cross=json('Builds/CrossPlatform08/report.json');
const builds=[...read('Builds/Logs/Both.log').matchAll(/FRONTLINE_BUILD_OK (\w+) (\d+) bytes ([^\r\n]+)/g)].map(m=>({target:m[1],bytes:Number(m[2]),time:m[3]}));
if(builds.length!==2||win.developmentBuild||web.developmentBuild||!winDev.developmentBuild||!webDev.developmentBuild)throw Error('Wrong release/profile outputs');
if(release.median>.34)throw Error('Final prepared benchmark exceeds .34 ms/step');
if(read('Builds/Checks08Final/repeat-0.replay.json')!==read('Builds/Checks08Final/repeat-1.replay.json'))throw Error('Full current trajectory changed');
const report={version:'0.8.0',date:new Date().toISOString(),timezone:'Asia/Shanghai',unity:'6000.6.9f1',tests:{javascript:87,csharp:84,total:171},
 cpu:{thresholdMilliseconds:.34,prepared:release.median,fresh:fresh.median,old07Prepared:.2848863888888889,sameMachine07:stages[0].median,old06:.4365686111111111,oldRapier:.34082819444444445,stages,notes:['720 fixed steps, snapshot every six steps; warmup 1 + measured 5. Independent prepared frames and physics pools are allocated before timing. Product preallocates up to 902 frames.','Benchmark08First ran alongside checks and is retained as a contaminated diagnostic, not an acceptance result.','Benchmark08Hot retained its 0.3867 median and its 0.3276–0.3966 spread; no best-run substitution. Diagnostic2 uses extra timing probes.','Rules, projectile initialization and collision workload changed in 0.8. Prepared passes .34; fresh exceeds .34 and is reported separately.']},
 render:{windows:win,web,mobile,windowsDevelopment:winDev,webDevelopment:webDev},motion:json('Builds/Checks08Final/motion.json'),
 uiChecks:json('Builds/Visual08/mobile-input.json'),history:json('Unity/Assets/Frontline/Resources/history-goldens.json'),currentRepeat:{byteIdentical:true,sha256:sha('Builds/Checks08Final/repeat-0.replay.json'),ticks:json('Builds/Checks08Final/repeat-0.replay.json').frames.at(-1).tick},crossPlatform:cross,
 smoke:json('Builds/Smoke08/smoke.json'),agent:{stats:json('Builds/Agents08/agent-stats.json'),series:json('Builds/Agents08/series.json')},builds,webDirectoryBytes:bytes('dist'),bakedMeshes:128,
 frozenTrees:{src:'7f09629cfca1ebe5c4c27fcb034857c8940759ef',test:'7e1293310f2d3d79c2d4cb2498d9d85df598e25a',scripts:'192e32422c014cacd2384f298736aaf2dedea9df'},
 limitations:['PhysX seeds require the same engine/platform/editor; portable replay uses complete trajectories.','Mono/WebGL thread-allocation APIs fail calibration. Release GC=-1 is unavailable; genuine frame GC comes from Development ProfilerRecorder, including UI/QA overhead.','Web millisecond timing is quantized; reciprocal FPS is approximate, not display refresh. QA uses DPR=1; ordinary site rendering allows DPR up to 2.','Fresh snapshot CPU exceeds .34 ms/step. Prepared CPU also rises versus 0.7; the new physics workload is documented rather than called unchanged.','Unity Editor SearchDatabase indexing exception persisted with exit 0; independent player checks passed. One temporary ILPP pipe build failure was retried successfully.','390px uses a real browser pointer; a physical phone multitouch session was not performed. Existing rare CJK font subset and Windows-only external process Bot limitations remain.','The production site retains its existing owner-only audience and normal ChatGPT sign-in gate. A successful deployment is not a claim that a gated production battle was opened.']};
mkdirSync('docs/validation',{recursive:true});writeFileSync('docs/validation/unity-v0.8.json',JSON.stringify(report,null,2)+'\n');
const lines=['# Unity 0.8.0 验证记录','',report.date+' · Unity 6000.6.9f1 · Windows x64 / WebGL','',
'受击、移动和碰撞升级已经完成。Core 零 UnityEngine、固定60Hz、3米网格、实例合批、冻结旧目录与0.1–0.7兼容路径均保留。布阵 schema 仍为0.6.0，新增战斗规则使用 unity-0.8.0 / 回放v3。','',
'## 行为与真实物理','',
'受击不再按1/6抽样。96个特效、24个音效提示预热复用；普通同目标4步节流，格挡/重击/致命命中绕过节流。六个可省略的浮点通道记录闪白、来向倾斜、实际平面速度/方向和累计步程。矩阵和实例参数直接表现，不增加材质或战斗刚体。盾牌正面击退系数0.22–0.40，轻/中/重硬直分别约0.055/0.13/0.42秒；重击能延缓前摇。','',
'兵种角速度不同，骑兵随速度减小转向能力并保持前向惯性；静止停止摆腿，后撤反向步态，速度决定步幅与节奏。桥/门降低横向友军死顶；坡道按切线控制。实测发现高台冰面原静摩擦抵消低响应推力，已用低摩擦材质修正。','',
'马身双胶囊随Y轴转向，车架为盒体，岩石为与视觉同朝向的最多64三角面凸包。按实际高速启用 ContinuousSpeculative。对象池启用后重设刚体发射姿态/速度，修复禁用状态写入被重置的问题；六刚体残骸保留真实出生姿态和动量。肩高箭能越过普通盾卫，低位箭仍被友军挡住；近距离友方/中立旗杆豁免0.10秒后恢复。','',
'原生单骑碰撞用例禁用了普通攻击冷却，以确认命中确由实际接触触发：最高4.6684m/s、217步接触、正面盾卫生命220→193。桥/遗迹/高台8人纵队均8/8通过；高台下坡766次样本最大额外离地0.0321米。车架和24m/s战马墙体测试、双胶囊主人忽略、旗杆到期与池重用检查均通过。','',
'## CPU：所有测法与中间失败','',
'720步、每6步独立快照，预热1轮、测量5轮。预分配与物理池初始化不计入计时；实际开战前最多预分配902帧。以下为45v45中位数。','',
'| 测量 | ms/步 |','|---|---:|',
'| Unity0.6历史 | 0.436569 |','| Rapier旧版 | 0.340828 |','| Unity0.7历史最终 | 0.284886 |',
...stages.map(x=>'| '+x.name+' | '+x.median.toFixed(6)+' |'),'',
'正式预分配测法满足≤0.34；fresh测法未满足，不能将其隐藏。正式预分配比0.7历史值上升'+((release.median/.2848863888888889-1)*100).toFixed(1)+'%，比本机0.7复测上升'+((release.median/stages[0].median-1)*100).toFixed(1)+'%；新轮廓、真实发射、冲锋和战斗进程改变了负载。此结果不是“性能完全不变”。First为并行检查污染的诊断；Hot和Diagnostic2保留全部原始分轮值。详见JSON。','',
'## 图形与每帧GC','',
'| 成品 / 初始兵力 | 中位帧时ms | p95 ms | 实际Draw Calls |','|---|---:|---:|---:|',
...[['Windows Release',win],['WebGL 1280',web],['WebGL 390',mobile]].flatMap(([name,d])=>d.rows.map(r=>'| '+name+' '+r.unitsPerTeam+'v'+r.unitsPerTeam+' | '+r.medianFrameMilliseconds.toFixed(3)+' | '+r.p95FrameMilliseconds.toFixed(3)+' | '+r.medianBatches+' |')),'',
'Draw Calls含阴影与UI，不把手工提交数称为实际Batches。网页毫秒计时量化，倒数FPS仅为近似；1280×800和390×844验收DPR=1。三档草木/粒子/阴影联动与静态缓存均检查，采样期间静态重建和对象池耗尽均0。','',
'| Development / 初始兵力 | GC中位B/帧 | 平均 | 峰值 |','|---|---:|---:|---:|',
...[['Windows',winDev],['WebGL',webDev]].flatMap(([name,d])=>d.rows.map(r=>'| '+name+' '+r.unitsPerTeam+'v'+r.unitsPerTeam+' | '+r.medianGcBytes+' | '+r.meanGcBytes.toFixed(1)+' | '+r.maxGcBytes+' |')),'',
'Release线程分配接口校准无效，不将恒0当作零GC；Release不可读为-1。Development使用真实ProfilerRecorder，包含UI/验收开销。0.7 Windows Development平均约883/695B，本轮显著降低。','',
'## 测试、回放与交付','',
'npm test：87项JS+84项C#=171项全通过。新增覆盖平滑转向、盾牌减击退、重击延缓、池上限、独立快照、真实速度步态、后撤、坡道、窄口、发射和历史身份。npm run test:physics、独立checks、六图smoke、Bot Bo3均通过；完整当前回放两次逐字相同，最终'+report.currentRepeat.ticks+'步蓝胜。Bot Bo3红2:0，122次观察、12条合法军令、0拒收/超时。','',
'冻结0.6/0.7规则和物理适配器后，完整历史轨迹SHA与原件逐字一致：','',...report.history.map(x=>'- '+x.engine+'：'+x.tick+'步'+x.winner+'胜，SHA-256 '+x.sha256),'',
'实际WebGL工坊导入/导出的0.6、0.7、0.8完整轨迹对照见JSON crossPlatform字段。旧格式新增字段缺省0并省略写出。','',
'npm run build等待退出0及两端FRONTLINE_BUILD_OK；正式包未混入Development成品。','',...builds.map(x=>'- '+x.target+'：构建摘要'+x.bytes+'字节，'+x.time),'',
'相对0.7构建摘要：Windows '+((builds[0].bytes/old.builds[0].bytes-1)*100).toFixed(2)+'%，WebGL '+((builds[1].bytes/old.builds[1].bytes-1)*100).toFixed(2)+'%。实际Web目录为'+report.webDirectoryBytes+'字节；交付ZIP精确大小、SHA与发布提交见交付目录release.json/SHA256。128个Mesh保留，无新增贴图。','',
'原始截图交付含Windows、WebGL桌面与390px的连续冲锋/格挡帧、车架前后、窄口、高台和45/60兵力；对比页使用未修改原图，0.7默认阵型3秒作历史参照。车架图明确是一次前推冲量的受控碰撞用例。','',
'![盾牌格挡连续帧](screenshots/v08/block.png)','![骑兵接战](screenshots/v08/charge.png)','![车架墙体接触](screenshots/v08/cart.png)','![高台移动](screenshots/v08/slope.png)','',
'## 限制与环境问题','',...report.limitations.map(x=>'- '+x),'' ];
writeFileSync('docs/V0.8_VALIDATION.md',lines.join('\n'));
console.log('v0.8 report written; prepared='+release.median+' fresh='+fresh.median);
